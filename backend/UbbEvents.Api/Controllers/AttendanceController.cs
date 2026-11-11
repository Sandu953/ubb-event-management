using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UbbEvents.Api.DTOs;
using UbbEvents.Api.Services;

namespace UbbEvents.Api.Controllers;

[ApiController, Authorize, Route("api/events/{id:guid}")]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403),
 ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409)]
public sealed class AttendanceController(AttendanceService attendance) : ControllerBase
{
    /// <summary>Check in a confirmed registration while registration is open (organization staff or global ADMIN).</summary>
    [HttpPost("check-in")]
    [ProducesResponseType<RegistrationResponse>(200)]
    public async Task<ActionResult<RegistrationResponse>> CheckIn(Guid id, CheckInRequest request, CancellationToken ct)
        => Ok(await attendance.CheckInAsync(id, request, ct));

    /// <summary>Get registration and attendance totals (organization staff or global ADMIN).</summary>
    [HttpGet("attendance")]
    [ProducesResponseType<AttendanceResponse>(200)]
    public async Task<ActionResult<AttendanceResponse>> Totals(Guid id, CancellationToken ct)
        => Ok(await attendance.TotalsAsync(id, ct));
}
