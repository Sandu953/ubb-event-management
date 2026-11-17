using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using UbbEvents.Api.Models;

namespace UbbEvents.Api.Data.Seeding;

public sealed record DemoSeedResult(bool DryRun, string Target, int Users, int Organizations,
    int Memberships, int Events, int Registrations, int AuditLogs);

public sealed class DemoSeedException(string message) : Exception(message);

public sealed class DevelopmentDataSeeder(AppDbContext db, IHostEnvironment environment,
    IConfiguration configuration, IPasswordHasher<User> passwords, TimeProvider clock)
{
    public string Target
    {
        get
        {
            var connection = new NpgsqlConnectionStringBuilder(db.Database.GetConnectionString());
            return $"{connection.Host}:{connection.Port}/{connection.Database}";
        }
    }

    public async Task<DemoSeedResult> SeedAsync(bool dryRun = false, CancellationToken ct = default)
    {
        if (!environment.IsDevelopment())
            throw new DemoSeedException("Demo data seeding is only allowed in Development.");
        var password = configuration["DemoSeed:Password"];
        if (!dryRun && (string.IsNullOrWhiteSpace(password) || password.Length is < 12 or > 128))
            throw new DemoSeedException("Configure DemoSeed:Password with 12-128 characters, or use scripts/seed-demo.ps1.");
        if ((await db.Database.GetPendingMigrationsAsync(ct)).Any())
            throw new DemoSeedException("Apply the existing migrations to the intended development database before seeding. No schema changes were applied.");

        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        await db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '5s'", ct);
        // Share the administrator bootstrap lock; serialize concurrent CLI runs without touching existing event rows.
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(1488753231)", ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var catalog = DemoDataCatalog.Create(now);
        var userIds = catalog.Users.Select(x => x.Id).ToArray();
        var emails = catalog.Users.Select(x => x.Email).ToArray();
        var existingUsers = await db.Users.AsNoTracking()
            .Where(x => userIds.Contains(x.Id) || emails.Contains(x.Email.Trim().ToLower())).ToListAsync(ct);
        foreach (var user in catalog.Users)
        {
            if (existingUsers.Any(x => (x.Id == user.Id && x.Email.Trim().ToLowerInvariant() != user.Email) ||
                (x.Id != user.Id && x.Email.Trim().ToLowerInvariant() == user.Email)))
                throw new DemoSeedException($"Demo identity collision for {user.Email}. Existing accounts were not changed or elevated.");
        }
        var users = catalog.Users.Where(x => existingUsers.All(u => u.Id != x.Id)).ToArray();

        var orgIds = catalog.Organizations.Select(x => x.Id).ToArray();
        var names = catalog.Organizations.Select(x => x.Name).ToArray();
        var existingOrganizations = await db.Organizations.AsNoTracking()
            .Where(x => orgIds.Contains(x.Id) || names.Contains(x.Name)).ToListAsync(ct);
        foreach (var org in catalog.Organizations)
        {
            if (existingOrganizations.Any(x => (x.Id == org.Id && x.Name != org.Name) || (x.Id != org.Id && x.Name == org.Name)))
                throw new DemoSeedException($"Demo organization identity collision for {org.Name}. Existing organizations were not changed.");
        }
        var organizations = catalog.Organizations.Where(x => existingOrganizations.All(o => o.Id != x.Id)).ToArray();
        var existingMemberships = await db.OrganizationMembers.AsNoTracking()
            .Where(x => orgIds.Contains(x.OrganizationId) && userIds.Contains(x.UserId)).ToListAsync(ct);
        var memberships = catalog.Memberships.Where(x => !existingMemberships.Any(
            m => m.OrganizationId == x.OrganizationId && m.UserId == x.UserId)).ToArray();

        var eventIds = catalog.Events.Select(x => x.Id).ToArray();
        var existingEvents = await db.Events.AsNoTracking().Where(x => eventIds.Contains(x.Id)).ToListAsync(ct);
        foreach (var ev in existingEvents)
        {
            var expected = catalog.Events.Single(x => x.Id == ev.Id);
            if (ev.OrganizationId != expected.OrganizationId || ev.CreatedBy != expected.CreatedBy)
                throw new DemoSeedException("A demo event ID belongs to an unexpected organization or creator. No data was changed.");
        }
        // Never refill registrations on an existing event: users may have canceled, checked in, or changed its capacity.
        var events = catalog.Events.Where(x => existingEvents.All(e => e.Id != x.Id)).ToArray();
        var newEventIds = events.Select(x => x.Id).ToHashSet();
        var registrations = catalog.Registrations.Where(x => newEventIds.Contains(x.EventId)).ToArray();
        var audits = users.Length + organizations.Length + memberships.Length + events.Length + registrations.Length;
        var result = new DemoSeedResult(dryRun, Target, users.Length, organizations.Length,
            memberships.Length, events.Length, registrations.Length, audits);
        if (dryRun)
        {
            await tx.CommitAsync(ct);
            return result;
        }

        var actorId = catalog.Users.Single(x => x.GlobalRole == GlobalRole.ADMIN).Id;
        void Audit(string type, Guid id, Guid? orgId, string action, string? details = null) => db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId, OrganizationId = orgId, EntityType = type, EntityId = id,
            Action = action, Details = details, OccurredAt = now
        });
        foreach (var user in users)
        {
            user.PasswordHash = passwords.HashPassword(user, password!);
            Audit("User", user.Id, null, "DemoUserSeeded", user.GlobalRole.ToString());
        }
        foreach (var org in organizations) Audit("Organization", org.Id, org.Id, "DemoOrganizationSeeded");
        foreach (var member in memberships) Audit("OrganizationMember", member.UserId, member.OrganizationId,
            "DemoMembershipSeeded", member.OrgRole.ToString());
        foreach (var ev in events) Audit("Event", ev.Id, ev.OrganizationId, "DemoEventSeeded", ev.Status.ToString());
        foreach (var registration in registrations) Audit("Registration", registration.Id,
            events.Single(x => x.Id == registration.EventId).OrganizationId, "DemoRegistrationSeeded",
            $"{registration.Status}; CheckedIn={registration.CheckedIn}");
        db.Users.AddRange(users);
        db.Organizations.AddRange(organizations);
        db.OrganizationMembers.AddRange(memberships);
        db.Events.AddRange(events);
        db.Registrations.AddRange(registrations);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }
}
