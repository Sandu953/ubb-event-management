using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using UbbEvents.Api.Data;
using UbbEvents.Api.DTOs;
using UbbEvents.Api.Models;

namespace UbbEvents.Api.Tests;

[Collection("PostgreSQL"), Trait("Category", "Integration")]
public class AuthAndOrganizationTests(PostgresFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task Register_login_and_me_use_secure_student_account()
    {
        using var anonymous = Client();
        var email = $"{Guid.NewGuid():N}@ubb.test";
        var request = new RegisterRequest(email.ToUpperInvariant(), "A long test password 123!", "A Student");
        var response = await anonymous.PostAsJsonAsync("/api/auth/register", request, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var auth = await Read<AuthResponse>(response);
        Assert.Equal(GlobalRole.STUDENT, auth.User.GlobalRole);
        Assert.Equal(email, auth.User.Email);
        Assert.DoesNotContain("passwordHash", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        using var user = Client(auth);
        var me = await Read<CurrentUserResponse>(await user.GetAsync("/api/auth/me"));
        Assert.Equal(auth.User.Id, me.User.Id);
        Assert.Empty(me.Memberships);
        await using var db = new AppDbContext(Fixture.Options);
        var stored = await db.Users.SingleAsync(x => x.Id == auth.User.Id);
        Assert.NotEqual(request.Password, stored.PasswordHash);
        Assert.Equal(HttpStatusCode.Conflict, (await anonymous.PostAsJsonAsync("/api/auth/register", request with { Email = email }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, "Wrong password"), Json)).StatusCode);
        var login = await Read<AuthResponse>(await anonymous.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, request.Password), Json));
        Assert.Equal(auth.User.Id, login.User.Id);
    }

    [Theory]
    [InlineData("bad-email", "A long password 123!", "Student")]
    [InlineData("student@ubb.test", "short", "Student")]
    [InlineData("student@ubb.test", "A long password 123!", " ")]
    public async Task Invalid_account_fields_are_validation_problems(string email, string password, string name)
    {
        using var client = Client();
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, password, name), Json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Anonymous_and_overposting_requests_cannot_elevate_privileges()
    {
        using var client = Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = $"{Guid.NewGuid():N}@ubb.test", password = "A long password 123!", fullName = "Student", globalRole = "ADMIN"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var student = await UserAsync();
        using var participant = Client(student);
        Assert.Equal(HttpStatusCode.Forbidden, (await participant.PostAsJsonAsync("/api/organizations",
            new CreateOrganizationRequest("Unauthorized", "Description", student.User.Id), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/events", EventInput(Guid.NewGuid()), Json)).StatusCode);
    }

    [Fact]
    public async Task Organization_onboarding_scopes_roles_and_preserves_last_administrator()
    {
        var global = await UserAsync(GlobalRole.ADMIN);
        var orgAdmin = await UserAsync();
        var organizer = await UserAsync();
        var other = await UserAsync();
        using var admin = Client(global);
        using var owner = Client(orgAdmin);
        using var staff = Client(organizer);
        using var outsider = Client(other);
        var org = await OrganizationAsync(admin, orgAdmin.User.Id);
        var second = await OrganizationAsync(admin, other.User.Id);
        var add = new AddMemberRequest(organizer.User.Id, OrgRole.ORGANIZER);
        Assert.Equal(HttpStatusCode.Created, (await owner.PostAsJsonAsync($"/api/organizations/{org.Id}/members", add, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync($"/api/organizations/{org.Id}/members", add, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync($"/api/organizations/{org.Id}/members",
            new AddMemberRequest(other.User.Id, OrgRole.ORG_ADMIN), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsJsonAsync($"/api/organizations/{org.Id}/members",
            new AddMemberRequest(other.User.Id, OrgRole.ORGANIZER), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync($"/api/organizations/{org.Id}/members")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync($"/api/organizations/{second.Id}")).StatusCode);
        var members = await Read<PagedResponse<MemberResponse>>(await owner.GetAsync($"/api/organizations/{org.Id}/members?pageSize=1"));
        Assert.Single(members.Items);
        Assert.Equal(2, members.TotalCount);
        var own = await Read<PagedResponse<OrganizationResponse>>(await owner.GetAsync("/api/organizations"));
        Assert.Single(own.Items);
        Assert.Equal(org.Id, own.Items[0].Id);
        // The same user may have an independent role in another tenant.
        await Read<MemberResponse>(await admin.PostAsJsonAsync($"/api/organizations/{second.Id}/members",
            new AddMemberRequest(organizer.User.Id, OrgRole.ORG_ADMIN), Json));
        var memberships = await Read<CurrentUserResponse>(await staff.GetAsync("/api/auth/me"));
        Assert.Contains(memberships.Memberships, x => x.OrganizationId == org.Id && x.OrgRole == OrgRole.ORGANIZER);
        Assert.Contains(memberships.Memberships, x => x.OrganizationId == second.Id && x.OrgRole == OrgRole.ORG_ADMIN);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/organizations/{org.Id}/members/{orgAdmin.User.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PutAsJsonAsync($"/api/organizations/{org.Id}/members/{orgAdmin.User.Id}",
            new UpdateMemberRequest(OrgRole.ORGANIZER), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/organizations/{org.Id}/members/{organizer.User.Id}",
            new UpdateMemberRequest(OrgRole.ORG_ADMIN), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/organizations/{org.Id}/members/{orgAdmin.User.Id}")).StatusCode);
        // Revoking membership takes effect even with a previously issued token.
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync($"/api/organizations/{org.Id}/members")).StatusCode);
        await using var db = new AppDbContext(Fixture.Options);
        Assert.True(await db.AuditLogs.AnyAsync(x => x.OrganizationId == org.Id && x.Action == "MemberRoleChanged"));
        Assert.True(await db.AuditLogs.AnyAsync(x => x.OrganizationId == org.Id && x.Action == "MemberRemoved"));
    }
}
