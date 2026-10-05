using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Opdeweg.Api.Middleware;

internal sealed class RedisHealthCheck(IConnectionMultiplexer redis) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var latency = await redis.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy($"PING {latency.TotalMilliseconds:F1} ms");
        }
        catch (Exception ex) when (ex is RedisException or RedisTimeoutException)
        {
            return HealthCheckResult.Unhealthy("Redis unreachable", ex);
        }
    }
}
