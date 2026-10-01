using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Opdeweg.Application.Features.Proximity;
using Opdeweg.Application.Interfaces;
using Opdeweg.Infrastructure.LiveKit;

namespace Opdeweg.Api.Controllers;

/// <summary>
/// LiveKit webhooks. Every participant join triggers a roster reconciliation, so even someone
/// replaying a still-valid token for a room they were removed from is disconnected immediately.
/// </summary>
[ApiController]
[Route("api/v1/livekit/webhook")]
[AllowAnonymous]
public sealed class LiveKitWebhookController(LiveKitWebhookVerifier verifier, IVoiceRoomReconciler reconciler) : ControllerBase
{
    private const int MaxBodyBytes = 64 * 1024;

    [HttpPost]
    [Consumes("application/webhook+json", "application/json")]
    [RequestSizeLimit(MaxBodyBytes)]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(cancellationToken);
        var webhook = verifier.Verify(Request.Headers.Authorization, body);
        if (webhook is null)
        {
            return Unauthorized();
        }

        if (webhook.Event is "participant_joined" or "track_published" &&
            webhook.Room is { } room &&
            RoomNames.TryGetGroupId(room, out var groupId))
        {
            reconciler.Enqueue(groupId);
        }

        return Ok();
    }
}
