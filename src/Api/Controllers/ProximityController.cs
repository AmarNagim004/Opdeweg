using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Opdeweg.Api.Extensions;
using Opdeweg.Application.Common;
using Opdeweg.Application.DTOs;
using Opdeweg.Application.Features.Proximity;
using Opdeweg.Application.Features.Voice;
using Opdeweg.Application.Options;

namespace Opdeweg.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class ProximityController(ProximityQueryService query, VoiceAccessService voice) : ControllerBase
{
    [HttpGet("proximity")]
    public Task<ProximitySnapshotDto> GetSnapshot([FromQuery] bool includeVoice, CancellationToken cancellationToken) =>
        query.GetSnapshotAsync(User.GetUserId(), includeVoice, cancellationToken);

    /// <summary>Issues a voice token for the caller's current proximity group only.</summary>
    [HttpPost("voice/token")]
    public async Task<VoiceAccessDto> IssueVoiceToken(CancellationToken cancellationToken) =>
        await voice.IssueForCurrentGroupAsync(User.GetUserId(), cancellationToken)
        ?? throw AppException.Conflict("not_in_group", "You are not in a proximity group right now.");

    /// <summary>Server-driven client tuning, so thresholds can change without an app release.</summary>
    [HttpGet("config")]
    [AllowAnonymous]
    public ClientConfigDto GetConfig([FromServices] IOptions<ProximityOptions> proximity, [FromServices] IOptions<LocationOptions> location)
    {
        var p = proximity.Value;
        var l = location.Value;
        return new ClientConfigDto(
            new ProximityConfigDto(p.JoinDistanceMeters, p.LeaveDistanceMeters, p.MaxGroupSize),
            new LocationConfigDto(l.ClientDistanceThresholdMeters, l.ClientMaxIntervalSeconds, l.ClientMinIntervalMilliseconds, l.ClientHeadingChangeDegrees, l.ClientSpeedChangeMetersPerSecond));
    }
}
