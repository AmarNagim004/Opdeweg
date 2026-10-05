using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Opdeweg.Api.Configuration;
using Opdeweg.Api.Extensions;
using Opdeweg.Application.DTOs;
using Opdeweg.Application.Features.Driving;
using Opdeweg.Application.Features.Location;
using Opdeweg.Domain.Enums;

namespace Opdeweg.Api.Controllers;

[ApiController]
[Route("api/v1/driving")]
public sealed class DrivingController(DrivingSessionService sessions, LocationIngestionService locations) : ControllerBase
{
    [HttpGet("sessions/current")]
    [ProducesResponseType<DrivingSessionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GetCurrent(CancellationToken cancellationToken) =>
        await sessions.GetCurrentAsync(User.GetUserId(), cancellationToken) is { } session ? Ok(session) : NoContent();

    /// <summary>Starts a driving session (idempotent: returns the active one if it exists).</summary>
    [HttpPost("sessions")]
    public Task<DrivingSessionDto> Start(CancellationToken cancellationToken) =>
        sessions.StartAsync(User.GetUserId(), cancellationToken);

    [HttpPost("sessions/current/end")]
    public async Task<IActionResult> End(CancellationToken cancellationToken)
    {
        await sessions.EndAsync(User.GetUserId(), DrivingSessionEndReason.UserEnded, cancellationToken);
        return NoContent();
    }

    /// <summary>Submits a location fix; answers with the up-to-date proximity snapshot.</summary>
    [HttpPost("location")]
    [EnableRateLimiting(RateLimitingSetup.Location)]
    public Task<LocationUpdateResponse> UpdateLocation(LocationUpdateRequest request, CancellationToken cancellationToken) =>
        locations.IngestAsync(User.GetUserId(), request, cancellationToken);
}
