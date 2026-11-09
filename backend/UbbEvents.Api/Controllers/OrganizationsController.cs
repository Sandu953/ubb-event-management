using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UbbEvents.Api.DTOs;
using UbbEvents.Api.Services;

namespace UbbEvents.Api.Controllers;

[ApiController, Route("api/organizations"), Authorize]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401),
 ProducesResponseType<ProblemDetails>(403), ProducesResponseType<ProblemDetails>(404)]
public sealed class OrganizationsController(OrganizationService organizations) : ControllerBase
{
    /// <summary>List own organizations; a global administrator can list all.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResponse<OrganizationResponse>>(200)]
    public async Task<ActionResult<PagedResponse<OrganizationResponse>>> List([FromQuery] PagingQuery query, CancellationToken ct)
        => Ok(await organizations.ListAsync(query, ct));

    /// <summary>Get organization details (organization staff or global administrator).</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<OrganizationResponse>(200)]
    public async Task<ActionResult<OrganizationResponse>> Get(Guid id, CancellationToken ct) => Ok(await organizations.GetAsync(id, ct));

    /// <summary>Create an organization and assign its initial administrator (global ADMIN).</summary>
    [HttpPost, Authorize(Policy = "GlobalAdmin")]
    [ProducesResponseType<OrganizationResponse>(201), ProducesResponseType<ProblemDetails>(409)]
    public async Task<ActionResult<OrganizationResponse>> Create(CreateOrganizationRequest request, CancellationToken ct)
    {
        var result = await organizations.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    /// <summary>List members (organization staff or global administrator).</summary>
    [HttpGet("{id:guid}/members")]
    [ProducesResponseType<PagedResponse<MemberResponse>>(200)]
    public async Task<ActionResult<PagedResponse<MemberResponse>>> Members(Guid id, [FromQuery] PagingQuery query, CancellationToken ct)
        => Ok(await organizations.MembersAsync(id, query, ct));

    /// <summary>Add an organizer (ORG_ADMIN); only global ADMIN may appoint another ORG_ADMIN.</summary>
    [HttpPost("{id:guid}/members")]
    [ProducesResponseType<MemberResponse>(201), ProducesResponseType<ProblemDetails>(409)]
    public async Task<ActionResult<MemberResponse>> AddMember(Guid id, AddMemberRequest request, CancellationToken ct)
        => CreatedAtAction(nameof(Members), new { id }, await organizations.AddMemberAsync(id, request, ct));

    /// <summary>Change a member role; organization administrator assignments require global ADMIN.</summary>
    [HttpPut("{id:guid}/members/{userId:guid}")]
    [ProducesResponseType(204), ProducesResponseType<ProblemDetails>(409)]
    public async Task<IActionResult> UpdateMember(Guid id, Guid userId, UpdateMemberRequest request, CancellationToken ct)
    {
        await organizations.ChangeMemberAsync(id, userId, request, ct);
        return NoContent();
    }

    /// <summary>Remove an organizer (ORG_ADMIN) or administrator (global ADMIN). Retain the last administrator.</summary>
    [HttpDelete("{id:guid}/members/{userId:guid}")]
    [ProducesResponseType(204), ProducesResponseType<ProblemDetails>(409)]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId, CancellationToken ct)
    {
        await organizations.ChangeMemberAsync(id, userId, null, ct);
        return NoContent();
    }
}
