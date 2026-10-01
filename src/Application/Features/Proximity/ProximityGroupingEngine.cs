using Microsoft.Extensions.Options;
using Opdeweg.Application.Options;
using Opdeweg.Domain.ValueObjects;

namespace Opdeweg.Application.Features.Proximity;

/// <summary>
/// Pure, deterministic proximity grouping.
/// <para>
/// Groups are <b>clique-constrained</b>: a driver may join a group only if they are within
/// <see cref="ProximityOptions.JoinDistanceMeters"/> of <i>every</i> member, and a group keeps a
/// member only while every pair stays within <see cref="ProximityOptions.LeaveDistanceMeters"/>.
/// This prevents chaining (A–600m–B–700m–C must never put A and C, 1.3 km apart, in one voice
/// room) and the join/leave gap provides hysteresis against GPS noise at the boundary.
/// </para>
/// <para>
/// The engine only reasons about a local neighbourhood (see <see cref="ProximityWorld"/>), which
/// the caller builds from a spatial index — never by comparing all drivers with each other.
/// </para>
/// </summary>
public sealed class ProximityGroupingEngine
{
    /// <summary>Boundary tolerance (1 µm) so "exactly 1000 m" is not decided by floating-point round-off.</summary>
    private const double BoundaryEpsilon = 1e-6;

    private readonly ProximityOptions _options;
    private readonly IGroupIdGenerator _groupIds;

    public ProximityGroupingEngine(IOptions<ProximityOptions> options, IGroupIdGenerator groupIds)
    {
        _options = options.Value;
        _groupIds = groupIds;
    }

    public ProximityPlan Evaluate(ProximityWorld world, Guid subjectId, DateTimeOffset now)
    {
        var plan = new ProximityPlanBuilder(world);
        PruneDeadMembers(world, now, plan);

        var subject = Live(world, subjectId, now);
        if (subject is null)
        {
            return plan.Build(subjectId);
        }

        if (world.GetGroup(subjectId) is { } currentGroup)
        {
            EnforceIntegrity(world, currentGroup, subjectId, plan);
        }

        var neighbours = FindNeighbours(world, subject, now);

        if (world.GetGroup(subjectId) is null)
        {
            TryJoinOrForm(world, subject, neighbours, plan);
        }

        if (world.GetGroup(subjectId) is not null)
        {
            Grow(world, subject, neighbours, plan);
        }

        return plan.Build(subjectId);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822", Justification = "Instance API kept symmetric with Evaluate.")]
    public ProximityPlan Remove(ProximityWorld world, Guid subjectId, ProximityChangeReason reason)
    {
        var plan = new ProximityPlanBuilder(world);
        if (world.GetGroup(subjectId) is { } groupId)
        {
            plan.Leave(subjectId, reason);
            DissolveIfTooSmall(world, groupId, plan);
        }

        return plan.Build(subjectId);
    }

    /// <summary>Members with no fresh position (expired, signed out, crashed) cannot hold a group together.</summary>
    private void PruneDeadMembers(ProximityWorld world, DateTimeOffset now, ProximityPlanBuilder plan)
    {
        foreach (var groupId in world.GroupIds.ToArray())
        {
            var dead = world.GetMembers(groupId).Where(id => Live(world, id, now) is null).ToArray();
            if (dead.Length == 0)
            {
                continue;
            }

            foreach (var id in dead)
            {
                plan.Leave(id, ProximityChangeReason.Stale);
            }

            DissolveIfTooSmall(world, groupId, plan);
        }
    }

    /// <summary>
    /// Removes members until every pair is within the leave distance. The member with the most
    /// violations goes first (then the most peripheral one), so a single driver who wandered off
    /// leaves rather than the majority they left behind.
    /// </summary>
    private void EnforceIntegrity(ProximityWorld world, string groupId, Guid subjectId, ProximityPlanBuilder plan)
    {
        while (true)
        {
            var members = world.GetMembers(groupId).Select(id => world.GetDriver(id)!).ToArray();
            if (members.Length < 2)
            {
                break;
            }

            var violations = new int[members.Length];
            var spread = new double[members.Length];
            var anyViolation = false;

            for (var i = 0; i < members.Length; i++)
            {
                for (var j = i + 1; j < members.Length; j++)
                {
                    var d = Distance(members[i], members[j]);
                    spread[i] += d;
                    spread[j] += d;
                    if (d > _options.LeaveDistanceMeters + BoundaryEpsilon)
                    {
                        violations[i]++;
                        violations[j]++;
                        anyViolation = true;
                    }
                }
            }

            if (!anyViolation)
            {
                break;
            }

            var leaver = Enumerable.Range(0, members.Length)
                .OrderByDescending(i => violations[i])
                .ThenByDescending(i => spread[i])
                .ThenByDescending(i => members[i].UserId == subjectId)
                .ThenBy(i => members[i].UserId)
                .Select(i => members[i].UserId)
                .First();

            plan.Leave(leaver, ProximityChangeReason.OutOfRange);
            plan.ReEvaluate(leaver);
        }

        DissolveIfTooSmall(world, groupId, plan);
    }

    private void TryJoinOrForm(ProximityWorld world, DriverState subject, IReadOnlyList<Neighbour> neighbours, ProximityPlanBuilder plan)
    {
        if (!CanInitiate(subject))
        {
            return;
        }

        JoinOption? best = null;

        foreach (var groupId in neighbours.Select(n => world.GetGroup(n.Driver.UserId)).OfType<string>().Distinct(StringComparer.Ordinal))
        {
            var members = world.GetMembers(groupId);
            if (members.Count >= _options.MaxGroupSize)
            {
                continue;
            }

            var maxDistance = members.Max(id => Distance(subject, world.GetDriver(id)!));
            if (maxDistance <= _options.JoinDistanceMeters + BoundaryEpsilon)
            {
                best = JoinOption.Better(best, new JoinOption(groupId, null, members.Count, maxDistance));
            }
        }

        foreach (var neighbour in neighbours)
        {
            if (world.GetGroup(neighbour.Driver.UserId) is null && CanInitiate(neighbour.Driver))
            {
                best = JoinOption.Better(best, new JoinOption(null, neighbour.Driver.UserId, 1, neighbour.Distance));
            }
        }

        if (best?.GroupId is { } existingGroup)
        {
            plan.Join(subject.UserId, existingGroup, ProximityChangeReason.Proximity);
        }
        else if (best?.Partner is { } partner)
        {
            var newGroupId = _groupIds.NewGroupId();
            plan.Join(subject.UserId, newGroupId, ProximityChangeReason.Proximity);
            plan.Join(partner, newGroupId, ProximityChangeReason.Proximity);
        }
    }

    /// <summary>Merges neighbouring groups and absorbs ungrouped neighbours when the clique constraint allows.</summary>
    private void Grow(ProximityWorld world, DriverState subject, IReadOnlyList<Neighbour> neighbours, ProximityPlanBuilder plan)
    {
        if (!CanInitiate(subject))
        {
            return;
        }

        var mergedSomething = true;
        while (mergedSomething)
        {
            mergedSomething = false;
            var groupId = world.GetGroup(subject.UserId)!;

            var otherGroups = neighbours
                .Select(n => world.GetGroup(n.Driver.UserId))
                .OfType<string>()
                .Where(g => !string.Equals(g, groupId, StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .OrderByDescending(g => world.GetMembers(g).Count)
                .ThenBy(g => g, StringComparer.Ordinal)
                .ToArray();

            foreach (var otherId in otherGroups)
            {
                var ours = world.GetMembers(groupId);
                var theirs = world.GetMembers(otherId);
                if (ours.Count + theirs.Count > _options.MaxGroupSize || !AllWithin(world, ours, theirs, _options.JoinDistanceMeters + BoundaryEpsilon))
                {
                    continue;
                }

                // The smaller group moves so as few drivers as possible switch voice rooms.
                var oursSurvives = ours.Count > theirs.Count ||
                    (ours.Count == theirs.Count && string.CompareOrdinal(groupId, otherId) < 0);
                var (survivor, moving) = oursSurvives ? (groupId, theirs.ToArray()) : (otherId, ours.ToArray());

                foreach (var member in moving)
                {
                    plan.Leave(member, ProximityChangeReason.Merged);
                    plan.Join(member, survivor, ProximityChangeReason.Merged);
                }

                mergedSomething = true;
                break;
            }
        }

        var finalGroup = world.GetGroup(subject.UserId)!;
        foreach (var neighbour in neighbours)
        {
            var members = world.GetMembers(finalGroup);
            if (members.Count >= _options.MaxGroupSize)
            {
                break;
            }

            if (world.GetGroup(neighbour.Driver.UserId) is null &&
                CanInitiate(neighbour.Driver) &&
                members.All(id => Distance(neighbour.Driver, world.GetDriver(id)!) <= _options.JoinDistanceMeters + BoundaryEpsilon))
            {
                plan.Join(neighbour.Driver.UserId, finalGroup, ProximityChangeReason.Proximity);
            }
        }
    }

    private static void DissolveIfTooSmall(ProximityWorld world, string groupId, ProximityPlanBuilder plan)
    {
        var members = world.GetMembers(groupId);
        if (members.Count != 1)
        {
            return;
        }

        var last = members.First();
        plan.Leave(last, ProximityChangeReason.GroupDissolved);
        plan.ReEvaluate(last);
    }

    private List<Neighbour> FindNeighbours(ProximityWorld world, DriverState subject, DateTimeOffset now) =>
        world.Drivers
            .Where(d => d.UserId != subject.UserId && IsLive(d, now))
            .Select(d => new Neighbour(d, Distance(subject, d)))
            .Where(n => n.Distance <= _options.JoinDistanceMeters + BoundaryEpsilon)
            .OrderBy(n => n.Distance)
            .ThenBy(n => n.Driver.UserId)
            .ToList();

    private static bool AllWithin(ProximityWorld world, IEnumerable<Guid> a, IReadOnlyCollection<Guid> b, double limit) =>
        a.All(x => b.All(y => Distance(world.GetDriver(x)!, world.GetDriver(y)!) <= limit));

    private DriverState? Live(ProximityWorld world, Guid userId, DateTimeOffset now) =>
        world.GetDriver(userId) is { } driver && IsLive(driver, now) ? driver : null;

    private bool IsLive(DriverState driver, DateTimeOffset now) => now - driver.LastSeenAt <= _options.StaleAfter;

    private bool CanInitiate(DriverState driver) =>
        driver.AccuracyMeters is null || driver.AccuracyMeters <= _options.MaxJoinAccuracyMeters;

    private static double Distance(DriverState a, DriverState b) => GeoMath.DistanceMeters(a.Position, b.Position);

    private readonly record struct Neighbour(DriverState Driver, double Distance);

    /// <summary>Either an existing group to join or an ungrouped partner to form a new group with.</summary>
    private sealed record JoinOption(string? GroupId, Guid? Partner, int Size, double MaxDistance)
    {
        /// <summary>Prefer the largest reachable group (so drivers converge), then the tightest fit.</summary>
        public static JoinOption Better(JoinOption? current, JoinOption candidate)
        {
            if (current is null || candidate.Size > current.Size)
            {
                return candidate;
            }

            return candidate.Size == current.Size && candidate.MaxDistance < current.MaxDistance ? candidate : current;
        }
    }
}
