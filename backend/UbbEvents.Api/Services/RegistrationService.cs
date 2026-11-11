using Microsoft.EntityFrameworkCore;
using UbbEvents.Api.Data;
using UbbEvents.Api.DTOs;
using UbbEvents.Api.Infrastructure;
using UbbEvents.Api.Models;
using UbbEvents.Api.Repositories;

namespace UbbEvents.Api.Services;

public sealed class RegistrationService(AppDbContext db, CurrentUser currentUser, OrganizationPermissions permissions,
    EventLockRepository locks, WaitlistService waitlist, AuditService audit)
{
    public async Task<RegistrationResponse> RegisterAsync(Guid eventId, CancellationToken ct)
    {
        var user = await currentUser.RequireAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        var ev = await locks.LockAsync(eventId, ct);
        if (ev.Status != EventStatus.REGISTRATION_OPEN) throw ApiException.Conflict("Event registration is not open.");
        var registration = await db.Registrations.SingleOrDefaultAsync(x => x.EventId == eventId && x.UserId == user.Id, ct);
        if (registration is not null && registration.Status != RegistrationStatus.CANCELED)
            throw ApiException.Conflict("You already have an active registration for this event.");
        var confirmed = await db.Registrations.CountAsync(x => x.EventId == eventId && x.Status == RegistrationStatus.CONFIRMED, ct);
        var reactivated = registration is not null;
        if (registration is null)
        {
            registration = new Registration { EventId = eventId, UserId = user.Id };
            db.Registrations.Add(registration);
        }
        registration.Status = confirmed < ev.MaxCapacity ? RegistrationStatus.CONFIRMED : RegistrationStatus.WAITLISTED;
        registration.CheckedIn = false;
        registration.RegisteredAt = DateTime.UtcNow;
        audit.Record(user.Id, ev.OrganizationId, "Registration", registration.Id,
            reactivated ? "RegistrationReactivated" : "RegistrationCreated", registration.Status.ToString());
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return registration.ToResponse();
    }

    public async Task CancelAsync(Guid eventId, CancellationToken ct)
    {
        var user = await currentUser.RequireAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        var ev = await locks.LockAsync(eventId, ct);
        var registration = await db.Registrations.SingleOrDefaultAsync(x => x.EventId == eventId && x.UserId == user.Id, ct)
            ?? throw ApiException.NotFound("Registration");
        if (registration.Status == RegistrationStatus.CANCELED) { await tx.CommitAsync(ct); return; }
        if (ev.Status == EventStatus.COMPLETED) throw ApiException.Conflict("Registrations for a completed event cannot be canceled.");
        if (registration.CheckedIn) throw ApiException.Conflict("A checked-in registration cannot be canceled by the participant.");
        var previous = registration.Status;
        registration.Status = RegistrationStatus.CANCELED;
        audit.Record(user.Id, ev.OrganizationId, "Registration", registration.Id, "RegistrationCanceled", $"{previous} -> CANCELED");
        if (previous == RegistrationStatus.CONFIRMED) await waitlist.PromoteAsync(ev, user.Id, 1, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<PagedResponse<MyRegistrationResponse>> MineAsync(RegistrationQuery query, CancellationToken ct)
    {
        RequestValidation.Ensure(query);
        var user = await currentUser.RequireAsync(ct);
        var registrations = from r in db.Registrations.AsNoTracking()
            join e in db.Events on r.EventId equals e.Id
            where r.UserId == user.Id && (query.Status == null || r.Status == query.Status)
            select new { Registration = r, Event = e };
        var total = await registrations.CountAsync(ct);
        var items = await registrations.OrderByDescending(x => x.Registration.RegisteredAt).ThenBy(x => x.Registration.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new MyRegistrationResponse(x.Registration.Id, x.Event.Id, x.Event.Title, x.Event.Status,
                x.Event.StartTime, x.Event.Venue, x.Registration.Status, x.Registration.CheckedIn, x.Registration.RegisteredAt)).ToListAsync(ct);
        return new(items, query.Page, query.PageSize, total);
    }

    public async Task<PagedResponse<ParticipantResponse>> ParticipantsAsync(Guid eventId, RegistrationQuery query, CancellationToken ct)
    {
        RequestValidation.Ensure(query);
        var ev = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == eventId, ct) ?? throw ApiException.NotFound("Event");
        await permissions.RequireStaffAsync(ev.OrganizationId, ct);
        var participants = from r in db.Registrations.AsNoTracking()
            join u in db.Users on r.UserId equals u.Id
            where r.EventId == eventId && (query.Status == null || r.Status == query.Status)
            select new { Registration = r, User = u };
        var total = await participants.CountAsync(ct);
        var items = await participants.OrderBy(x => x.Registration.RegisteredAt).ThenBy(x => x.Registration.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new ParticipantResponse(x.Registration.Id, x.User.Id, x.User.FullName, x.User.Email,
                x.Registration.Status, x.Registration.CheckedIn, x.Registration.RegisteredAt)).ToListAsync(ct);
        return new(items, query.Page, query.PageSize, total);
    }
}
