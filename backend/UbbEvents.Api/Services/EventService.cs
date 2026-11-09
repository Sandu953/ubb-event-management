using Microsoft.EntityFrameworkCore;
using UbbEvents.Api.Data;
using UbbEvents.Api.DTOs;
using UbbEvents.Api.Infrastructure;
using UbbEvents.Api.Models;
using UbbEvents.Api.Repositories;

namespace UbbEvents.Api.Services;

public sealed class EventService(AppDbContext db, CurrentUser currentUser, OrganizationPermissions permissions,
    EventLockRepository locks, EventLifecycle lifecycle, WaitlistService waitlist, AuditService audit)
{
    public async Task<PagedResponse<EventResponse>> ListAsync(EventQuery query, CancellationToken ct)
    {
        RequestValidation.Ensure(query);
        var events = db.Events.AsNoTracking().AsQueryable();
        if (query.IncludeDrafts)
        {
            var user = await currentUser.RequireAsync(ct);
            if (user.GlobalRole != GlobalRole.ADMIN)
                events = events.Where(e => e.Status != EventStatus.DRAFT || db.OrganizationMembers.Any(
                    m => m.UserId == user.Id && m.OrganizationId == e.OrganizationId));
        }
        else events = events.Where(x => x.Status != EventStatus.DRAFT);
        if (query.OrganizationId is { } org) events = events.Where(x => x.OrganizationId == org);
        if (query.Status is { } status) events = events.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(query.Type)) events = events.Where(x => x.Type == query.Type.Trim());
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            events = events.Where(x => x.Title.ToLower().Contains(term) || x.Description.ToLower().Contains(term) || x.Venue.ToLower().Contains(term));
        }
        if (query.From is { } from) events = events.Where(x => x.StartTime >= from.UtcDateTime);
        if (query.To is { } to) events = events.Where(x => x.StartTime <= to.UtcDateTime);
        var count = await events.CountAsync(ct);
        var items = await events.OrderBy(x => x.StartTime).ThenBy(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        return new(items.Select(x => x.ToResponse()).ToList(), query.Page, query.PageSize, count);
    }

    public async Task<EventResponse> GetAsync(Guid id, CancellationToken ct)
    {
        var ev = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiException.NotFound("Event");
        if (ev.Status == EventStatus.DRAFT && !await permissions.CanManageEventsAsync(ev.OrganizationId, ct))
            throw ApiException.NotFound("Event");
        return ev.ToResponse();
    }

    public async Task<EventResponse> CreateAsync(CreateEventRequest request, CancellationToken ct)
    {
        RequestValidation.Ensure(request);
        RequestValidation.Nonempty(request.OrganizationId, nameof(request.OrganizationId));
        var actor = await permissions.RequireStaffAsync(request.OrganizationId, ct);
        if (!await db.Organizations.AnyAsync(x => x.Id == request.OrganizationId, ct)) throw ApiException.NotFound("Organization");
        var ev = new Event { OrganizationId = request.OrganizationId, CreatedBy = actor.Id, Status = EventStatus.DRAFT, Version = 1 };
        ApplyDetails(ev, request);
        db.Events.Add(ev);
        audit.Record(actor.Id, ev.OrganizationId, "Event", ev.Id, "EventCreated", "DRAFT");
        await db.SaveChangesAsync(ct);
        return ev.ToResponse();
    }

    public async Task<EventResponse> UpdateAsync(Guid id, UpdateEventRequest request, CancellationToken ct)
    {
        RequestValidation.Ensure(request);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        var ev = await locks.LockAsync(id, ct);
        var actor = await permissions.RequireStaffAsync(ev.OrganizationId, ct);
        EnsureVersion(ev, request.Version);
        if (ev.Status is EventStatus.COMPLETED or EventStatus.CANCELLED)
            throw ApiException.Conflict("Completed or cancelled events cannot be edited.");
        var confirmed = await db.Registrations.CountAsync(x => x.EventId == id && x.Status == RegistrationStatus.CONFIRMED, ct);
        if (request.MaxCapacity < confirmed) throw ApiException.Conflict("Capacity cannot be lower than confirmed registrations.");
        ApplyDetails(ev, request);
        ev.Version++;
        await waitlist.PromoteAsync(ev, actor.Id, ev.MaxCapacity - confirmed, ct);
        audit.Record(actor.Id, ev.OrganizationId, "Event", ev.Id, "EventUpdated", $"Version {ev.Version}");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ev.ToResponse();
    }

    public async Task<EventResponse> ChangeStatusAsync(Guid id, ChangeEventStatusRequest request, CancellationToken ct)
    {
        RequestValidation.Ensure(request);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        var ev = await locks.LockAsync(id, ct);
        var actor = await permissions.RequireStaffAsync(ev.OrganizationId, ct);
        EnsureVersion(ev, request.Version);
        lifecycle.EnsureTransition(ev.Status, request.Status);
        var previous = ev.Status;
        ev.Status = request.Status;
        ev.Version++;
        if (ev.Status == EventStatus.CANCELLED)
        {
            var active = await db.Registrations.Where(x => x.EventId == id && x.Status != RegistrationStatus.CANCELED).ToListAsync(ct);
            foreach (var registration in active)
            {
                audit.Record(actor.Id, ev.OrganizationId, "Registration", registration.Id, "EventCancellation",
                    $"{registration.Status} -> CANCELED; CheckedIn={registration.CheckedIn}");
                registration.Status = RegistrationStatus.CANCELED;
                registration.CheckedIn = false;
            }
        }
        audit.Record(actor.Id, ev.OrganizationId, "Event", id, "EventStatusChanged", $"{previous} -> {ev.Status}");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ev.ToResponse();
    }

    private static void EnsureVersion(Event ev, int version)
    {
        if (ev.Version != version) throw ApiException.Conflict("The event changed. Reload it and submit the current version.");
    }

    private static void ApplyDetails(Event ev, EventDetailsRequest request)
    {
        ev.Title = request.Title.Trim(); ev.Type = request.Type.Trim(); ev.Description = request.Description.Trim();
        ev.Venue = request.Venue.Trim(); ev.StartTime = request.StartTime.UtcDateTime;
        ev.EndTime = request.EndTime.UtcDateTime; ev.MaxCapacity = request.MaxCapacity;
    }
}
