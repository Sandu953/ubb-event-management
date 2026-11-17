using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Npgsql;
using UbbEvents.Api.Data;
using UbbEvents.Api.Data.Seeding;
using UbbEvents.Api.DTOs;
using UbbEvents.Api.Models;

namespace UbbEvents.Api.Tests;

[Collection("PostgreSQL"), Trait("Category", "Integration")]
public class DevelopmentDataSeederTests(PostgresFixture fixture)
{
    private readonly string password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
    private static readonly DateTimeOffset Anchor = new(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);

    private async Task<DbContextOptions<AppDbContext>> DatabaseAsync(bool latest = true)
    {
        var database = $"seed_{Guid.NewGuid():N}";
        await using (var connection = new NpgsqlConnection(fixture.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", connection);
            await command.ExecuteNonQueryAsync();
        }
        var connectionString = new NpgsqlConnectionStringBuilder(fixture.ConnectionString) { Database = database }.ConnectionString;
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
        await using var db = new AppDbContext(options);
        if (latest) await db.Database.MigrateAsync();
        else await db.GetService<IMigrator>().MigrateAsync("20261107162303_InitialCreate");
        return options;
    }

    private DevelopmentDataSeeder Seeder(AppDbContext db, string environment = "Development", string? configuredPassword = null,
        DateTimeOffset? now = null) => new(db, new SeedEnvironment { EnvironmentName = environment },
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DemoSeed:Password"] = configuredPassword ?? password
        }).Build(), new PasswordHasher<User>(), new SeedClock(now ?? Anchor));

    [Fact]
    public async Task Creates_complete_dataset_with_hashed_passwords_and_valid_relationships()
    {
        var options = await DatabaseAsync();
        await using var db = new AppDbContext(options);
        var result = await Seeder(db).SeedAsync();
        Assert.Equal((10, 2, 6, 8, 16, 42),
            (result.Users, result.Organizations, result.Memberships, result.Events, result.Registrations, result.AuditLogs));
        Assert.False(result.DryRun);
        var users = await db.Users.AsNoTracking().ToListAsync();
        Assert.Single(users, x => x.GlobalRole == GlobalRole.ADMIN);
        Assert.All(users, user =>
        {
            Assert.NotEqual(password, user.PasswordHash);
            Assert.NotEqual(PasswordVerificationResult.Failed, new PasswordHasher<User>().VerifyHashedPassword(user, user.PasswordHash, password));
        });
        var events = await db.Events.AsNoTracking().ToListAsync();
        Assert.Equal(Enum.GetValues<EventStatus>().Order(), events.Select(x => x.Status).Distinct().Order());
        var registrations = await db.Registrations.AsNoTracking().ToListAsync();
        Assert.Equal(3, registrations.Count(x => x.Status == RegistrationStatus.WAITLISTED));
        Assert.Equal(3, registrations.Count(x => x.Status == RegistrationStatus.CANCELED));
        Assert.Equal(3, registrations.Count(x => x.CheckedIn));
        Assert.All(events, ev =>
        {
            Assert.True(ev.EndTime > ev.StartTime);
            Assert.Equal(DateTimeKind.Utc, ev.StartTime.Kind);
            Assert.True(registrations.Count(x => x.EventId == ev.Id && x.Status == RegistrationStatus.CONFIRMED) <= ev.MaxCapacity);
        });
        Assert.All(registrations.Where(x => x.CheckedIn), x => Assert.Equal(RegistrationStatus.CONFIRMED, x.Status));
        Assert.Equal(42, await db.AuditLogs.CountAsync());

        // The imported accounts are usable through the real authentication contract.
        using var app = new ApiFactory(fixture, new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = db.Database.GetConnectionString()
        });
        using var client = app.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("global.admin@demo.ubbevents.test", password));
        response.EnsureSuccessStatusCode();
        var json = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)
        {
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };
        Assert.Equal(GlobalRole.ADMIN, (await response.Content.ReadFromJsonAsync<AuthResponse>(json))!.User.GlobalRole);
    }

    [Fact]
    public async Task Dry_run_requires_no_password_and_writes_nothing()
    {
        var options = await DatabaseAsync();
        await using var db = new AppDbContext(options);
        var result = await Seeder(db, configuredPassword: "").SeedAsync(dryRun: true);
        Assert.True(result.DryRun);
        Assert.Equal(10, result.Users);
        Assert.Equal(16, result.Registrations);
        Assert.False(await db.Users.AnyAsync());
        Assert.False(await db.Organizations.AnyAsync());
        Assert.False(await db.Events.AnyAsync());
        Assert.False(await db.AuditLogs.AnyAsync());
    }

    [Fact]
    public async Task Reruns_preserve_credentials_roles_dates_capacity_and_registration_changes()
    {
        var options = await DatabaseAsync();
        DateTime originalStart;
        string modifiedHash;
        Guid changedUser;
        await using (var db = new AppDbContext(options))
        {
            await Seeder(db).SeedAsync();
            var user = await db.Users.SingleAsync(x => x.Email == "student.one@demo.ubbevents.test");
            changedUser = user.Id;
            user.FullName = "Edited by a developer";
            modifiedHash = user.PasswordHash = new PasswordHasher<User>().HashPassword(user, "Another test password 123!");
            var ev = await db.Events.SingleAsync(x => x.Title == "Demo: PostgreSQL concurrency lab");
            originalStart = ev.StartTime;
            ev.MaxCapacity = 3;
            ev.Version = 7;
            var waiting = await db.Registrations.Where(x => x.EventId == ev.Id && x.Status == RegistrationStatus.WAITLISTED)
                .OrderBy(x => x.RegisteredAt).FirstAsync();
            waiting.Status = RegistrationStatus.CANCELED;
            var member = await db.OrganizationMembers.FirstAsync(x => x.OrgRole == OrgRole.ORGANIZER);
            member.OrgRole = OrgRole.ORG_ADMIN;
            db.Users.Add(new User { Email = "unrelated@ubb.test", FullName = "Existing account", PasswordHash = "fixture only" });
            await db.SaveChangesAsync();
        }
        await using (var db = new AppDbContext(options))
        {
            var result = await Seeder(db, configuredPassword: "A different seed password!", now: Anchor.AddDays(30)).SeedAsync();
            Assert.Equal((0, 0, 0, 0, 0, 0),
                (result.Users, result.Organizations, result.Memberships, result.Events, result.Registrations, result.AuditLogs));
            var user = await db.Users.SingleAsync(x => x.Id == changedUser);
            Assert.Equal("Edited by a developer", user.FullName);
            Assert.Equal(modifiedHash, user.PasswordHash);
            var ev = await db.Events.SingleAsync(x => x.Title == "Demo: PostgreSQL concurrency lab");
            Assert.Equal((3, 7, originalStart), (ev.MaxCapacity, ev.Version, ev.StartTime));
            Assert.Equal(2, await db.Registrations.CountAsync(x => x.EventId == ev.Id && x.Status == RegistrationStatus.WAITLISTED));
            Assert.Equal(3, await db.OrganizationMembers.CountAsync(x => x.OrgRole == OrgRole.ORG_ADMIN));
            Assert.Equal(11, await db.Users.CountAsync());
            Assert.Equal(42, await db.AuditLogs.CountAsync());
        }
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task Nondevelopment_environments_are_rejected_before_database_access(string environment)
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=unused;Username=unused").Options);
        Assert.Contains("Development", (await Assert.ThrowsAsync<DemoSeedException>(
            () => Seeder(db, environment).SeedAsync())).Message);
    }

    [Fact]
    public async Task Weak_password_is_rejected_before_database_access()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=unused;Username=unused").Options);
        await Assert.ThrowsAsync<DemoSeedException>(() => Seeder(db, configuredPassword: "short").SeedAsync());
    }

    [Fact]
    public async Task Existing_account_with_reserved_email_is_not_elevated_and_no_partial_data_is_written()
    {
        var options = await DatabaseAsync();
        await using var db = new AppDbContext(options);
        var existing = new User { Email = "GLOBAL.ADMIN@DEMO.UBBEVENTS.TEST", FullName = "Existing student", PasswordHash = "unchanged" };
        db.Users.Add(existing);
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<DemoSeedException>(() => Seeder(db).SeedAsync());
        Assert.Equal(GlobalRole.STUDENT, (await db.Users.SingleAsync()).GlobalRole);
        Assert.Equal("unchanged", (await db.Users.SingleAsync()).PasswordHash);
        Assert.False(await db.Organizations.AnyAsync());
        Assert.False(await db.AuditLogs.AnyAsync());
    }

    [Fact]
    public async Task Organization_name_collision_rolls_back_the_entire_seed()
    {
        var options = await DatabaseAsync();
        await using var db = new AppDbContext(options);
        db.Organizations.Add(new Organization { Name = "Demo UBB Tech Society", Description = "Existing tenant" });
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<DemoSeedException>(() => Seeder(db).SeedAsync());
        Assert.False(await db.Users.AnyAsync());
        Assert.Equal(1, await db.Organizations.CountAsync());
        Assert.False(await db.Events.AnyAsync());
    }

    [Fact]
    public async Task Concurrent_seeders_create_one_dataset()
    {
        var options = await DatabaseAsync();
        await using var first = new AppDbContext(options);
        await using var second = new AppDbContext(options);
        var results = await Task.WhenAll(Seeder(first).SeedAsync(), Seeder(second).SeedAsync());
        Assert.Equal(10, results.Sum(x => x.Users));
        Assert.Equal(16, results.Sum(x => x.Registrations));
        Assert.Equal(42, await first.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task Pending_migrations_are_reported_without_applying_them()
    {
        var options = await DatabaseAsync(latest: false);
        await using var db = new AppDbContext(options);
        Assert.Contains("migrations", (await Assert.ThrowsAsync<DemoSeedException>(() => Seeder(db).SeedAsync())).Message);
        Assert.Single(await db.Database.GetAppliedMigrationsAsync());
        Assert.False(await db.Users.AnyAsync());
    }

    [Fact]
    public async Task Cli_exits_after_seeding_without_a_web_server_or_jwt_configuration()
    {
        var options = await DatabaseAsync();
        await using var db = new AppDbContext(options);
        var processInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
            WorkingDirectory = AppContext.BaseDirectory, CreateNoWindow = true
        };
        foreach (var argument in new[] { "exec", "--runtimeconfig", Path.Combine(AppContext.BaseDirectory, "UbbEvents.Api.Tests.runtimeconfig.json"),
            "--depsfile", Path.Combine(AppContext.BaseDirectory, "UbbEvents.Api.Tests.deps.json"), typeof(Program).Assembly.Location, "--seed-demo" })
            processInfo.ArgumentList.Add(argument);
        processInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        processInfo.Environment["DOTNET_ENVIRONMENT"] = "Development";
        processInfo.Environment["ConnectionStrings__DefaultConnection"] = db.Database.GetConnectionString();
        processInfo.Environment["DemoSeed__Password"] = password;
        processInfo.Environment["Jwt__SigningKey"] = "";
        processInfo.Environment["Logging__LogLevel__Microsoft"] = "Warning";
        using var process = Process.Start(processInfo)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30)); }
        catch { process.Kill(entireProcessTree: true); throw; }
        var output = await stdout + await stderr;
        Assert.True(process.ExitCode == 0, output);
        Assert.Contains("10 users", output);
        Assert.DoesNotContain(password, output);
        Assert.Equal(10, await db.Users.CountAsync());
    }

    private sealed class SeedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class SeedEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "SeedTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
