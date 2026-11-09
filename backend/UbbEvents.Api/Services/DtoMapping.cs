using UbbEvents.Api.DTOs;
using UbbEvents.Api.Models;

namespace UbbEvents.Api.Services;

public static class DtoMapping
{
    public static UserResponse ToResponse(this User u) => new(u.Id, u.Email, u.FullName, u.GlobalRole);
    public static OrganizationResponse ToResponse(this Organization o) => new(o.Id, o.Name, o.Description, o.CreatedAt);
    public static EventResponse ToResponse(this Event e) => new(e.Id, e.OrganizationId, e.Title, e.Type,
        e.Description, e.Venue, e.StartTime, e.EndTime, e.MaxCapacity, e.Status, e.Version, e.CreatedBy);
    public static RegistrationResponse ToResponse(this Registration r) => new(r.Id, r.EventId, r.UserId,
        r.Status, r.CheckedIn, r.RegisteredAt);
}
