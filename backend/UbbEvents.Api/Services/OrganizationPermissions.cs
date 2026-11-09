using Microsoft.EntityFrameworkCore;
using UbbEvents.Api.Data;
using UbbEvents.Api.Infrastructure;
using UbbEvents.Api.Models;

namespace UbbEvents.Api.Services;

public sealed class OrganizationPermissions(AppDbContext db, CurrentUser currentUser)
{
    public async Task<User> RequireAdminAsync(CancellationToken ct)
    {
        var user = await currentUser.RequireAsync(ct);
        if (user.GlobalRole != GlobalRole.ADMIN) throw ApiException.Forbidden();
        return user;
    }

    public async Task<bool> CanManageEventsAsync(Guid organizationId, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated) return false;
        var user = await currentUser.RequireAsync(ct);
        return user.GlobalRole == GlobalRole.ADMIN || await db.OrganizationMembers.AnyAsync(
            x => x.UserId == user.Id && x.OrganizationId == organizationId &&
                (x.OrgRole == OrgRole.ORG_ADMIN || x.OrgRole == OrgRole.ORGANIZER), ct);
    }

    public async Task<User> RequireStaffAsync(Guid organizationId, CancellationToken ct)
    {
        var user = await currentUser.RequireAsync(ct);
        if (!await CanManageEventsAsync(organizationId, ct)) throw ApiException.Forbidden();
        return user;
    }

    public async Task<User> RequireMemberManagerAsync(Guid organizationId, CancellationToken ct)
    {
        var user = await currentUser.RequireAsync(ct);
        if (user.GlobalRole != GlobalRole.ADMIN && !await db.OrganizationMembers.AnyAsync(
            x => x.UserId == user.Id && x.OrganizationId == organizationId && x.OrgRole == OrgRole.ORG_ADMIN, ct))
            throw ApiException.Forbidden();
        return user;
    }
}
