using Opdeweg.Application.Features.Proximity;
using Opdeweg.Application.Options;
using Opdeweg.Domain.ValueObjects;
using Opdeweg.UnitTests.TestSupport;

namespace Opdeweg.UnitTests.Proximity;

public sealed class ProximityGroupingEngineTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly Guid _alice = Guid.Parse("00000000-0000-0000-0000-00000000000a");
    private readonly Guid _bob = Guid.Parse("00000000-0000-0000-0000-00000000000b");
    private readonly Guid _charlie = Guid.Parse("00000000-0000-0000-0000-00000000000c");
    private readonly Guid _dave = Guid.Parse("00000000-0000-0000-0000-00000000000d");
    private readonly Guid _erin = Guid.Parse("00000000-0000-0000-0000-00000000000e");

    // ---------------------------------------------------------------- thresholds

    [Theory]
    [InlineData(999)]
    [InlineData(1000)]
    public void Ungrouped_drivers_within_join_distance_form_a_group(double meters)
    {
        var world = World((_alice, Geo.Origin), (_bob, Geo.East(meters)));

        var plan = Engine().Evaluate(world, _alice, Now);

        Assert.NotNull(world.GetGroup(_alice));
        Assert.Equal(world.GetGroup(_alice), world.GetGroup(_bob));
        Assert.Equal(2, plan.Transitions.Count);
        Assert.All(plan.Transitions, t => Assert.Equal(ProximityChangeReason.Proximity, t.Reason));
    }

    [Theory]
    [InlineData(1001)]
    [InlineData(1050)]
    [InlineData(1100)]
    public void Ungrouped_drivers_beyond_join_distance_do_not_join(double meters)
    {
        var world = World((_alice, Geo.Origin), (_bob, Geo.East(meters)));

        var plan = Engine().Evaluate(world, _alice, Now);

        Assert.True(plan.IsEmpty);
        Assert.Null(world.GetGroup(_alice));
        Assert.Null(world.GetGroup(_bob));
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(1050)]
    [InlineData(1100)]
    public void Grouped_drivers_remain_up_to_the_leave_distance(double meters)
    {
        var world = World((_alice, Geo.Origin), (_bob, Geo.East(meters)));
        Group(world, "g", _alice, _bob);

        var plan = Engine().Evaluate(world, _alice, Now);

        Assert.True(plan.IsEmpty);
        Assert.Equal("g", world.GetGroup(_alice));
        Assert.Equal("g", world.GetGroup(_bob));
    }

    [Theory]
    [InlineData(1101)]
    [InlineData(1500)]
    public void Grouped_drivers_beyond_the_leave_distance_leave(double meters)
    {
        var world = World((_alice, Geo.Origin), (_bob, Geo.East(meters)));
        Group(world, "g", _alice, _bob);

        var plan = Engine().Evaluate(world, _alice, Now);

        Assert.Null(world.GetGroup(_alice));
        Assert.Null(world.GetGroup(_bob));
        Assert.Empty(plan.Rosters["g"]);
        Assert.Contains(plan.Transitions, t => t.UserId == _alice && t.Reason == ProximityChangeReason.OutOfRange);
        Assert.Contains(plan.Transitions, t => t.UserId == _bob && t.Reason == ProximityChangeReason.GroupDissolved);
    }

    // ---------------------------------------------------------------- hysteresis

    [Fact]
    public void Gps_noise_around_the_join_boundary_does_not_cause_flapping()
    {
        var engine = Engine();
        var world = World((_alice, Geo.Origin), (_bob, Geo.East(995)));
        engine.Evaluate(world, _bob, Now);
        var group = world.GetGroup(_bob);
        Assert.NotNull(group);

        // Jitter well past the 1000 m join line, but inside the 1100 m leave line.
        foreach (var meters in new[] { 1040.0, 990, 1075, 1010, 1099, 1000, 1060, 1098 })
        {
            Move(world, _bob, Geo.East(meters));
            var plan = engine.Evaluate(world, _bob, Now);
            Assert.True(plan.IsEmpty, $"membership changed at {meters} m");
            Assert.Equal(group, world.GetGroup(_bob));
        }
    }

    [Fact]
    public void After_leaving_drivers_only_rejoin_once_back_within_the_join_distance()
    {
        var engine = Engine();
        var world = World((_alice, Geo.Origin), (_bob, Geo.East(1150)));
        Group(world, "g", _alice, _bob);
        engine.Evaluate(world, _bob, Now);
        Assert.Null(world.GetGroup(_bob));

        foreach (var meters in new[] { 1099.0, 1050, 1001, 1080, 1020 })
        {
            Move(world, _bob, Geo.East(meters));
            Assert.True(engine.Evaluate(world, _bob, Now).IsEmpty, $"re-joined at {meters} m");
        }

        Move(world, _bob, Geo.East(1000));
        engine.Evaluate(world, _bob, Now);
        Assert.NotNull(world.GetGroup(_bob));
        Assert.Equal(world.GetGroup(_alice), world.GetGroup(_bob));
    }

    // ---------------------------------------------------------------- spatial groups

    [Fact]
    public void Chained_drivers_are_never_connected_beyond_the_radius()
    {
        // Alice ---- 600 m ---- Bob ---- 700 m ---- Charlie  (Alice ↔ Charlie ≈ 1300 m)
        var world = World((_alice, Geo.Origin), (_bob, Geo.East(600)), (_charlie, Geo.East(1300)));
        var engine = Engine();

        engine.Evaluate(world, _alice, Now);
        engine.Evaluate(world, _charlie, Now);
        engine.Evaluate(world, _bob, Now);

        Assert.Equal(world.GetGroup(_alice), world.GetGroup(_bob));
        Assert.NotEqual(world.GetGroup(_alice), world.GetGroup(_charlie));
        AssertEveryGroupIsWithin(world, 1100);
    }

    [Fact]
    public void Joining_requires_the_join_distance_to_every_member()
    {
        var world = World((_alice, Geo.Origin), (_bob, Geo.East(800)), (_charlie, Geo.North(900)));
        Group(world, "g", _alice, _bob);

        // Charlie is 900 m from Alice but ~1.2 km from Bob.
        Engine().Evaluate(world, _charlie, Now);

        Assert.Null(world.GetGroup(_charlie));
    }

    [Fact]
    public void A_driver_joins_an_existing_group_when_within_range_of_everyone()
    {
        var world = World((_alice, Geo.Origin), (_bob, Geo.East(300)), (_charlie, Geo.North(400)));
        Group(world, "g", _alice, _bob);

        var plan = Engine().Evaluate(world, _charlie, Now);

        Assert.Equal("g", world.GetGroup(_charlie));
        Assert.Equal(3, plan.Rosters["g"].Count);
        Assert.Contains(plan.Transitions, t => t.UserId == _charlie && t.ToGroupId == "g");
    }

    [Fact]
    public void The_driver_who_wanders_off_leaves_while_the_majority_stays()
    {
        // Charlie and Dave drive off together; Alice, Bob and Erin stay put.
        var world = World(
            (_alice, Geo.Origin),
            (_bob, Geo.East(100)),
            (_erin, Geo.North(100)),
            (_charlie, Geo.East(1500)),
            (_dave, Geo.East(1550)));
        Group(world, "g", _alice, _bob, _erin, _charlie, _dave);

        // Alice (who did not move) is evaluated first; she must not be the one kicked out.
        var plan = Engine().Evaluate(world, _alice, Now);

        Assert.Equal("g", world.GetGroup(_alice));
        Assert.Equal("g", world.GetGroup(_bob));
        Assert.Equal("g", world.GetGroup(_erin));
        Assert.NotEqual("g", world.GetGroup(_charlie));
        Assert.NotEqual("g", world.GetGroup(_dave));
        Assert.Contains(_charlie, plan.ReEvaluate);
        AssertEveryGroupIsWithin(world, 1100);
    }

    [Fact]
    public void Nearby_groups_merge_when_everyone_is_in_range()
    {
        var world = World(
            (_alice, Geo.Origin),
            (_bob, Geo.East(200)),
            (_charlie, Geo.North(300)),
            (_dave, Geo.From(Geo.North(300), 90, 200)));
        Group(world, "g1", _alice, _bob);
        Group(world, "g2", _charlie, _dave);

        var plan = Engine().Evaluate(world, _alice, Now);

        var group = world.GetGroup(_alice);
        Assert.All(new[] { _bob, _charlie, _dave }, id => Assert.Equal(group, world.GetGroup(id)));
        Assert.Contains(plan.Transitions, t => t.Reason == ProximityChangeReason.Merged);
        Assert.Single(plan.Rosters.Values, r => r.Count == 0);
    }

    [Fact]
    public void Groups_do_not_merge_when_some_cross_pair_is_out_of_range()
    {
        var world = World(
            (_alice, Geo.Origin),
            (_bob, Geo.East(900)),
            (_charlie, Geo.From(Geo.Origin, 270, 500)),
            (_dave, Geo.From(Geo.Origin, 270, 700)));
        Group(world, "g1", _alice, _bob);
        Group(world, "g2", _charlie, _dave);

        Engine().Evaluate(world, _alice, Now);

        Assert.Equal("g1", world.GetGroup(_alice));
        Assert.Equal("g2", world.GetGroup(_charlie));
    }

    [Fact]
    public void Group_size_is_capped()
    {
        var world = World((_alice, Geo.Origin), (_bob, Geo.East(50)), (_charlie, Geo.East(100)), (_dave, Geo.East(150)));
        Group(world, "g", _alice, _bob, _charlie);

        Engine(o => o.MaxGroupSize = 3).Evaluate(world, _dave, Now);

        Assert.Null(world.GetGroup(_dave));
        Assert.Equal(3, world.GetMembers("g").Count);
    }

    [Fact]
    public void An_ungrouped_driver_prefers_the_larger_reachable_group()
    {
        var world = World(
            (_alice, Geo.Origin),
            (_bob, Geo.East(150)),
            (_charlie, Geo.North(150)),
            (_dave, Geo.East(400)),
            (_erin, Geo.East(250)));
        Group(world, "small", _dave, _bob);
        world.AddMembership(_charlie, "big");
        world.AddMembership(_alice, "big");

        // Make "big" larger by adding a third close member.
        var frank = Guid.NewGuid();
        world.AddDriver(new DriverState(frank, Geo.North(50), 5, Now));
        world.AddMembership(frank, "big");

        Engine().Evaluate(world, _erin, Now);

        Assert.Equal("big", world.GetGroup(_erin));
    }

    // ---------------------------------------------------------------- liveness & accuracy

    [Fact]
    public void Stale_members_are_pruned_and_small_groups_dissolve()
    {
        var world = new ProximityWorld();
        world.AddDriver(new DriverState(_alice, Geo.Origin, 5, Now));
        world.AddDriver(new DriverState(_bob, Geo.East(100), 5, Now - TimeSpan.FromMinutes(5)));
        Group(world, "g", _alice, _bob);

        var plan = Engine().Evaluate(world, _alice, Now);

        Assert.Null(world.GetGroup(_alice));
        Assert.Null(world.GetGroup(_bob));
        Assert.Contains(plan.Transitions, t => t.UserId == _bob && t.Reason == ProximityChangeReason.Stale);
    }

    [Fact]
    public void Members_whose_presence_vanished_are_pruned()
    {
        var world = new ProximityWorld();
        world.AddDriver(new DriverState(_alice, Geo.Origin, 5, Now));
        world.AddDriver(new DriverState(_bob, Geo.East(100), 5, Now));
        Group(world, "g", _alice, _bob, _charlie); // Charlie has no position at all.

        Engine().Evaluate(world, _alice, Now);

        Assert.Equal("g", world.GetGroup(_alice));
        Assert.Null(world.GetGroup(_charlie));
        Assert.Equal(2, world.GetMembers("g").Count);
    }

    [Fact]
    public void Stale_candidates_are_ignored()
    {
        var world = new ProximityWorld();
        world.AddDriver(new DriverState(_alice, Geo.Origin, 5, Now));
        world.AddDriver(new DriverState(_bob, Geo.East(100), 5, Now - TimeSpan.FromMinutes(2)));

        Assert.True(Engine().Evaluate(world, _alice, Now).IsEmpty);
    }

    [Fact]
    public void Poor_accuracy_cannot_create_groups_but_keeps_existing_ones()
    {
        var engine = Engine();
        var world = new ProximityWorld();
        world.AddDriver(new DriverState(_alice, Geo.Origin, 120, Now));
        world.AddDriver(new DriverState(_bob, Geo.East(300), 5, Now));

        Assert.True(engine.Evaluate(world, _alice, Now).IsEmpty);
        Assert.True(engine.Evaluate(world, _bob, Now).IsEmpty);

        Group(world, "g", _alice, _bob);
        Assert.True(engine.Evaluate(world, _alice, Now).IsEmpty);
        Assert.Equal("g", world.GetGroup(_alice));
    }

    // ---------------------------------------------------------------- removal

    [Fact]
    public void Removing_a_driver_from_a_pair_dissolves_the_group()
    {
        var world = World((_alice, Geo.Origin), (_bob, Geo.East(100)));
        Group(world, "g", _alice, _bob);

        var plan = Engine().Remove(world, _alice, ProximityChangeReason.SessionEnded);

        Assert.Null(world.GetGroup(_bob));
        Assert.Contains(plan.Transitions, t => t.UserId == _alice && t.Reason == ProximityChangeReason.SessionEnded);
        Assert.Contains(plan.Transitions, t => t.UserId == _bob && t.Reason == ProximityChangeReason.GroupDissolved);
        Assert.Contains(_bob, plan.ReEvaluate);
    }

    [Fact]
    public void Removing_a_driver_keeps_the_rest_of_a_larger_group()
    {
        var world = World((_alice, Geo.Origin), (_bob, Geo.East(100)), (_charlie, Geo.North(100)));
        Group(world, "g", _alice, _bob, _charlie);

        var plan = Engine().Remove(world, _alice, ProximityChangeReason.Stale);

        Assert.Equal("g", world.GetGroup(_bob));
        Assert.Equal("g", world.GetGroup(_charlie));
        Assert.Single(plan.Transitions);
    }

    // ---------------------------------------------------------------- helpers

    private static ProximityGroupingEngine Engine(Action<ProximityOptions>? configure = null) =>
        new(TestOptions.Proximity(configure), new SequentialGroupIds());

    private static ProximityWorld World(params (Guid Id, GeoPoint Position)[] drivers)
    {
        var world = new ProximityWorld();
        foreach (var (id, position) in drivers)
        {
            world.AddDriver(new DriverState(id, position, 5, Now));
        }

        return world;
    }

    private static void Group(ProximityWorld world, string groupId, params Guid[] members)
    {
        foreach (var member in members)
        {
            world.AddMembership(member, groupId);
        }
    }

    private static void Move(ProximityWorld world, Guid id, GeoPoint position) =>
        world.AddDriver(new DriverState(id, position, 5, Now));

    private static void AssertEveryGroupIsWithin(ProximityWorld world, double meters)
    {
        foreach (var groupId in world.GroupIds)
        {
            var members = world.GetMembers(groupId).Select(id => world.GetDriver(id)!).ToArray();
            foreach (var a in members)
            {
                foreach (var b in members)
                {
                    Assert.True(GeoMath.DistanceMeters(a.Position, b.Position) <= meters, $"group {groupId} spans more than {meters} m");
                }
            }
        }
    }
}
