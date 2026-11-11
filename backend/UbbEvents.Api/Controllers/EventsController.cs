using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UbbEvents.Api.DTOs;
using UbbEvents.Api.Services;

namespace UbbEvents.Api.Controllers;

[ApiController, Route("api/events")]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409)]
public sealed class EventsController(EventService events) : ControllerBase
{
    /// <summary>Search public events. IncludeDrafts requires authentication and only exposes authorized drafts.</summary>
    [HttpGet, AllowAnonymous]
    [ProducesResponseType<PagedResponse<EventResponse>>(200)]
    public async Task<ActionResult<PagedResponse<EventResponse>>> List([FromQuery] EventQuery query, CancellationToken ct)
        => Ok(await events.ListAsync(query, ct));

    /// <summary>Retrieve a public event; only its staff or global ADMIN can retrieve a draft.</summary>
    [HttpGet("{id:guid}"), AllowAnonymous]
    [ProducesResponseType<EventResponse>(200)]
    public async Task<ActionResult<EventResponse>> Get(Guid id, CancellationToken ct) => Ok(await events.GetAsync(id, ct));

    /// <summary>Create a draft event (organization staff or global ADMIN).</summary>
    [HttpPost, Authorize]
    [ProducesResponseType<EventResponse>(201), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
    public async Task<ActionResult<EventResponse>> Create(CreateEventRequest request, CancellationToken ct)
    {
        var result = await events.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    /// <summary>Update event details using the current Version (organization staff or global ADMIN).</summary>
    [HttpPut("{id:guid}"), Authorize]
    [ProducesResponseType<EventResponse>(200)]
    public async Task<ActionResult<EventResponse>> Update(Guid id, UpdateEventRequest request, CancellationToken ct)
        => Ok(await events.UpdateAsync(id, request, ct));

    /// <summary>Apply an allowed lifecycle transition using the current Version (organization staff or global ADMIN).</summary>
    [HttpPatch("{id:guid}/status"), Authorize]
    [ProducesResponseType<EventResponse>(200)]
    public async Task<ActionResult<EventResponse>> ChangeStatus(Guid id, ChangeEventStatusRequest request, CancellationToken ct)
        => Ok(await events.ChangeStatusAsync(id, request, ct));
}
