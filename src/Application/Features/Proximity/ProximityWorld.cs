namespace Opdeweg.Application.Features.Proximity;

/// <summary>
/// The local neighbourhood the grouping engine reasons about: a subject driver, nearby candidates
/// from the spatial index, and the full rosters of every group any of them belongs to.
/// </summary>
public sealed class ProximityWorld
{
    private readonly Dictionary<Guid, DriverState> _drivers = [];
    private readonly Dictionary<Guid, string> _groupOf = [];
    private readonly Dictionary<string, HashSet<Guid>> _members = new(StringComparer.Ordinal);

    public IEnumerable<DriverState> Drivers => _drivers.Values;

    public IReadOnlyCollection<string> GroupIds => _members.Keys;

    public void AddDriver(DriverState driver) => _drivers[driver.UserId] = driver;

    public void AddMembership(Guid userId, string groupId) => Join(userId, groupId);

    public DriverState? GetDriver(Guid userId) => _drivers.GetValueOrDefault(userId);

    public string? GetGroup(Guid userId) => _groupOf.GetValueOrDefault(userId);

    public IReadOnlyCollection<Guid> GetMembers(string groupId) =>
        _members.TryGetValue(groupId, out var members) ? members : [];

    internal void Join(Guid userId, string groupId)
    {
        Leave(userId);
        if (!_members.TryGetValue(groupId, out var members))
        {
            members = [];
            _members[groupId] = members;
        }

        members.Add(userId);
        _groupOf[userId] = groupId;
    }

    internal void Leave(Guid userId)
    {
        if (!_groupOf.Remove(userId, out var groupId))
        {
            return;
        }

        if (_members.TryGetValue(groupId, out var members))
        {
            members.Remove(userId);
            if (members.Count == 0)
            {
                _members.Remove(groupId);
            }
        }
    }
}
