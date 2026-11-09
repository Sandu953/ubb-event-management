using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UbbEvents.Api.Data;
using UbbEvents.Api.DTOs;
using UbbEvents.Api.Infrastructure;
using UbbEvents.Api.Models;

namespace UbbEvents.Api.Services;

public sealed class AuthService(AppDbContext db, IPasswordHasher<User> passwords, JwtTokenService tokens,
    CurrentUser currentUser, AuditService audit)
{
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        RequestValidation.Ensure(request);
        var email = NormalizeEmail(request.Email);
        if (await db.Users.AnyAsync(x => x.Email.Trim().ToLower() == email, ct))
            throw ApiException.Conflict("This email address is already registered.");
        var user = new User { Email = email, FullName = request.FullName.Trim(), GlobalRole = GlobalRole.STUDENT };
        user.PasswordHash = passwords.HashPassword(user, request.Password);
        db.Users.Add(user);
        audit.Record(user.Id, null, "User", user.Id, "UserRegistered");
        await db.SaveChangesAsync(ct);
        return tokens.Issue(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        RequestValidation.Ensure(request);
        var email = NormalizeEmail(request.Email);
        var user = await db.Users.SingleOrDefaultAsync(x => x.Email.Trim().ToLower() == email, ct);
        if (user is null) throw ApiException.Unauthorized();
        var result = passwords.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed) throw ApiException.Unauthorized();
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwords.HashPassword(user, request.Password);
            user.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        return tokens.Issue(user);
    }

    public async Task<CurrentUserResponse> MeAsync(CancellationToken ct)
    {
        var user = await currentUser.RequireAsync(ct);
        var memberships = await (from m in db.OrganizationMembers.AsNoTracking()
            join o in db.Organizations on m.OrganizationId equals o.Id
            where m.UserId == user.Id
            orderby o.Name
            select new MembershipResponse(o.Id, o.Name, m.OrgRole)).ToListAsync(ct);
        return new CurrentUserResponse(user.ToResponse(), memberships);
    }
}
