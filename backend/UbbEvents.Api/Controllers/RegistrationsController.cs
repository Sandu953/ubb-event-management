using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UbbEvents.Api.DTOs;
using UbbEvents.Api.Services;

namespace UbbEvents.Api.Controllers;

[ApiController, Authorize, Route("api")]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401),
 ProducesResponseType<ProblemDetails>(403), ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409)]
public sealed class RegistrationsController(RegistrationService registrations) : ControllerBase
{
    /// <summary>Register the authenticated user; returns CONFIRMED or WAITLISTED. No user ID is accepted.</summary>
    [HttpPost("events/{id:guid}/registrations")]
    [ProducesResponseType<RegistrationResponse>(200)]
    public async Task<ActionResult<RegistrationResponse>> Register(Guid id, CancellationToken ct)
        => Ok(await registrations.RegisterAsync(id, ct));

    /// <summary>List event participants with optional status filter (organization staff or global ADMIN).</summary>
    [HttpGet("events/{id:guid}/registrations")]
    [ProducesResponseType<PagedResponse<ParticipantResponse>>(200)]
    public async Task<ActionResult<PagedResponse<ParticipantResponse>>> Participants(Guid id, [FromQuery] RegistrationQuery query, CancellationToken ct)
        => Ok(await registrations.ParticipantsAsync(id, query, ct));

    /// <summary>List the current user's registration history, including cancellations.</summary>
    [HttpGet("registrations/me")]
    [ProducesResponseType<PagedResponse<MyRegistrationResponse>>(200)]
    public async Task<ActionResult<PagedResponse<MyRegistrationResponse>>> Mine([FromQuery] RegistrationQuery query, CancellationToken ct)
        => Ok(await registrations.MineAsync(query, ct));

    /// <summary>Cancel own registration and atomically promote the oldest waitlisted participant when eligible.</summary>
    [HttpDelete("events/{id:guid}/registrations/me")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        await registrations.CancelAsync(id, ct);
        return NoContent();
    }
}
