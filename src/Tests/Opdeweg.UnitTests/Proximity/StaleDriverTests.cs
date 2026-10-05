using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Opdeweg.Application.Features.Proximity;
using Opdeweg.Infrastructure.Services;
using Opdeweg.UnitTests.TestSupport;

namespace Opdeweg.UnitTests.Proximity;

public sealed class StaleDriverTests
{
    [Fact]
    public async Task Drivers_without_updates_are_eventually_removed_from_proximity()
    {
        var h = new ProximityHarness();
        var alice = await h.StartDriverAsync("Alice");
        var bob = await h.StartDriverAsync("Bob");
        await h.MoveAsync(alice, TestSupport.Geo.Origin);
        await h.MoveAsync(bob, TestSupport.Geo.East(300));
        Assert.NotNull(h.GroupOf(bob));

        // Alice keeps reporting; Bob goes silent (tunnel, dead battery, ...).
        var sweeper = Sweeper(h);
        for (var i = 0; i < 4; i++)
        {
            h.Time.Advance(TimeSpan.FromSeconds(15));
            await h.MoveAsync(alice, TestSupport.Geo.North(i));
            await sweeper.SweepAsync(CancellationToken.None);
        }

        Assert.Empty(h.Queue.Commands); // 60 s is still within the stale timeout

        h.Time.Advance(TimeSpan.FromSeconds(15));
        await h.MoveAsync(alice, TestSupport.Geo.North(5));
        await sweeper.SweepAsync(CancellationToken.None);

        var removal = Assert.IsType<RemoveDriverCommand>(Assert.Single(h.Queue.Commands));
        Assert.Equal(bob, removal.UserId);
        Assert.Equal(ProximityChangeReason.Stale, removal.Reason);
        Assert.False(removal.EndSession);

        await h.RunAsync(removal);

        Assert.DoesNotContain(bob, h.Store.Indexed);
        Assert.Null(h.GroupOf(alice));
        Assert.Null(h.GroupOf(bob));
        Assert.True(h.Store.Sessions.ContainsKey(bob)); // the driving session survives a short outage
        Assert.DoesNotContain(bob, await h.Store.FindNearbyAsync(TestSupport.Geo.Origin, 5000, 10, CancellationToken.None));
    }

    [Fact]
    public async Task A_returning_driver_rejoins_after_being_swept()
    {
        var h = new ProximityHarness();
        var alice = await h.StartDriverAsync("Alice");
        var bob = await h.StartDriverAsync("Bob");
        await h.MoveAsync(alice, TestSupport.Geo.Origin);
        await h.MoveAsync(bob, TestSupport.Geo.East(300));
        h.Time.Advance(TimeSpan.FromSeconds(90));
        await h.MoveAsync(alice, TestSupport.Geo.Origin);
        await Sweeper(h).SweepAsync(CancellationToken.None);
        foreach (var command in h.Queue.Commands.ToArray())
        {
            await h.RunAsync(command);
        }

        Assert.Null(h.GroupOf(bob));

        await h.MoveAsync(bob, TestSupport.Geo.East(320));

        Assert.NotNull(h.GroupOf(bob));
        Assert.Equal(h.GroupOf(alice), h.GroupOf(bob));
    }

    private static PresenceSweeper Sweeper(ProximityHarness h) => new(
        h.Store,
        h.Queue,
        new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
        TestOptions.Proximity(),
        TestOptions.Driving(),
        h.Time,
        NullLogger<PresenceSweeper>.Instance);
}
