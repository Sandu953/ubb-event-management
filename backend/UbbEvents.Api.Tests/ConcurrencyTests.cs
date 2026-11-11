using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using UbbEvents.Api.Data;
using UbbEvents.Api.DTOs;
using UbbEvents.Api.Models;

namespace UbbEvents.Api.Tests;

[Collection("PostgreSQL"), Trait("Category", "Integration")]
public class ConcurrencyTests(PostgresFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task Event_lock_timeout_returns_nonleaking_conflict_without_partial_registration()
    {
        var (staff, org) = await SetupAsync();
        using var owner = staff;
        var ev = await EventAsync(owner, org.Id);
        using var participant = Client(await UserAsync());
        await using var locker = new AppDbContext(Fixture.Options);
        await using var tx = await locker.Database.BeginTransactionAsync();
        await locker.Events.FromSqlInterpolated($"SELECT * FROM \"Events\" WHERE \"Id\" = {ev.Id} FOR UPDATE").SingleAsync();
        var response = await Register(participant, ev.Id).WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("SELECT", body);
        Assert.DoesNotContain("Npgsql", body);
        await tx.CommitAsync();
        await using var verify = new AppDbContext(Fixture.Options);
        Assert.False(await verify.Registrations.AnyAsync(x => x.EventId == ev.Id));
        Assert.False(await verify.AuditLogs.AnyAsync(x => x.OrganizationId == org.Id && x.EntityType == "Registration"));
    }

    [Fact]
    public async Task Concurrent_requests_wait_for_event_lock_and_cannot_overbook_final_place()
    {
        var (staff, org) = await SetupAsync();
        using var owner = staff;
        var ev = await EventAsync(owner, org.Id);
        using var first = Client(await UserAsync());
        using var second = Client(await UserAsync());
        await using var locker = new AppDbContext(Fixture.Options);
        await using var tx = await locker.Database.BeginTransactionAsync();
        await locker.Events.FromSqlInterpolated($"SELECT * FROM \"Events\" WHERE \"Id\" = {ev.Id} FOR UPDATE").SingleAsync();
        var requests = new[] { Register(first, ev.Id), Register(second, ev.Id) };
        await Task.Delay(150); // Deliberately hold the real PostgreSQL row lock across both requests.
        Assert.All(requests, request => Assert.False(request.IsCompleted));
        await tx.CommitAsync();
        var responses = await Task.WhenAll(requests).WaitAsync(TimeSpan.FromSeconds(20));
        var results = await Task.WhenAll(responses.Select(Read<RegistrationResponse>));
        Assert.Single(results, x => x.Status == RegistrationStatus.CONFIRMED);
        Assert.Single(results, x => x.Status == RegistrationStatus.WAITLISTED);
        await using var db = new AppDbContext(Fixture.Options);
        Assert.Equal(1, await db.Registrations.CountAsync(x => x.EventId == ev.Id && x.Status == RegistrationStatus.CONFIRMED));
    }

    [Fact]
    public async Task Simultaneous_duplicate_registration_creates_only_one_row()
    {
        var (staff, org) = await SetupAsync();
        using var owner = staff;
        var ev = await EventAsync(owner, org.Id);
        using var participant = Client(await UserAsync());
        var results = await Task.WhenAll(Register(participant, ev.Id), Register(participant, ev.Id));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
        await using var db = new AppDbContext(Fixture.Options);
        Assert.Equal(1, await db.Registrations.CountAsync(x => x.EventId == ev.Id));
    }

    [Fact]
    public async Task Concurrent_confirmed_cancellations_promote_each_waiter_once()
    {
        var (staff, org) = await SetupAsync();
        using var owner = staff;
        var ev = await EventAsync(owner, org.Id, 2);
        using var first = Client(await UserAsync());
        using var second = Client(await UserAsync());
        using var third = Client(await UserAsync());
        using var fourth = Client(await UserAsync());
        await Read<RegistrationResponse>(await Register(first, ev.Id));
        await Read<RegistrationResponse>(await Register(second, ev.Id));
        var thirdReg = await Read<RegistrationResponse>(await Register(third, ev.Id));
        var fourthReg = await Read<RegistrationResponse>(await Register(fourth, ev.Id));
        var results = await Task.WhenAll(Cancel(first, ev.Id), Cancel(second, ev.Id));
        Assert.All(results, result => Assert.Equal(HttpStatusCode.NoContent, result.StatusCode));
        await using var db = new AppDbContext(Fixture.Options);
        var rows = await db.Registrations.Where(x => x.EventId == ev.Id).ToListAsync();
        Assert.Equal(2, rows.Count(x => x.Status == RegistrationStatus.CONFIRMED));
        Assert.Equal(2, rows.Count(x => x.Status == RegistrationStatus.CANCELED));
        Assert.All(rows.Where(x => x.Id == thirdReg.Id || x.Id == fourthReg.Id), x => Assert.Equal(RegistrationStatus.CONFIRMED, x.Status));
        Assert.Equal(2, await db.AuditLogs.CountAsync(x => x.OrganizationId == org.Id && x.Action == "WaitlistPromoted"));
    }

    [Fact]
    public async Task Concurrent_duplicate_cancellation_is_idempotent_and_promotes_only_one()
    {
        var (staff, org) = await SetupAsync();
        using var owner = staff;
        var ev = await EventAsync(owner, org.Id);
        using var first = Client(await UserAsync());
        using var second = Client(await UserAsync());
        using var third = Client(await UserAsync());
        await Read<RegistrationResponse>(await Register(first, ev.Id));
        var waiting = await Read<RegistrationResponse>(await Register(second, ev.Id));
        var last = await Read<RegistrationResponse>(await Register(third, ev.Id));
        var results = await Task.WhenAll(Cancel(first, ev.Id), Cancel(first, ev.Id));
        Assert.All(results, x => Assert.Equal(HttpStatusCode.NoContent, x.StatusCode));
        await using var db = new AppDbContext(Fixture.Options);
        Assert.Equal(RegistrationStatus.CONFIRMED, (await db.Registrations.SingleAsync(x => x.Id == waiting.Id)).Status);
        Assert.Equal(RegistrationStatus.WAITLISTED, (await db.Registrations.SingleAsync(x => x.Id == last.Id)).Status);
        Assert.Equal(1, await db.AuditLogs.CountAsync(x => x.OrganizationId == org.Id && x.Action == "WaitlistPromoted"));
    }

    [Fact]
    public async Task Registration_racing_capacity_reduction_never_exceeds_committed_capacity()
    {
        var (staff, org) = await SetupAsync();
        using var owner = staff;
        var ev = await EventAsync(owner, org.Id, 2);
        using var first = Client(await UserAsync());
        using var second = Client(await UserAsync());
        await Read<RegistrationResponse>(await Register(first, ev.Id));
        var register = Register(second, ev.Id);
        var update = owner.PutAsJsonAsync($"/api/events/{ev.Id}", UpdateInput(ev, 1), Json);
        await Read<RegistrationResponse>(await register);
        Assert.Contains((await update).StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
        await using var db = new AppDbContext(Fixture.Options);
        var current = await db.Events.SingleAsync(x => x.Id == ev.Id);
        Assert.True(await db.Registrations.CountAsync(x => x.EventId == ev.Id && x.Status == RegistrationStatus.CONFIRMED) <= current.MaxCapacity);
    }
}
