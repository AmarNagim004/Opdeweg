using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Opdeweg.Application.DTOs;
using Opdeweg.Application.Features.Proximity;
using Opdeweg.Domain.ValueObjects;

namespace Opdeweg.IntegrationTests;

public sealed class ProximityFlowTests(ApiFactory factory)
{
    // Each test uses its own far-apart area so tests never see each other's drivers.
    private static GeoPoint Area(double latitude) => new(latitude, 5.12);

    [Fact]
    public async Task Nearby_drivers_are_grouped_and_receive_voice_access_in_realtime()
    {
        factory.SkipIfUnavailable();
        var origin = Area(51.0);
        var alice = await factory.RegisterAsync("Alice");
        var bob = await factory.RegisterAsync("Bob");
        var charlie = await factory.RegisterAsync("Charlie");
        await alice.StartDrivingAsync();
        await bob.StartDrivingAsync();
        await charlie.StartDrivingAsync();

        await using var bobHub = factory.CreateHubConnection(bob.Auth.AccessToken);
        var joined = new TaskCompletionSource<ProximityGroupDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var left = new TaskCompletionSource<ProximityGroupLeftDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        bobHub.On<ProximityGroupDto>("ProximityGroupJoined", g => joined.TrySetResult(g));
        bobHub.On<ProximityGroupLeftDto>("ProximityGroupLeft", l => left.TrySetResult(l));
        await bobHub.StartAsync(TestContext.Current.CancellationToken);

        // Alice ---- 600 m ---- Bob ---- 700 m ---- Charlie
        await alice.MoveAsync(origin);
        await charlie.MoveAsync(GeoMath.Destination(origin, 90, 1300));
        var bobResult = await bob.MoveAsync(GeoMath.Destination(origin, 90, 600));

        Assert.Equal(LocationUpdateStatus.Accepted, bobResult.Status);
        var group = bobResult.Proximity!.Group!;
        Assert.Equal("Alice", Assert.Single(group.Members).DisplayName);
        Assert.Equal("Charlie", Assert.Single(bobResult.Proximity.Nearby).DisplayName); // nearby, but not in Bob's voice group

        var pushed = await joined.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(group.GroupId, pushed.GroupId);
        Assert.Equal(RoomNames.ForGroup(group.GroupId), pushed.Voice!.Room);
        Assert.False(string.IsNullOrEmpty(pushed.Voice.Token));

        // Coordinates never leak to other drivers.
        var raw = await bob.Http.GetStringAsync("/api/v1/proximity", TestContext.Current.CancellationToken);
        Assert.DoesNotContain("latitude", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(alice.Auth.User.Id.ToString(), raw, StringComparison.OrdinalIgnoreCase);

        // Charlie is out of Alice's range, so the server refuses him voice access.
        var charlieToken = await charlie.Http.PostAsync("/api/v1/voice/token", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, charlieToken.StatusCode);

        // The hub's resync returns the same group plus a fresh token.
        var snapshot = await bobHub.InvokeAsync<ProximitySnapshotDto>("GetSnapshot", TestContext.Current.CancellationToken);
        Assert.Equal(group.GroupId, snapshot.Group!.GroupId);
        Assert.NotNull(snapshot.Group.Voice);

        // Alice ends her drive → Bob's pair group dissolves → Bob is regrouped with Charlie (700 m).
        (await alice.Http.PostAsync("/api/v1/driving/sessions/current/end", null, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();
        var leftEvent = await left.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(group.GroupId, leftEvent.GroupId);

        var after = await bob.Http.GetFromJsonAsync<ProximitySnapshotDto>("/api/v1/proximity", ApiFactory.Json, TestContext.Current.CancellationToken);
        Assert.Equal("Charlie", Assert.Single(after!.Group!.Members).DisplayName);
        Assert.Contains(RoomNames.ForGroup(group.GroupId), factory.RoomAdmin.Deleted);
    }

    [Fact]
    public async Task Location_updates_are_validated_server_side()
    {
        factory.SkipIfUnavailable();
        var origin = Area(48.0);
        var driver = await factory.RegisterAsync("Robin");

        var noSession = await driver.PostLocationAsync(origin.Latitude, origin.Longitude);
        Assert.Equal(HttpStatusCode.Conflict, noSession.StatusCode);
        Assert.Contains("no_active_session", await noSession.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);

        await driver.StartDrivingAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await driver.PostLocationAsync(123, 4)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await driver.PostLocationAsync(origin.Latitude, origin.Longitude, timestamp: DateTimeOffset.UtcNow.AddMinutes(-10))).StatusCode);

        (await driver.PostLocationAsync(origin.Latitude, origin.Longitude)).EnsureSuccessStatusCode();

        var throttled = await driver.Http.PostAsJsonAsync("/api/v1/driving/location", new { latitude = origin.Latitude, longitude = origin.Longitude, timestamp = DateTimeOffset.UtcNow }, ApiFactory.Json, TestContext.Current.CancellationToken);
        Assert.Equal(LocationUpdateStatus.Throttled, (await throttled.Content.ReadFromJsonAsync<LocationUpdateResponse>(ApiFactory.Json, TestContext.Current.CancellationToken))!.Status);

        await Task.Delay(1100, TestContext.Current.CancellationToken);
        var teleport = GeoMath.Destination(origin, 0, 80_000);
        var rejected = await driver.PostLocationAsync(teleport.Latitude, teleport.Longitude);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, rejected.StatusCode);
        Assert.Contains("implausible_movement", await rejected.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Starting_a_session_is_idempotent_and_ending_it_stops_tracking()
    {
        factory.SkipIfUnavailable();
        var origin = Area(45.0);
        var driver = await factory.RegisterAsync("Kim");

        var first = await (await driver.Http.PostAsync("/api/v1/driving/sessions", null, TestContext.Current.CancellationToken)).Content.ReadFromJsonAsync<DrivingSessionDto>(ApiFactory.Json, TestContext.Current.CancellationToken);
        var second = await (await driver.Http.PostAsync("/api/v1/driving/sessions", null, TestContext.Current.CancellationToken)).Content.ReadFromJsonAsync<DrivingSessionDto>(ApiFactory.Json, TestContext.Current.CancellationToken);
        Assert.Equal(first!.Id, second!.Id);

        await driver.MoveAsync(origin);
        (await driver.Http.PostAsync("/api/v1/driving/sessions/current/end", null, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();

        var current = await driver.Http.GetAsync("/api/v1/driving/sessions/current", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, current.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await driver.PostLocationAsync(origin.Latitude, origin.Longitude)).StatusCode);

        var snapshot = await driver.Http.GetFromJsonAsync<ProximitySnapshotDto>("/api/v1/proximity", ApiFactory.Json, TestContext.Current.CancellationToken);
        Assert.False(snapshot!.SessionActive);
    }

    [Fact]
    public async Task Deleting_the_account_removes_the_driver_everywhere()
    {
        factory.SkipIfUnavailable();
        var origin = Area(43.0);
        var gone = await factory.RegisterAsync("Gone");
        var stays = await factory.RegisterAsync("Stays");
        await gone.StartDrivingAsync();
        await stays.StartDrivingAsync();
        await gone.MoveAsync(origin);
        var grouped = await stays.MoveAsync(GeoMath.Destination(origin, 180, 200));
        Assert.NotNull(grouped.Proximity!.Group);

        (await gone.Http.DeleteAsync("/api/v1/me", TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();

        var snapshot = await stays.Http.GetFromJsonAsync<ProximitySnapshotDto>("/api/v1/proximity", ApiFactory.Json, TestContext.Current.CancellationToken);
        Assert.Null(snapshot!.Group);
        Assert.Empty(snapshot.Nearby);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { email = gone.Email, password = "correct horse battery" }, TestContext.Current.CancellationToken)).StatusCode);
    }
}
