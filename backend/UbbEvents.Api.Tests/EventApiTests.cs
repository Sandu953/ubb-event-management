using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using UbbEvents.Api.DTOs;
using UbbEvents.Api.Models;

namespace UbbEvents.Api.Tests;

[Collection("PostgreSQL"), Trait("Category", "Integration")]
public class EventApiTests(PostgresFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task Draft_visibility_tenant_isolation_and_paging_are_enforced()
    {
        var (staff, org) = await SetupAsync();
        using var owner = staff;
        using var anonymous = Client();
        var draft = await EventAsync(owner, org.Id, open: false);
        Assert.Equal(EventStatus.DRAFT, draft.Status);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/events/{draft.Id}")).StatusCode);
        var publicList = await Read<PagedResponse<EventResponse>>(await anonymous.GetAsync($"/api/events?organizationId={org.Id}"));
        Assert.Empty(publicList.Items);
        var ownDrafts = await Read<PagedResponse<EventResponse>>(await owner.GetAsync($"/api/events?includeDrafts=true&organizationId={org.Id}"));
        Assert.Equal(draft.Id, Assert.Single(ownDrafts.Items).Id);
        // A staff member of another tenant cannot view the draft, list participants, edit, or transition it.
        var otherAdmin = await UserAsync();
        await OrganizationAsync(owner, otherAdmin.User.Id);
        using var other = Client(otherAdmin);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/events/{draft.Id}")).StatusCode);
        var otherDrafts = await Read<PagedResponse<EventResponse>>(await other.GetAsync($"/api/events?includeDrafts=true&organizationId={org.Id}"));
        Assert.Empty(otherDrafts.Items);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PutAsJsonAsync($"/api/events/{draft.Id}", UpdateInput(draft, 2), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ChangeStatus(other, draft, EventStatus.PUBLISHED)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync($"/api/events/{draft.Id}/registrations")).StatusCode);
        var published = await Transition(owner, draft, EventStatus.PUBLISHED);
        var filtered = await Read<PagedResponse<EventResponse>>(await anonymous.GetAsync(
            $"/api/events?organizationId={org.Id}&search=workshop&type=Workshop&status=PUBLISHED&page=1&pageSize=1"));
        Assert.Equal(published.Id, Assert.Single(filtered.Items).Id);
        Assert.Equal(1, filtered.TotalCount);
        Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.GetAsync("/api/events?pageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.GetAsync("/api/events?status=999")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/events?includeDrafts=true")).StatusCode);
    }

    [Fact]
    public async Task Date_capacity_version_and_terminal_states_are_validated()
    {
        var (staff, org) = await SetupAsync();
        using var owner = staff;
        var input = EventInput(org.Id);
        input.MaxCapacity = 0;
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/events", input, Json)).StatusCode);
        input.MaxCapacity = 1; input.EndTime = input.StartTime;
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/events", input, Json)).StatusCode);
        input.EndTime = input.StartTime.AddHours(1); input.StartTime = input.StartTime.ToOffset(TimeSpan.FromHours(2));
        var ev = await Read<EventResponse>(await owner.PostAsJsonAsync("/api/events", input, Json));
        Assert.Equal(input.StartTime.UtcDateTime, ev.StartTime);
        Assert.Equal(DateTimeKind.Utc, ev.StartTime.Kind);
        Assert.Equal(HttpStatusCode.Conflict, (await ChangeStatus(owner, ev, EventStatus.COMPLETED)).StatusCode);
        var published = await Transition(owner, ev, EventStatus.PUBLISHED);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PutAsJsonAsync($"/api/events/{ev.Id}", UpdateInput(ev, 2), Json)).StatusCode);
        var opened = await Transition(owner, published, EventStatus.REGISTRATION_OPEN);
        var completed = await Transition(owner, opened, EventStatus.COMPLETED);
        Assert.Equal(HttpStatusCode.Conflict, (await ChangeStatus(owner, completed, EventStatus.CANCELLED)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PutAsJsonAsync($"/api/events/{ev.Id}", UpdateInput(completed, 2), Json)).StatusCode);
    }

    [Fact]
    public async Task Two_editors_with_same_version_cannot_overwrite_each_other()
    {
        var (staff, org) = await SetupAsync();
        using var owner = staff;
        var ev = await EventAsync(owner, org.Id, open: false);
        var results = await Task.WhenAll(owner.PutAsJsonAsync($"/api/events/{ev.Id}", UpdateInput(ev, 2), Json),
            owner.PutAsJsonAsync($"/api/events/{ev.Id}", UpdateInput(ev, 3), Json));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
        var current = await Read<EventResponse>(await owner.GetAsync($"/api/events/{ev.Id}"));
        Assert.Equal(ev.Version + 1, current.Version);
    }

    [Fact]
    public async Task Swagger_exposes_all_workflows_with_dto_schemas_and_bearer_security()
    {
        using var client = Client();
        var response = await client.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = json.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/events/{id}/registrations", out _));
        Assert.True(paths.TryGetProperty("/api/auth/me", out var me));
        Assert.Equal("Bearer", json.RootElement.GetProperty("security")[0].EnumerateObject().Single().Name);
        Assert.False(me.GetProperty("get").TryGetProperty("security", out _)); // Inherits bearer requirement.
        Assert.Equal(0, paths.GetProperty("/api/events").GetProperty("get").GetProperty("security").GetArrayLength());
        Assert.True(json.RootElement.GetProperty("components").GetProperty("schemas").TryGetProperty("EventResponse", out _));
        foreach (var path in paths.EnumerateObject().Where(x => x.Name.StartsWith("/api/")))
        foreach (var operation in path.Value.EnumerateObject())
        {
            if (operation.Name is not ("get" or "post" or "put" or "patch" or "delete")) continue;
            var success = operation.Value.GetProperty("responses").EnumerateObject()
                .Where(x => x.Name.StartsWith('2')).ToArray();
            Assert.True(success.Length > 0, $"Missing success response: {operation.Name} {path.Name}");
            if (success[0].Name != "204") Assert.True(success[0].Value.TryGetProperty("content", out _),
                $"Missing success schema: {operation.Name} {path.Name}");
        }
    }
}
