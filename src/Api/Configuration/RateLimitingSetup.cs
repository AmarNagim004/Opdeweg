using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Opdeweg.Api.Extensions;

namespace Opdeweg.Api.Configuration;

internal sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public int AuthPermitsPerMinute { get; set; } = 10;

    public int GlobalPermitsPerMinute { get; set; } = 240;

    public int LocationBurst { get; set; } = 20;

    public int HubInvocationsPerTenSeconds { get; set; } = 30;
}

internal static class RateLimitingSetup
{
    public const string Auth = "auth";
    public const string Location = "location";

    public static IServiceCollection AddOpdewegRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>() ?? new RateLimitingOptions();
        services.AddSingleton(options);

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                await context.HttpContext.Response.WriteAsJsonAsync(
                    new { title = "Too many requests.", status = 429, code = "rate_limited" },
                    cancellationToken);
            };

            // Credential endpoints: strict, per client address.
            limiter.AddPolicy(Auth, context => RateLimitPartition.GetFixedWindowLimiter(
                ClientAddress(context),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = options.AuthPermitsPerMinute, Window = TimeSpan.FromMinutes(1) }));

            // Location ingestion: ~1 update/s sustained per user with a small burst (the app sends far less).
            limiter.AddPolicy(Location, context => RateLimitPartition.GetTokenBucketLimiter(
                PartitionKey(context),
                _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = options.LocationBurst,
                    TokensPerPeriod = 10,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(10),
                    QueueLimit = 0,
                }));

            limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context => RateLimitPartition.GetTokenBucketLimiter(
                PartitionKey(context),
                _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = options.GlobalPermitsPerMinute,
                    TokensPerPeriod = options.GlobalPermitsPerMinute,
                    ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        });

        return services;
    }

    private static string PartitionKey(HttpContext context) =>
        context.User.GetUserIdOrNull()?.ToString() ?? ClientAddress(context);

    private static string ClientAddress(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
