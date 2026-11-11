using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using UbbEvents.Api.Data;
using UbbEvents.Api.Models;

namespace UbbEvents.Api.Tests;

[Collection("PostgreSQL"), Trait("Category", "Integration")]
public class PersistenceTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Initial_database_upgrades_additively_and_preserves_data()
    {
        // Another database inside the disposable test server, never the developer's server.
        var database = $"upgrade_{Guid.NewGuid():N}";
        await using (var connection = new NpgsqlConnection(fixture.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", connection);
            await command.ExecuteNonQueryAsync();
        }
        var connectionString = new NpgsqlConnectionStringBuilder(fixture.ConnectionString) { Database = database }.ConnectionString;
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
        var user = new User { Email = $"{Guid.NewGuid():N}@UBB.TEST", FullName = "Existing User", PasswordHash = "existing hash" };
        var org = new Organization { Name = "Existing organization", Description = "Existing data" };
        var ev = new Event
        {
            OrganizationId = org.Id, CreatedBy = user.Id, Title = "Existing event", Type = "Workshop", Venue = "Campus",
            StartTime = DateTime.UtcNow.AddDays(1), EndTime = DateTime.UtcNow.AddDays(2), MaxCapacity = 1, Version = 0
        };
        var registration = new Registration { EventId = ev.Id, UserId = user.Id, Status = RegistrationStatus.CONFIRMED };
        await using (var db = new AppDbContext(options))
        {
            await db.GetService<IMigrator>().MigrateAsync("20261107162303_InitialCreate");
            db.AddRange(user, org, ev);
            await db.SaveChangesAsync();
            // Use the original schema contract: it had no database default for CheckedIn.
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Registrations\" (\"Id\", \"EventId\", \"UserId\", \"Status\", \"CheckedIn\", \"RegisteredAt\") VALUES ({registration.Id}, {ev.Id}, {user.Id}, {"CONFIRMED"}, {false}, {registration.RegisteredAt})");
            await db.Database.MigrateAsync();
        }
        await using (var db = new AppDbContext(options))
        {
            Assert.Equal(user.Email, (await db.Users.SingleAsync()).Email);
            Assert.Equal(ev.Id, (await db.Events.SingleAsync()).Id);
            Assert.Equal(1, (await db.Events.SingleAsync()).Version);
            Assert.Equal(registration.Id, (await db.Registrations.SingleAsync()).Id);
            Assert.Empty(await db.AuditLogs.ToListAsync());
            Assert.Equal(2, (await db.Database.GetAppliedMigrationsAsync()).Count());
            Assert.False(db.Database.HasPendingModelChanges());
        }
    }

    [Fact]
    public async Task Normalized_email_unique_index_is_enforced_by_postgresql()
    {
        var email = $"{Guid.NewGuid():N}@ubb.test";
        await using var db = new AppDbContext(fixture.Options);
        db.Users.Add(new User { Email = email, FullName = "First", PasswordHash = "fixture hash" });
        await db.SaveChangesAsync();
        db.Users.Add(new User { Email = $" {email.ToUpperInvariant()} ", FullName = "Duplicate", PasswordHash = "fixture hash" });
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    [Fact]
    public async Task Audit_failure_rolls_back_registration_in_the_same_transaction()
    {
        await using var db = new AppDbContext(fixture.Options);
        var user = new User { Email = $"{Guid.NewGuid():N}@ubb.test", FullName = "Fixture", PasswordHash = "fixture hash" };
        var org = new Organization { Name = $"Atomic {Guid.NewGuid():N}", Description = "Fixture" };
        var ev = new Event
        {
            OrganizationId = org.Id, CreatedBy = user.Id, Title = "Atomic event", MaxCapacity = 1,
            StartTime = DateTime.UtcNow.AddDays(1), EndTime = DateTime.UtcNow.AddDays(2)
        };
        db.AddRange(user, org, ev);
        await db.SaveChangesAsync();
        var registration = new Registration { EventId = ev.Id, UserId = user.Id, Status = RegistrationStatus.CONFIRMED };
        db.Registrations.Add(registration);
        db.AuditLogs.Add(new AuditLog { ActorUserId = Guid.NewGuid(), EntityId = registration.Id, Action = "InvalidActor", EntityType = "Registration" });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        await using var verify = new AppDbContext(fixture.Options);
        Assert.False(await verify.Registrations.AnyAsync(x => x.Id == registration.Id));
        Assert.False(await verify.AuditLogs.AnyAsync(x => x.EntityId == registration.Id));
    }
}
