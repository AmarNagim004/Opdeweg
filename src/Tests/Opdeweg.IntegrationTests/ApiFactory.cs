using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Opdeweg.Application.DTOs;
using Opdeweg.Application.Interfaces;
using StackExchange.Redis;

[assembly: AssemblyFixture(typeof(Opdeweg.IntegrationTests.ApiFactory))]

namespace Opdeweg.IntegrationTests;

/// <summary>
/// Boots the real API against real PostgreSQL/PostGIS and Redis. Configure with
/// OPDEWEG_TEST_DATABASE / OPDEWEG_TEST_REDIS; tests are skipped when either is unreachable.
/// The SFU admin API is replaced with a recorder so tests do not need a LiveKit server.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly string _database = Environment.GetEnvironmentVariable("OPDEWEG_TEST_DATABASE")
        ?? "Host=localhost;Port=5432;Database=opdeweg_test;Username=opdeweg;Password=opdeweg";

    private readonly string _redis = Environment.GetEnvironmentVariable("OPDEWEG_TEST_REDIS") ?? "localhost:6379,defaultDatabase=15";

    private readonly string _keyPrefix = $"opdt-{Guid.NewGuid():N}:"[..14] + ":";

    public string? SkipReason { get; private set; }

    public RecordingRoomAdmin RoomAdmin { get; } = new();

    public async ValueTask InitializeAsync()
    {
        try
        {
            await using (var connection = new NpgsqlConnection(_database))
            {
                await connection.OpenAsync();
            }

            await using var redis = await ConnectionMultiplexer.ConnectAsync(_redis);
            await redis.GetDatabase().PingAsync();
        }
        catch (Exception ex) when (ex is NpgsqlException or RedisException or System.Net.Sockets.SocketException or InvalidOperationException)
        {
            SkipReason = $"PostgreSQL/Redis not reachable for integration tests: {ex.Message}";
            return;
        }

        // Fresh schema per run (the database is dedicated to tests).
        await using (var reset = new NpgsqlConnection(_database))
        {
            await reset.OpenAsync();
            await using var cmd = new NpgsqlCommand("DROP SCHEMA public CASCADE; CREATE SCHEMA public;", reset);
            await cmd.ExecuteNonQueryAsync();
        }

        _ = Server; // start the host (applies migrations)
    }

    public override async ValueTask DisposeAsync()
    {
        if (SkipReason is null)
        {
            await using var redis = await ConnectionMultiplexer.ConnectAsync(_redis + ",allowAdmin=true");
            foreach (var endpoint in redis.GetEndPoints())
            {
                var server = redis.GetServer(endpoint);
                var keys = server.Keys(redis.GetDatabase().Database, _keyPrefix + "*").ToArray();
                if (keys.Length > 0)
                {
                    await redis.GetDatabase().KeyDeleteAsync(keys);
                }
            }
        }

        await base.DisposeAsync();
    }

    public void SkipIfUnavailable() => Assert.SkipWhen(SkipReason is not null, SkipReason ?? string.Empty);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Database"] = _database,
            ["ConnectionStrings:Redis"] = _redis,
            ["Database:MigrateOnStartup"] = "true",
            ["Redis:KeyPrefix"] = _keyPrefix,
            ["Jwt:Secret"] = "integration-tests-only-secret-0123456789abcdef",
            ["LiveKit:Url"] = "wss://voice.test",
            ["LiveKit:ApiKey"] = "testkey",
            ["LiveKit:ApiSecret"] = "integration-tests-livekit-secret-0123456789",
            ["RateLimiting:AuthPermitsPerMinute"] = "1000",
            ["RateLimiting:GlobalPermitsPerMinute"] = "10000",
            ["RateLimiting:LocationBurst"] = "1000",
            ["Auth:RefreshReuseGraceSeconds"] = "2",
            ["Logging:LogLevel:Default"] = "Warning",
        };

        builder.UseEnvironment("Testing");

        // Minimal hosting reads some values while registering services (host settings) and binds options
        // later from app configuration, where appsettings.json would otherwise win: set both.
        foreach (var (key, value) in settings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(settings));

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IVoiceRoomAdmin>();
            services.AddSingleton<IVoiceRoomAdmin>(RoomAdmin);
        });
    }

    public async Task<TestDriver> RegisterAsync(string displayName)
    {
        var client = CreateClient();
        var email = $"{displayName.ToLowerInvariant()}-{Guid.NewGuid():N}@example.com";
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = "correct horse battery", displayName }, Json);
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return new TestDriver(this, client, auth, email);
    }

    public HubConnection CreateHubConnection(string accessToken) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(Server.BaseAddress, "/hubs/proximity"), options =>
            {
                options.HttpMessageHandlerFactory = _ => Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
                options.Transports = HttpTransportType.LongPolling;
            })
            .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)))
            .Build();
}

public sealed record TestDriver(ApiFactory Factory, HttpClient Http, AuthResponse Auth, string Email)
{
    public async Task<HttpResponseMessage> PostLocationAsync(double latitude, double longitude, double? accuracy = 8, DateTimeOffset? timestamp = null) =>
        await Http.PostAsJsonAsync(
            "/api/v1/driving/location",
            new { latitude, longitude, accuracy, speed = 12.0, heading = 90.0, timestamp = timestamp ?? DateTimeOffset.UtcNow },
            ApiFactory.Json);

    public async Task<LocationUpdateResponse> MoveAsync(Domain.ValueObjects.GeoPoint point)
    {
        var response = await PostLocationAsync(point.Latitude, point.Longitude);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LocationUpdateResponse>(ApiFactory.Json))!;
    }

    public async Task StartDrivingAsync() => (await Http.PostAsync("/api/v1/driving/sessions", null)).EnsureSuccessStatusCode();
}

public sealed class RecordingRoomAdmin : IVoiceRoomAdmin
{
    private readonly List<string> _deleted = [];

    public IReadOnlyList<string> Deleted
    {
        get
        {
            lock (_deleted)
            {
                return [.. _deleted];
            }
        }
    }

    public Task<IReadOnlyList<string>> ListParticipantIdentitiesAsync(string room, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>([]);

    public Task RemoveParticipantAsync(string room, string identity, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task DeleteRoomAsync(string room, CancellationToken cancellationToken)
    {
        lock (_deleted)
        {
            _deleted.Add(room);
        }

        return Task.CompletedTask;
    }
}
