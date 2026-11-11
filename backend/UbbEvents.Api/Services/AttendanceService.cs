using Microsoft.EntityFrameworkCore;
using UbbEvents.Api.Data;
using UbbEvents.Api.DTOs;
using UbbEvents.Api.Infrastructure;
using UbbEvents.Api.Models;
using UbbEvents.Api.Repositories;

namespace UbbEvents.Api.Services;

public sealed class AttendanceService(AppDbContext db, OrganizationPermissions permissions,
    EventLockRepository locks, AuditService audit)
{
    public async Task<RegistrationResponse> CheckInAsync(Guid eventId, CheckInRequest request, CancellationToken ct)
    {
        RequestValidation.Nonempty(request.RegistrationId, nameof(request.RegistrationId));
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        var ev = await locks.LockAsync(eventId, ct);
        var actor = await permissions.RequireStaffAsync(ev.OrganizationId, ct);
        if (ev.Status != EventStatus.REGISTRATION_OPEN) throw ApiException.Conflict("Check-in requires an event with open registration.");
        var registration = await db.Registrations.SingleOrDefaultAsync(x => x.Id == request.RegistrationId && x.EventId == eventId, ct)
            ?? throw ApiException.NotFound("Registration");
        if (registration.Status != RegistrationStatus.CONFIRMED) throw ApiException.Conflict("Only confirmed participants can check in.");
        if (registration.CheckedIn) throw ApiException.Conflict("Participant is already checked in.");
        registration.CheckedIn = true;
        audit.Record(actor.Id, ev.OrganizationId, "Registration", registration.Id, "ParticipantCheckedIn");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return registration.ToResponse();
    }

    public async Task<AttendanceResponse> TotalsAsync(Guid eventId, CancellationToken ct)
    {
        var ev = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == eventId, ct) ?? throw ApiException.NotFound("Event");
        await permissions.RequireStaffAsync(ev.OrganizationId, ct);
        return await db.Registrations.Where(x => x.EventId == eventId).GroupBy(x => x.EventId)
            .Select(g => new AttendanceResponse(eventId,
                g.Count(x => x.Status == RegistrationStatus.CONFIRMED), g.Count(x => x.Status == RegistrationStatus.WAITLISTED),
                g.Count(x => x.Status == RegistrationStatus.CANCELED), g.Count(x => x.CheckedIn)))
            .SingleOrDefaultAsync(ct) ?? new AttendanceResponse(eventId, 0, 0, 0, 0);
    }
}
