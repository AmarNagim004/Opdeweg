using Opdeweg.Application.Features.Proximity;
using Opdeweg.Domain.Enums;
using Opdeweg.UnitTests.Proximity;
using Opdeweg.UnitTests.TestSupport;

namespace Opdeweg.UnitTests.Voice;

/// <summary>Voice access follows server-side proximity membership — never the client's opinion.</summary>
public sealed class VoiceAuthorizationTests
{
    [Fact]
    public async Task Proximity_joined_authorizes_voice_for_that_room_only()
    {
        var h = new ProximityHarness();
        var alice = await h.StartDriverAsync("Alice");
        var bob = await h.StartDriverAsync("Bob");
        var charlie = await h.StartDriverAsync("Charlie");

        await h.MoveAsync(alice, TestSupport.Geo.Origin);
        await h.MoveAsync(charlie, TestSupport.Geo.East(5000));
        await h.MoveAsync(bob, TestSupport.Geo.East(400));

        var group = h.GroupOf(alice);
        Assert.NotNull(group);
        Assert.Equal(group, h.GroupOf(bob));

        // Both joiners were pushed a token for exactly their group's room, bound to their session handle.
        var room = RoomNames.ForGroup(group!);
        foreach (var (user, other) in new[] { (alice, bob), (bob, alice) })
        {
            var joined = Assert.Single(h.Realtime.Joined, e => e.UserId == user);
            Assert.Equal(room, joined.Group.Voice!.Room);
            Assert.Equal(h.HandleOf(user), joined.Group.Voice.Identity);
            var member = Assert.Single(joined.Group.Members);
            Assert.Equal(h.HandleOf(other), member.Id);
            Assert.Equal(400, member.ApproxDistanceMeters);
        }

        // On-demand token requests are authorised by the same server-side membership.
        Assert.Equal(room, (await h.Voice.IssueForCurrentGroupAsync(alice, CancellationToken.None))?.Room);
        Assert.Null(await h.Voice.IssueForCurrentGroupAsync(charlie, CancellationToken.None));
        Assert.DoesNotContain(h.TokenIssuer.Issued, g => g.Identity == h.HandleOf(charlie));
    }

    [Fact]
    public async Task Proximity_left_removes_voice_access_and_reconciles_the_room()
    {
        var h = new ProximityHarness();
        var alice = await h.StartDriverAsync("Alice");
        var bob = await h.StartDriverAsync("Bob");
        await h.MoveAsync(alice, TestSupport.Geo.Origin);
        await h.MoveAsync(bob, TestSupport.Geo.East(300));
        var group = h.GroupOf(alice)!;
        h.Rooms.Enqueued.Clear();

        h.Time.Advance(TimeSpan.FromSeconds(30));
        await h.MoveAsync(bob, TestSupport.Geo.East(1200));

        Assert.Null(h.GroupOf(alice));
        Assert.Null(h.GroupOf(bob));
        Assert.Contains(h.Realtime.Left, e => e.UserId == bob && e.Left.GroupId == group && e.Left.Reason == ProximityChangeReason.OutOfRange);
        Assert.Contains(h.Realtime.Left, e => e.UserId == alice && e.Left.GroupId == group);
        Assert.Contains(group, h.Rooms.Enqueued); // SFU room reconciled → participants evicted / room closed
        Assert.Null(await h.Voice.IssueForCurrentGroupAsync(alice, CancellationToken.None));
        Assert.Null(await h.Voice.IssueForCurrentGroupAsync(bob, CancellationToken.None));
    }

    [Fact]
    public async Task Session_ended_disconnects_voice()
    {
        var h = new ProximityHarness();
        var alice = await h.StartDriverAsync("Alice");
        var bob = await h.StartDriverAsync("Bob");
        var charlie = await h.StartDriverAsync("Charlie");
        await h.MoveAsync(alice, TestSupport.Geo.Origin);
        await h.MoveAsync(bob, TestSupport.Geo.East(200));
        await h.MoveAsync(charlie, TestSupport.Geo.North(200));
        var group = h.GroupOf(alice)!;
        Assert.Equal(group, h.GroupOf(charlie));

        await h.DrivingSessions().EndAsync(alice, DrivingSessionEndReason.UserEnded, CancellationToken.None);

        Assert.Contains(h.Realtime.Left, e => e.UserId == alice && e.Left.Reason == ProximityChangeReason.SessionEnded);
        Assert.Contains(h.Realtime.SessionsEnded, e => e.UserId == alice);
        Assert.Contains(group, h.Rooms.Enqueued);
        Assert.False(h.Store.Sessions.ContainsKey(alice));
        Assert.DoesNotContain(alice, h.Store.Indexed);
        Assert.Null(await h.Voice.IssueForCurrentGroupAsync(alice, CancellationToken.None));

        // The remaining members keep talking and are told the roster changed.
        Assert.Equal(group, h.GroupOf(bob));
        Assert.Equal(group, h.GroupOf(charlie));
        Assert.Contains(h.Realtime.RosterChanged, e => e.UserId == bob && e.Group.Members.Count == 1);
    }

    [Fact]
    public async Task Rejoining_after_driving_apart_and_back_issues_a_fresh_token()
    {
        var h = new ProximityHarness();
        var alice = await h.StartDriverAsync("Alice");
        var bob = await h.StartDriverAsync("Bob");
        await h.MoveAsync(alice, TestSupport.Geo.Origin);
        await h.MoveAsync(bob, TestSupport.Geo.East(500));
        h.Time.Advance(TimeSpan.FromSeconds(20));
        await h.MoveAsync(bob, TestSupport.Geo.East(1500));
        Assert.Null(h.GroupOf(bob));

        h.Time.Advance(TimeSpan.FromSeconds(20));
        await h.MoveAsync(bob, TestSupport.Geo.East(700));

        Assert.NotNull(h.GroupOf(bob));
        Assert.Equal(2, h.Realtime.Joined.Count(e => e.UserId == bob));
    }
}
