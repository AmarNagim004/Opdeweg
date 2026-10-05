using System.ComponentModel.DataAnnotations;

namespace Opdeweg.Infrastructure.Redis;

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    /// <summary>Prefix for every key, allowing several environments to share one Redis safely.</summary>
    [Required, MinLength(1), MaxLength(32)]
    public string KeyPrefix { get; set; } = "opd:";

    /// <summary>Use Redis as the SignalR backplane so events reach users connected to any API instance.</summary>
    public bool UseSignalRBackplane { get; set; } = true;
}

/// <summary>
/// Key layout (all values expire automatically):
/// <list type="bullet">
/// <item><c>session:{user}</c> hash — active driving session projection and current group.</item>
/// <item><c>pos:{user}</c> hash — the single latest usable fix (no history).</item>
/// <item><c>geo</c> GEO set — spatial index of drivers with a live position.</item>
/// <item><c>seen</c> sorted set — last accepted update per driver, for stale/idle sweeps.</item>
/// <item><c>grp:{group}</c> set — proximity group roster.</item>
/// </list>
/// </summary>
public sealed class RedisKeys(Microsoft.Extensions.Options.IOptions<RedisOptions> options)
{
    private readonly string _prefix = options.Value.KeyPrefix;

    public string Session(Guid userId) => $"{_prefix}session:{userId:N}";

    public string Position(Guid userId) => $"{_prefix}pos:{userId:N}";

    public string Geo => $"{_prefix}geo";

    public string Seen => $"{_prefix}seen";

    public string Group(string groupId) => $"{_prefix}grp:{groupId}";

    public string Lock(string name) => $"{_prefix}lock:{name}";

    public static string Member(Guid userId) => userId.ToString("N");
}
