using Microsoft.EntityFrameworkCore;
using UbbEvents.Api.Data;
using UbbEvents.Api.Models;

namespace UbbEvents.Api.Services;

public sealed class WaitlistService(AppDbContext db, AuditService audit)
{
    // Caller must hold the parent event row lock. Database counts do not include pending tracked changes.
    public async Task PromoteAsync(Event ev, Guid actor, int places, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Promotion requires a transaction.");
        if (ev.Status != EventStatus.REGISTRATION_OPEN || places <= 0) return;
        var waiting = await db.Registrations.Where(x => x.EventId == ev.Id && x.Status == RegistrationStatus.WAITLISTED)
            .OrderBy(x => x.RegisteredAt).ThenBy(x => x.Id).Take(places).ToListAsync(ct);
        foreach (var registration in waiting)
        {
            registration.Status = RegistrationStatus.CONFIRMED;
            audit.Record(actor, ev.OrganizationId, "Registration", registration.Id, "WaitlistPromoted", "WAITLISTED -> CONFIRMED");
        }
    }
}
