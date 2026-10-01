using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Opdeweg.Api.Configuration;
using Opdeweg.Api.Hubs;
using Opdeweg.Api.Middleware;
using Opdeweg.Application;
using Opdeweg.Application.Interfaces;
using Opdeweg.Infrastructure;
using Opdeweg.Infrastructure.Persistence;
using Opdeweg.Infrastructure.Redis;
using Opdeweg.Infrastructure.Security;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
EnvironmentConfiguration.Apply(builder);

builder.Services
    .AddApplication(builder.Configuration)
    .AddInfrastructure(builder.Configuration);

// ---- HTTP API ----
builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)))
    .ConfigureApiBehaviorOptions(o =>
    {
        var defaultFactory = o.InvalidModelStateResponseFactory;
        o.InvalidModelStateResponseFactory = context =>
        {
            var result = defaultFactory(context);
            if (result is ObjectResult { Value: ProblemDetails problem })
            {
                problem.Extensions["code"] = "validation_failed";
            }

            return result;
        };
    });

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<AppExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddOpdewegRateLimiting(builder.Configuration);

// ---- Authentication: short-lived JWT access tokens ----
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((options, jwt) =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Value.Issuer,
            ValidAudience = jwt.Value.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(jwt.Value.SecretBytes),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "sub",
        };
        options.Events = new JwtBearerEvents
        {
            // WebSockets cannot send headers from React Native, so the hub accepts the token in the query string.
            OnMessageReceived = context =>
            {
                var token = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments(ProximityHub.Path))
                {
                    context.Token = token;
                }

                return Task.CompletedTask;
            },
        };
    });
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// ---- Realtime ----
var signalR = builder.Services
    .AddSignalR(o =>
    {
        o.EnableDetailedErrors = builder.Environment.IsDevelopment();
        o.KeepAliveInterval = TimeSpan.FromSeconds(15);
        o.ClientTimeoutInterval = TimeSpan.FromSeconds(45);
        o.AddFilter<HubRateLimitFilter>();
    })
    .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));

if (builder.Configuration.GetValue("Redis:UseSignalRBackplane", true))
{
    // Lets proximity events reach users connected to any API instance.
    signalR.AddStackExchangeRedis(o =>
    {
        o.Configuration.ChannelPrefix = RedisChannel.Literal(builder.Configuration.GetValue("Redis:KeyPrefix", "opd:") + "signalr");
        o.ConnectionFactory = async writer =>
        {
            var options = ConfigurationOptions.Parse(builder.Configuration.GetConnectionString("Redis")!);
            options.AbortOnConnectFail = false;
            return await ConnectionMultiplexer.ConnectAsync(options, writer);
        };
    });
}

builder.Services.AddSingleton<HubRateLimitFilter>();
builder.Services.AddSingleton<IUserIdProvider, SubjectUserIdProvider>();
builder.Services.AddSingleton<IRealtimeNotifier, SignalRRealtimeNotifier>();

// ---- Reverse proxy (TLS termination) ----
var behindProxy = builder.Configuration.GetValue("ForwardedHeaders:Enabled", false);
if (behindProxy)
{
    // Only enable when the API is reachable exclusively through a trusted load balancer.
    builder.Services.Configure<ForwardedHeadersOptions>(o =>
    {
        o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        o.KnownIPNetworks.Clear();
        o.KnownProxies.Clear();
    });
}

// ---- Health ----
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("postgres", tags: ["ready"])
    .AddCheck<RedisHealthCheck>("redis", tags: ["ready"]);

var app = builder.Build();

if (app.Configuration.GetValue("Database:MigrateOnStartup", app.Environment.IsDevelopment()))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

if (behindProxy)
{
    app.UseForwardedHeaders();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseMiddleware<RequestLoggingMiddleware>();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.MapControllers();
app.MapHub<ProximityHub>(ProximityHub.Path);
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous().DisableRateLimiting();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") }).AllowAnonymous().DisableRateLimiting();

await app.RunAsync();

/// <summary>Entry point marker for integration tests.</summary>
public partial class Program;
