using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using UbbEvents.Api.Data;
using UbbEvents.Api.DTOs;
using UbbEvents.Api.Models;

namespace UbbEvents.Api.Tests;

[Collection("PostgreSQL"), Trait("Category", "Integration")]
public class RegistrationTests(PostgresFixture fixture) : ApiTestBase(fixture)
{
    [Theory]
    [InlineData(EventStatus.DRAFT)]
    [InlineData(EventStatus.PUBLISHED)]
    [InlineData(EventStatus.COMPLETED)]
    [InlineData(EventStatus.CANCELLED)]
    public async Task Only_open_events_accept_registrations(EventStatus status)
    {
        var (staff, org) = await SetupAsync();
        using var owner = staff;
        var ev = await EventAsync(owner, org.Id, open: false);
        if (status != EventStatus.DRAFT) ev = await Transition(owner, ev, EventStatus.PUBLISHED);
        if (status == EventStatus.COMPLETED)
        {
            ev = await Transition(owner, ev, EventStatus.REGISTRATION_OPEN);
            ev = await Transition(owner, ev, EventStatus.COMPLETED);
        }
        else if (status == EventStatus.CANCELLED) ev = await Transition(owner, ev, EventStatus.CANCELLED);
        using var participant = Client(await UserAsync());
        Assert.Equal(HttpStatusCode.Conflict, (await Register(participant, ev.Id)).StatusCode);
        await using var db = new AppDbContext(Fixture.Options);
        Assert.False(await db.Registrations.AnyAsync(x => x.EventId == ev.Id));
    }

    [Fact]
    public async Task Confirm_waitlist_cancel_and_reactivate_preserve_history()
    {
        var (staff, org) = await SetupAsync();
        using var owner = staff;
        var ev = await EventAsync(owner, org.Id);
        using var first = Client(await UserAsync());
        using var second = Client(await UserAsync());
        var confirmed = await Read<RegistrationResponse>(await Register(first, ev.Id));
        var waiting = await Read<RegistrationResponse>(await Register(second, ev.Id));
        Assert.Equal(RegistrationStatus.CONFIRMED, confirmed.Status);
        Assert.Equal(RegistrationStatus.WAITLISTED, waiting.Status);
        Assert.Equal(HttpStatusCode.Conflict, (await Register(first, ev.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Cancel(first, ev.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Cancel(first, ev.Id)).StatusCode);
        var reactivated = await Read<RegistrationResponse>(await Register(first, ev.Id));
        Assert.Equal(confirmed.Id, reactivated.Id);
        Assert.Equal(RegistrationStatus.WAITLISTED, reactivated.Status);
        Assert.False(reactivated.CheckedIn);
        Assert.True(reactivated.RegisteredAt > confirmed.RegisteredAt);
        var history = await Read<PagedResponse<MyRegistrationResponse>>(await first.GetAsync("/api/registrations/me"));
        Assert.Equal(reactivated.Id, Assert.Single(history.Items).Id);
        Assert.Equal(HttpStatusCode.NoContent, (await Cancel(first, ev.Id)).StatusCode);
        var canceled = await Read<PagedResponse<MyRegistrationResponse>>(await first.GetAsync("/api/registrations/me?status=CANCELED"));
        Assert.Equal(RegistrationStatus.CANCELED, Assert.Single(canceled.Items).Status);
        await using var db = new AppDbContext(Fixture.Options);
        Assert.Equal(2, await db.Registrations.CountAsync(x => x.EventId == ev.Id));
        Assert.Equal(RegistrationStatus.CONFIRMED, (await db.Registrations.SingleAsync(x => x.Id == waiting.Id)).Status);
        Assert.True(await db.AuditLogs.AnyAsync(x => x.EntityId == confirmed.Id && x.Action == "RegistrationReactivated"));
    }

    [Fact]
    public async Task Waitlisted_cancellation_does_not_promote_and_confirmed_cancellation_without_queue_frees_place()
    {
        var (staff, org) = await SetupAsync();
        using var owner = staff;
        var ev = await EventAsync(owner, org.Id);
        using var first = Client(await UserAsync());
        using var second = Client(await UserAsync());
        using var third = Client(await UserAsync());
        await Read<RegistrationResponse>(await Register(first, ev.Id));
        var secondReg = await Read<RegistrationResponse>(await Register(second, ev.Id));
        var thirdReg = await Read<RegistrationResponse>(await Register(third, ev.Id));
        Assert.Equal(HttpStatusCode.NoContent, (await Cancel(second, ev.Id)).StatusCode);
        await using (var db = new AppDbContext(Fixture.Options))
        {
            Assert.Equal(RegistrationStatus.CANCELED, (await db.Registrations.SingleAsync(x => x.Id == secondReg.Id)).Status);
            Assert.Equal(RegistrationStatus.WAITLISTED, (await db.Registrations.SingleAsync(x => x.Id == thirdReg.Id)).Status);
        }
        Assert.Equal(HttpStatusCode.NoContent, (await Cancel(third, ev.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Cancel(first, ev.Id)).StatusCode);
        var back = await Read<RegistrationResponse>(await Register(second, ev.Id));
        Assert.Equal(RegistrationStatus.CONFIRMED, back.Status);
    }

    [Fact]
    public async Task Promotion_is_fifo_with_uuid_tiebreak_and_capacity_increase_promotes_queue()
    {
        var (staff, org) = await SetupAsync();
        using var owner = staff;
        var ev = await EventAsync(owner, org.Id);
        using var first = Client(await UserAsync());
        using var second = Client(await UserAsync());
        using var third = Client(await UserAsync());
        await Read<RegistrationResponse>(await Register(first, ev.Id));
        var secondReg = await Read<RegistrationResponse>(await Register(second, ev.Id));
        var thirdReg = await Read<RegistrationResponse>(await Register(third, ev.Id));
        var sharedTime = DateTime.UtcNow.AddMinutes(-1);
        await using (var db = new AppDbContext(Fixture.Options))
            await db.Registrations.Where(x => x.Id == secondReg.Id || x.Id == thirdReg.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.RegisteredAt, sharedTime));
        Guid firstWaiting;
        await using (var db = new AppDbContext(Fixture.Options))
            firstWaiting = await db.Registrations.Where(x => x.EventId == ev.Id && x.Status == RegistrationStatus.WAITLISTED)
                .OrderBy(x => x.RegisteredAt).ThenBy(x => x.Id).Select(x => x.Id).FirstAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await Cancel(first, ev.Id)).StatusCode);
        await using (var db = new AppDbContext(Fixture.Options))
            Assert.Equal(firstWaiting, (await db.Registrations.SingleAsync(x => x.EventId == ev.Id && x.Status == RegistrationStatus.CONFIRMED)).Id);
        ev = await Read<EventResponse>(await owner.PutAsJsonAsync($"/api/events/{ev.Id}", UpdateInput(ev, 2), Json));
        await using (var db = new AppDbContext(Fixture.Options))
        {
            Assert.Equal(2, await db.Registrations.CountAsync(x => x.EventId == ev.Id && x.Status == RegistrationStatus.CONFIRMED));
            Assert.False(await db.Registrations.AnyAsync(x => x.EventId == ev.Id && x.Status == RegistrationStatus.WAITLISTED));
        }
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PutAsJsonAsync($"/api/events/{ev.Id}", UpdateInput(ev, 1), Json)).StatusCode);
    }

    [Fact]
    public async Task Event_cancellation_cancels_active_rows_without_deleting_history_or_promoting()
    {
        var (staff, org) = await SetupAsync();
        using var owner = staff;
        var ev = await EventAsync(owner, org.Id);
        using var first = Client(await UserAsync());
        using var second = Client(await UserAsync());
        await Read<RegistrationResponse>(await Register(first, ev.Id));
        await Read<RegistrationResponse>(await Register(second, ev.Id));
        await Transition(owner, ev, EventStatus.CANCELLED);
        await using var db = new AppDbContext(Fixture.Options);
        var rows = await db.Registrations.Where(x => x.EventId == ev.Id).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, x => Assert.Equal(RegistrationStatus.CANCELED, x.Status));
        Assert.Equal(2, await db.AuditLogs.CountAsync(x => x.OrganizationId == org.Id && x.Action == "EventCancellation"));
        Assert.Equal(HttpStatusCode.NoContent, (await Cancel(first, ev.Id)).StatusCode);
    }
}
