using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;
using UbbEvents.Api.Data;
using UbbEvents.Api.DTOs;
using UbbEvents.Api.Models;

namespace UbbEvents.Api.Tests;

[CollectionDefinition("PostgreSQL")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;

public sealed class PostgresFixture : IAsyncLifetime
{
    // Always start a NEW disposable server on a random port. No developer connection string is accepted.
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder().WithImage("postgres:16")
        .WithDatabase("ubb_integration").WithUsername("ubb_test")
        .WithPassword(Convert.ToHexString(RandomNumberGenerator.GetBytes(32))).Build();
    public string ConnectionString => postgres.GetConnectionString();
    public string JwtKey { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(48));
    public DbContextOptions<AppDbContext> Options => new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionString).Options;

    public async Task InitializeAsync()
    {
        await postgres.StartAsync();
        await using var db = new AppDbContext(Options);
        await db.Database.MigrateAsync();
    }
    public Task DisposeAsync() => postgres.DisposeAsync().AsTask();
}

public sealed class ApiFactory(PostgresFixture fixture, Dictionary<string, string?>? overrides = null,
    string environment = "Development") : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.ConfigureAppConfiguration((_, config) =>
        {
            var values = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = fixture.ConnectionString,
                ["Jwt:Issuer"] = "ubb-integration",
                ["Jwt:Audience"] = "ubb-integration-client",
                ["Jwt:SigningKey"] = fixture.JwtKey,
                ["Jwt:ExpirationMinutes"] = "60",
                ["DevelopmentAdmin:Enabled"] = "false",
                ["Logging:LogLevel:Default"] = "Warning",
                ["Logging:LogLevel:Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware"] = "None"
            };
            if (overrides is not null) foreach (var pair in overrides) values[pair.Key] = pair.Value;
            config.AddInMemoryCollection(values);
        });
    }
}

public abstract class ApiTestBase(PostgresFixture fixture) : IDisposable
{
    protected readonly PostgresFixture Fixture = fixture;
    protected readonly ApiFactory Factory = new(fixture);
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
    protected HttpClient Client() => Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    protected HttpClient Client(AuthResponse auth)
    {
        var client = Client();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
    protected static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"HTTP {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }
    protected async Task<AuthResponse> UserAsync(GlobalRole role = GlobalRole.STUDENT)
    {
        // Fixture setup bypasses HTTP only to create the global admin; production has no such endpoint.
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(18));
        var user = new User { Email = $"{Guid.NewGuid():N}@ubb.test", FullName = "Test User", GlobalRole = role };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password);
        await using var db = new AppDbContext(Fixture.Options);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        using var client = Client();
        return await Read<AuthResponse>(await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(user.Email, password), Json));
    }
    protected async Task<OrganizationResponse> OrganizationAsync(HttpClient admin, Guid initialAdmin)
        => await Read<OrganizationResponse>(await admin.PostAsJsonAsync("/api/organizations",
            new CreateOrganizationRequest($"Organization {Guid.NewGuid():N}", "Student organization", initialAdmin), Json));

    protected static CreateEventRequest EventInput(Guid organizationId, int capacity = 1) => new()
    {
        OrganizationId = organizationId, Title = "Concurrency workshop", Type = "Workshop", Description = "Testing registrations",
        Venue = "UBB Room 1", StartTime = DateTimeOffset.UtcNow.AddDays(1), EndTime = DateTimeOffset.UtcNow.AddDays(1).AddHours(2),
        MaxCapacity = capacity
    };
    protected async Task<EventResponse> EventAsync(HttpClient staff, Guid organizationId, int capacity = 1, bool open = true)
    {
        var ev = await Read<EventResponse>(await staff.PostAsJsonAsync("/api/events", EventInput(organizationId, capacity), Json));
        if (open)
        {
            ev = await Transition(staff, ev, EventStatus.PUBLISHED);
            ev = await Transition(staff, ev, EventStatus.REGISTRATION_OPEN);
        }
        return ev;
    }
    protected static Task<HttpResponseMessage> ChangeStatus(HttpClient staff, EventResponse ev, EventStatus status)
        => staff.PatchAsJsonAsync($"/api/events/{ev.Id}/status", new ChangeEventStatusRequest(status, ev.Version), Json);
    protected static async Task<EventResponse> Transition(HttpClient staff, EventResponse ev, EventStatus status)
        => await Read<EventResponse>(await ChangeStatus(staff, ev, status));
    protected static UpdateEventRequest UpdateInput(EventResponse ev, int capacity) => new()
    {
        Title = ev.Title, Type = ev.Type, Description = ev.Description, Venue = ev.Venue,
        StartTime = ev.StartTime, EndTime = ev.EndTime, Version = ev.Version, MaxCapacity = capacity
    };
    protected static Task<HttpResponseMessage> Register(HttpClient participant, Guid eventId)
        => participant.PostAsync($"/api/events/{eventId}/registrations", null);
    protected static Task<HttpResponseMessage> Cancel(HttpClient participant, Guid eventId)
        => participant.DeleteAsync($"/api/events/{eventId}/registrations/me");
    protected async Task<(HttpClient Staff, OrganizationResponse Organization)> SetupAsync()
    {
        var admin = await UserAsync(GlobalRole.ADMIN);
        var client = Client(admin);
        return (client, await OrganizationAsync(client, admin.User.Id));
    }
    public void Dispose() => Factory.Dispose();
}
