using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UbbEvents.Api.Data;
using UbbEvents.Api.DTOs;
using UbbEvents.Api.Infrastructure;
using UbbEvents.Api.Models;

namespace UbbEvents.Api.Services;

public static class DevelopmentAdminSeeder
{
    public static async Task SeedAsync(WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>("DevelopmentAdmin:Enabled")) return;
        if (!app.Environment.IsDevelopment())
            throw new InvalidOperationException("Development administrator seeding is only allowed in Development.");
        var email = app.Configuration["DevelopmentAdmin:Email"] ?? "";
        var password = app.Configuration["DevelopmentAdmin:Password"] ?? "";
        var fullName = app.Configuration["DevelopmentAdmin:FullName"] ?? "Development Administrator";
        RequestValidation.Ensure(new RegisterRequest(email, password, fullName));
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        email = AuthService.NormalizeEmail(email);
        // Serialize bootstraps, including across multiple development app instances.
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(1488753231)");
        var existing = await db.Users.SingleOrDefaultAsync(x => x.Email.Trim().ToLower() == email);
        if (existing is not null)
        {
            if (existing.GlobalRole != GlobalRole.ADMIN)
                throw new InvalidOperationException("The bootstrap email belongs to a student. Choose a different email.");
            await tx.CommitAsync();
            return;
        }
        var admin = new User { Email = email, FullName = fullName.Trim(), GlobalRole = GlobalRole.ADMIN };
        admin.PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>().HashPassword(admin, password);
        db.Users.Add(admin);
        scope.ServiceProvider.GetRequiredService<AuditService>().Record(admin.Id, null, "User", admin.Id, "DevelopmentAdminCreated");
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }
}
