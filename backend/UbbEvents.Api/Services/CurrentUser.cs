using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using UbbEvents.Api.Data;
using UbbEvents.Api.Infrastructure;
using UbbEvents.Api.Models;

namespace UbbEvents.Api.Services;

public sealed class CurrentUser(IHttpContextAccessor accessor, AppDbContext db)
{
    private Task<User>? userTask;
    public bool IsAuthenticated => accessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public Task<User> RequireAsync(CancellationToken ct) => userTask ??= LoadAsync(ct);

    private async Task<User> LoadAsync(CancellationToken ct)
    {
        var subject = accessor.HttpContext?.User.FindFirstValue("sub");
        if (!IsAuthenticated || !Guid.TryParse(subject, out var id) || id == Guid.Empty)
            throw ApiException.Unauthorized();
        return await db.Users.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiException.Unauthorized();
    }
}
