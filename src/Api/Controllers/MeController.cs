using Microsoft.AspNetCore.Mvc;
using Opdeweg.Api.Extensions;
using Opdeweg.Application.DTOs;
using Opdeweg.Application.Features.Profile;

namespace Opdeweg.Api.Controllers;

[ApiController]
[Route("api/v1/me")]
public sealed class MeController(ProfileService profiles) : ControllerBase
{
    [HttpGet]
    public Task<MeDto> Get(CancellationToken cancellationToken) => profiles.GetAsync(User.GetUserId(), cancellationToken);

    [HttpPatch]
    public Task<MeDto> Update(UpdateProfileRequest request, CancellationToken cancellationToken) =>
        profiles.UpdateAsync(User.GetUserId(), request, cancellationToken);

    /// <summary>Permanently deletes the account and all associated data.</summary>
    [HttpDelete]
    public async Task<IActionResult> Delete(CancellationToken cancellationToken)
    {
        await profiles.DeleteAsync(User.GetUserId(), cancellationToken);
        return NoContent();
    }
}
