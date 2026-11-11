using Microsoft.EntityFrameworkCore;
using UbbEvents.Api.Data;
using UbbEvents.Api.DTOs;
using UbbEvents.Api.Infrastructure;
using UbbEvents.Api.Models;

namespace UbbEvents.Api.Services;

public sealed class OrganizationService(AppDbContext db, CurrentUser currentUser,
    OrganizationPermissions permissions, AuditService audit)
{
    public async Task<PagedResponse<OrganizationResponse>> ListAsync(PagingQuery paging, CancellationToken ct)
    {
        RequestValidation.Ensure(paging);
        var user = await currentUser.RequireAsync(ct);
        var query = db.Organizations.AsNoTracking().AsQueryable();
        if (user.GlobalRole != GlobalRole.ADMIN)
            query = query.Where(o => db.OrganizationMembers.Any(m => m.OrganizationId == o.Id && m.UserId == user.Id));
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Skip((paging.Page - 1) * paging.PageSize).Take(paging.PageSize).ToListAsync(ct);
        return new(items.Select(x => x.ToResponse()).ToList(), paging.Page, paging.PageSize, total);
    }

    public async Task<OrganizationResponse> GetAsync(Guid id, CancellationToken ct)
    {
        await permissions.RequireStaffAsync(id, ct);
        return (await db.Organizations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw ApiException.NotFound("Organization")).ToResponse();
    }

    public async Task<OrganizationResponse> CreateAsync(CreateOrganizationRequest request, CancellationToken ct)
    {
        var actor = await permissions.RequireAdminAsync(ct);
        RequestValidation.Ensure(request);
        RequestValidation.Nonempty(request.InitialAdminUserId, nameof(request.InitialAdminUserId));
        if (!await db.Users.AnyAsync(x => x.Id == request.InitialAdminUserId, ct)) throw ApiException.NotFound("Initial admin user");
        var name = request.Name.Trim();
        if (await db.Organizations.AnyAsync(x => x.Name == name, ct)) throw ApiException.Conflict("Organization name already exists.");
        var organization = new Organization { Name = name, Description = request.Description.Trim() };
        db.Organizations.Add(organization);
        db.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = organization.Id, UserId = request.InitialAdminUserId, OrgRole = OrgRole.ORG_ADMIN
        });
        audit.Record(actor.Id, organization.Id, "Organization", organization.Id, "OrganizationCreated");
        audit.Record(actor.Id, organization.Id, "OrganizationMember", request.InitialAdminUserId, "MemberAssigned", "ORG_ADMIN");
        await db.SaveChangesAsync(ct);
        return organization.ToResponse();
    }

    public async Task<PagedResponse<MemberResponse>> MembersAsync(Guid id, PagingQuery paging, CancellationToken ct)
    {
        RequestValidation.Ensure(paging);
        await permissions.RequireStaffAsync(id, ct);
        if (!await db.Organizations.AnyAsync(x => x.Id == id, ct)) throw ApiException.NotFound("Organization");
        var query = from member in db.OrganizationMembers.AsNoTracking()
            join user in db.Users on member.UserId equals user.Id
            where member.OrganizationId == id
            select new { user.Id, user.Email, user.FullName, member.OrgRole };
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.FullName).ThenBy(x => x.Id)
            .Skip((paging.Page - 1) * paging.PageSize).Take(paging.PageSize)
            .Select(x => new MemberResponse(x.Id, x.Email, x.FullName, x.OrgRole)).ToListAsync(ct);
        return new(items, paging.Page, paging.PageSize, total);
    }

    private async Task LockOrganizationAsync(Guid id, CancellationToken ct)
    {
        _ = await db.Organizations.FromSqlInterpolated($"SELECT * FROM \"Organizations\" WHERE \"Id\" = {id} FOR UPDATE")
            .SingleOrDefaultAsync(ct) ?? throw ApiException.NotFound("Organization");
    }

    public async Task<MemberResponse> AddMemberAsync(Guid id, AddMemberRequest request, CancellationToken ct)
    {
        RequestValidation.Ensure(request);
        RequestValidation.Nonempty(request.UserId, nameof(request.UserId));
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        await LockOrganizationAsync(id, ct);
        var actor = await permissions.RequireMemberManagerAsync(id, ct);
        if (request.OrgRole == OrgRole.ORG_ADMIN && actor.GlobalRole != GlobalRole.ADMIN) throw ApiException.Forbidden();
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == request.UserId, ct) ?? throw ApiException.NotFound("User");
        if (await db.OrganizationMembers.AnyAsync(x => x.OrganizationId == id && x.UserId == request.UserId, ct))
            throw ApiException.Conflict("This user is already a member.");
        db.OrganizationMembers.Add(new OrganizationMember { OrganizationId = id, UserId = user.Id, OrgRole = request.OrgRole });
        audit.Record(actor.Id, id, "OrganizationMember", user.Id, "MemberAssigned", request.OrgRole.ToString());
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(user.Id, user.Email, user.FullName, request.OrgRole);
    }

    public async Task ChangeMemberAsync(Guid id, Guid userId, UpdateMemberRequest? request, CancellationToken ct)
    {
        if (request is not null) RequestValidation.Ensure(request);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        await LockOrganizationAsync(id, ct);
        var actor = await permissions.RequireMemberManagerAsync(id, ct);
        var member = await db.OrganizationMembers.SingleOrDefaultAsync(x => x.OrganizationId == id && x.UserId == userId, ct)
            ?? throw ApiException.NotFound("Member");
        // Only global administrators appoint, demote, or remove organization administrators.
        if (actor.GlobalRole != GlobalRole.ADMIN && (member.OrgRole == OrgRole.ORG_ADMIN || request?.OrgRole == OrgRole.ORG_ADMIN))
            throw ApiException.Forbidden();
        if (member.OrgRole == OrgRole.ORG_ADMIN && request?.OrgRole != OrgRole.ORG_ADMIN &&
            !await db.OrganizationMembers.AnyAsync(x => x.OrganizationId == id && x.UserId != userId && x.OrgRole == OrgRole.ORG_ADMIN, ct))
            throw ApiException.Conflict("An organization must retain at least one organization administrator.");
        var previous = member.OrgRole;
        if (request is null) db.OrganizationMembers.Remove(member);
        else member.OrgRole = request.OrgRole;
        audit.Record(actor.Id, id, "OrganizationMember", userId, request is null ? "MemberRemoved" : "MemberRoleChanged",
            $"{previous} -> {request?.OrgRole.ToString() ?? "REMOVED"}");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
