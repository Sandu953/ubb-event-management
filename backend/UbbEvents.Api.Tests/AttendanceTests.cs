using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using UbbEvents.Api.Data;
using UbbEvents.Api.DTOs;
using UbbEvents.Api.Models;

namespace UbbEvents.Api.Tests;

[Collection("PostgreSQL"), Trait("Category", "Integration")]
public class AttendanceTests(PostgresFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task Only_authorized_staff_can_check_in_confirmed_participants_once()
    {
        var (staff, org) = await SetupAsync();
        using var owner = staff;
        var ev = await EventAsync(owner, org.Id);
        var organizer = await UserAsync();
        var outsider = await UserAsync();
        await Read<MemberResponse>(await owner.PostAsJsonAsync($"/api/organizations/{org.Id}/members",
            new AddMemberRequest(organizer.User.Id, OrgRole.ORGANIZER), Json));
        await OrganizationAsync(owner, outsider.User.Id);
        using var allowed = Client(organizer);
        using var forbidden = Client(outsider);
        using var first = Client(await UserAsync());
        using var second = Client(await UserAsync());
        var confirmed = await Read<RegistrationResponse>(await Register(first, ev.Id));
        var waiting = await Read<RegistrationResponse>(await Register(second, ev.Id));
        Assert.Equal(HttpStatusCode.Forbidden, (await forbidden.PostAsJsonAsync($"/api/events/{ev.Id}/check-in", new CheckInRequest(confirmed.Id), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await first.GetAsync($"/api/events/{ev.Id}/attendance")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await allowed.PostAsJsonAsync($"/api/events/{ev.Id}/check-in", new CheckInRequest(waiting.Id), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await allowed.PostAsJsonAsync($"/api/events/{ev.Id}/check-in", new CheckInRequest(Guid.NewGuid()), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Cancel(second, ev.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await allowed.PostAsJsonAsync($"/api/events/{ev.Id}/check-in", new CheckInRequest(waiting.Id), Json)).StatusCode);
        var requests = await Task.WhenAll(
            allowed.PostAsJsonAsync($"/api/events/{ev.Id}/check-in", new CheckInRequest(confirmed.Id), Json),
            allowed.PostAsJsonAsync($"/api/events/{ev.Id}/check-in", new CheckInRequest(confirmed.Id), Json));
        Assert.Single(requests, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(requests, x => x.StatusCode == HttpStatusCode.Conflict);
        var totals = await Read<AttendanceResponse>(await allowed.GetAsync($"/api/events/{ev.Id}/attendance"));
        Assert.Equal(1, totals.CheckedIn);
        Assert.Equal(1, totals.Confirmed);
        Assert.Equal(0, totals.Waitlisted);
        Assert.Equal(1, totals.Canceled);
        Assert.Equal(HttpStatusCode.Conflict, (await Cancel(first, ev.Id)).StatusCode);
        var participants = await Read<PagedResponse<ParticipantResponse>>(await allowed.GetAsync($"/api/events/{ev.Id}/registrations?status=CONFIRMED"));
        Assert.True(Assert.Single(participants.Items).CheckedIn);
        var completed = await Transition(allowed, ev, EventStatus.COMPLETED);
        Assert.Equal(HttpStatusCode.Conflict, (await allowed.PostAsJsonAsync($"/api/events/{ev.Id}/check-in", new CheckInRequest(confirmed.Id), Json)).StatusCode);
        await using var db = new AppDbContext(Fixture.Options);
        Assert.Equal(EventStatus.COMPLETED, completed.Status);
        Assert.Equal(1, await db.AuditLogs.CountAsync(x => x.EntityId == confirmed.Id && x.Action == "ParticipantCheckedIn"));
    }
}
