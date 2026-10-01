using StackExchange.Redis;

namespace Opdeweg.Infrastructure.Redis;

public interface IDistributedLock
{
    Task<IAsyncDisposable> AcquireAsync(string name, TimeSpan expiry, CancellationToken cancellationToken);
}

/// <summary>
/// Simple Redis lease (SET NX PX + token-checked release). Serialises proximity processing across
/// API instances; region partitioning would replace this when a single writer stops being enough.
/// </summary>
internal sealed class RedisDistributedLock(IConnectionMultiplexer redis, RedisKeys keys) : IDistributedLock
{
    public async Task<IAsyncDisposable> AcquireAsync(string name, TimeSpan expiry, CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        var key = keys.Lock(name);
        var token = Guid.NewGuid().ToString("N");
        var delay = TimeSpan.FromMilliseconds(5);

        while (!await db.LockTakeAsync(key, token, expiry))
        {
            await Task.Delay(delay, cancellationToken);
            delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 2, 100));
        }

        return new Lease(db, key, token);
    }

    private sealed class Lease(IDatabase db, RedisKey key, RedisValue token) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync() => await db.LockReleaseAsync(key, token);
    }
}
