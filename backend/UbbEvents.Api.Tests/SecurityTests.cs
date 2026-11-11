using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using UbbEvents.Api.Data;
using UbbEvents.Api.DTOs;
using UbbEvents.Api.Models;

namespace UbbEvents.Api.Tests;

[Collection("PostgreSQL"), Trait("Category", "Integration")]
public class SecurityTests(PostgresFixture fixture) : ApiTestBase(fixture)
{
    [Theory]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("signature")]
    [InlineData("expiration")]
    [InlineData("subject")]
    public async Task Invalid_jwt_is_rejected(string invalidField)
    {
        var user = await UserAsync();
        var key = invalidField == "signature" ? Convert.ToHexString(RandomNumberGenerator.GetBytes(48)) : Fixture.JwtKey;
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(invalidField == "issuer" ? "wrong" : "ubb-integration",
            invalidField == "audience" ? "wrong" : "ubb-integration-client",
            [new Claim("sub", invalidField == "subject" ? "not-a-user-id" : user.User.Id.ToString()), new Claim("role", "STUDENT")],
            now.AddHours(-2), invalidField == "expiration" ? now.AddMinutes(-5) : now.AddHours(1),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256));
        using var client = Client();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        var response = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain(key, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Missing_role_and_undefined_enums_are_rejected()
    {
        var (staff, org) = await SetupAsync();
        using var owner = staff;
        var user = await UserAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync($"/api/organizations/{org.Id}/members", new { userId = user.User.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync($"/api/organizations/{org.Id}/members", new { userId = user.User.Id, orgRole = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync($"/api/organizations/{org.Id}/members", new { userId = user.User.Id, orgRole = "ADMIN" })).StatusCode);
    }

    [Fact]
    public async Task Participant_identity_is_derived_from_claims_and_history_is_private()
    {
        var (staff, org) = await SetupAsync();
        using var owner = staff;
        var ev = await EventAsync(owner, org.Id);
        var actor = await UserAsync();
        var other = await UserAsync();
        using var participant = Client(actor);
        using var outsider = Client(other);
        var response = await Read<RegistrationResponse>(await participant.PostAsJsonAsync($"/api/events/{ev.Id}/registrations", new { userId = other.User.Id }));
        Assert.Equal(actor.User.Id, response.UserId);
        var ownHistory = await Read<PagedResponse<MyRegistrationResponse>>(await outsider.GetAsync("/api/registrations/me"));
        Assert.Empty(ownHistory.Items);
        Assert.Equal(HttpStatusCode.NotFound, (await Cancel(outsider, ev.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync($"/api/events/{ev.Id}/registrations")).StatusCode);
    }

    [Fact]
    public async Task Development_bootstrap_is_idempotent_and_does_not_elevate_students()
    {
        var email = $"bootstrap-{Guid.NewGuid():N}@ubb.test";
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var config = new Dictionary<string, string?>
        {
            ["DevelopmentAdmin:Enabled"] = "true", ["DevelopmentAdmin:Email"] = email,
            ["DevelopmentAdmin:Password"] = password
        };
        using (var app = new ApiFactory(Fixture, config))
        using (var client = app.CreateClient())
        {
            var auth = await Read<AuthResponse>(await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password), Json));
            Assert.Equal(GlobalRole.ADMIN, auth.User.GlobalRole);
        }
        using (var app = new ApiFactory(Fixture, config))
        using (var client = app.CreateClient()) Assert.True((await client.GetAsync("/api/health")).IsSuccessStatusCode);
        await using var db = new AppDbContext(Fixture.Options);
        var admin = await db.Users.SingleAsync(x => x.Email == email);
        Assert.Equal(1, await db.AuditLogs.CountAsync(x => x.EntityId == admin.Id && x.Action == "DevelopmentAdminCreated"));
        var student = await UserAsync();
        config["DevelopmentAdmin:Email"] = student.User.Email;
        using var invalid = new ApiFactory(Fixture, config);
        Assert.Throws<InvalidOperationException>(() => invalid.CreateClient());
        Assert.Equal(GlobalRole.STUDENT, (await db.Users.SingleAsync(x => x.Id == student.User.Id)).GlobalRole);
    }

    [Fact]
    public void Development_bootstrap_is_disabled_outside_development()
    {
        using var app = new ApiFactory(Fixture, new Dictionary<string, string?> { ["DevelopmentAdmin:Enabled"] = "true" }, "Production");
        Assert.Throws<InvalidOperationException>(() => app.CreateClient());
    }

    [Fact]
    public async Task Authentication_rate_limit_returns_problem_details()
    {
        using var client = Client();
        HttpResponseMessage? response = null;
        for (var i = 0; i < 21; i++)
            response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("absent@ubb.test", "Wrong password"), Json);
        Assert.Equal(HttpStatusCode.TooManyRequests, response!.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
