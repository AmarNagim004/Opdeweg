namespace Opdeweg.Application.Features.Proximity;

/// <summary>The outcome of one proximity evaluation: what to persist and whom to notify.</summary>
public sealed class ProximityPlan
{
    public static readonly ProximityPlan Empty = new([], [], new Dictionary<string, IReadOnlyList<Guid>>(), []);

    public ProximityPlan(
        IReadOnlyList<GroupMembershipChange> changes,
        IReadOnlyList<MembershipTransition> transitions,
        IReadOnlyDictionary<string, IReadOnlyList<Guid>> rosters,
        IReadOnlyCollection<Guid> reEvaluate)
    {
        Changes = changes;
        Transitions = transitions;
        Rosters = rosters;
        ReEvaluate = reEvaluate;
    }

    /// <summary>Ordered membership mutations to apply atomically.</summary>
    public IReadOnlyList<GroupMembershipChange> Changes { get; }

    /// <summary>Net per-driver transitions (drivers that left and re-joined the same group are omitted).</summary>
    public IReadOnlyList<MembershipTransition> Transitions { get; }

    /// <summary>Final roster of every group touched by the plan. An empty roster means the group dissolved.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<Guid>> Rosters { get; }

    /// <summary>Drivers displaced by this plan who may now fit another group.</summary>
    public IReadOnlyCollection<Guid> ReEvaluate { get; }

    public bool IsEmpty => Changes.Count == 0;
}

internal sealed class ProximityPlanBuilder(ProximityWorld world)
{
    private readonly List<GroupMembershipChange> _changes = [];
    private readonly Dictionary<Guid, string?> _initialGroup = [];
    private readonly Dictionary<Guid, ProximityChangeReason> _lastReason = [];
    private readonly List<string> _touchedGroups = [];
    private readonly HashSet<Guid> _reEvaluate = [];

    public void Join(Guid userId, string groupId, ProximityChangeReason reason)
    {
        if (world.GetGroup(userId) == groupId)
        {
            return;
        }

        if (world.GetGroup(userId) is not null)
        {
            Leave(userId, reason);
        }

        Remember(userId);
        world.Join(userId, groupId);
        _changes.Add(new GroupMembershipChange(userId, groupId, MembershipChangeKind.Joined, reason));
        Touch(groupId);
        _lastReason[userId] = reason;
    }

    public void Leave(Guid userId, ProximityChangeReason reason)
    {
        var groupId = world.GetGroup(userId);
        if (groupId is null)
        {
            return;
        }

        Remember(userId);
        world.Leave(userId);
        _changes.Add(new GroupMembershipChange(userId, groupId, MembershipChangeKind.Left, reason));
        Touch(groupId);
        _lastReason[userId] = reason;
    }

    public void ReEvaluate(Guid userId) => _reEvaluate.Add(userId);

    public ProximityPlan Build(Guid? evaluatedUser = null)
    {
        if (_changes.Count == 0)
        {
            return ProximityPlan.Empty;
        }

        var transitions = new List<MembershipTransition>();
        foreach (var (userId, from) in _initialGroup)
        {
            var to = world.GetGroup(userId);
            if (!string.Equals(from, to, StringComparison.Ordinal))
            {
                transitions.Add(new MembershipTransition(userId, from, to, _lastReason[userId]));
            }
        }

        var rosters = new Dictionary<string, IReadOnlyList<Guid>>(StringComparer.Ordinal);
        foreach (var groupId in _touchedGroups)
        {
            rosters[groupId] = [.. world.GetMembers(groupId)];
        }

        // Drivers who already ended up in a group, and the driver being evaluated right now, need no re-run.
        var reEvaluate = _reEvaluate
            .Where(id => id != evaluatedUser && world.GetGroup(id) is null)
            .ToArray();

        return new ProximityPlan([.. _changes], transitions, rosters, reEvaluate);
    }

    private void Remember(Guid userId) => _initialGroup.TryAdd(userId, world.GetGroup(userId));

    private void Touch(string groupId)
    {
        if (!_touchedGroups.Contains(groupId, StringComparer.Ordinal))
        {
            _touchedGroups.Add(groupId);
        }
    }
}
