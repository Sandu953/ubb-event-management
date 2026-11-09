using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using UbbEvents.Api.Models;

namespace UbbEvents.Api.DTOs;

public record HealthResponse(string Status);

public record RegisterRequest(
    [Required, EmailAddress, StringLength(254)] string Email,
    [Required, StringLength(128, MinimumLength = 12)] string Password,
    [Required, StringLength(200)] string FullName);
public record LoginRequest([Required, EmailAddress, StringLength(254)] string Email,
    [Required, StringLength(128)] string Password);
public record UserResponse(Guid Id, string Email, string FullName, GlobalRole GlobalRole);
public record AuthResponse(string AccessToken, DateTime ExpiresAt, UserResponse User);
public record MembershipResponse(Guid OrganizationId, string OrganizationName, OrgRole OrgRole);
public record CurrentUserResponse(UserResponse User, IReadOnlyList<MembershipResponse> Memberships);

public record CreateOrganizationRequest([Required, StringLength(200)] string Name,
    [Required, StringLength(4000)] string Description, Guid InitialAdminUserId);
public record OrganizationResponse(Guid Id, string Name, string Description, DateTime CreatedAt);
public record AddMemberRequest([property: JsonRequired] Guid UserId,
    [property: JsonRequired] [EnumDataType(typeof(OrgRole))] OrgRole OrgRole);
public record UpdateMemberRequest([property: JsonRequired] [EnumDataType(typeof(OrgRole))] OrgRole OrgRole);
public record MemberResponse(Guid UserId, string Email, string FullName, OrgRole OrgRole);

public class PagingQuery
{
    [Range(1, 1000000)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
}
public record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public class EventQuery : PagingQuery, IValidatableObject
{
    public Guid? OrganizationId { get; set; }
    [StringLength(200)] public string? Search { get; set; }
    [StringLength(100)] public string? Type { get; set; }
    [EnumDataType(typeof(EventStatus))] public EventStatus? Status { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public bool IncludeDrafts { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (From > To) yield return new ValidationResult("From must be before To.", [nameof(From)]);
    }
}

public class EventDetailsRequest : IValidatableObject
{
    [Required, StringLength(200)] public string Title { get; set; } = "";
    [Required, StringLength(100)] public string Type { get; set; } = "";
    [Required, StringLength(10000)] public string Description { get; set; } = "";
    [Required, StringLength(500)] public string Venue { get; set; } = "";
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    [Range(1, int.MaxValue)] public int MaxCapacity { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartTime == default || EndTime <= StartTime)
            yield return new ValidationResult("EndTime must be later than a nonempty StartTime.", [nameof(EndTime)]);
    }
}
public class CreateEventRequest : EventDetailsRequest
{
    public Guid OrganizationId { get; set; }
}
public class UpdateEventRequest : EventDetailsRequest
{
    [Range(1, int.MaxValue)] public int Version { get; set; }
}
public record ChangeEventStatusRequest([property: JsonRequired] [EnumDataType(typeof(EventStatus))] EventStatus Status,
    [property: JsonRequired] [Range(1, int.MaxValue)] int Version);
public record EventResponse(Guid Id, Guid OrganizationId, string Title, string Type, string Description,
    string Venue, DateTime StartTime, DateTime EndTime, int MaxCapacity, EventStatus Status, int Version, Guid CreatedBy);

public class RegistrationQuery : PagingQuery
{
    [EnumDataType(typeof(RegistrationStatus))] public RegistrationStatus? Status { get; set; }
}
public record RegistrationResponse(Guid Id, Guid EventId, Guid UserId, RegistrationStatus Status,
    bool CheckedIn, DateTime RegisteredAt);
public record MyRegistrationResponse(Guid Id, Guid EventId, string EventTitle, EventStatus EventStatus,
    DateTime StartTime, string Venue, RegistrationStatus Status, bool CheckedIn, DateTime RegisteredAt);
public record ParticipantResponse(Guid RegistrationId, Guid UserId, string FullName, string Email,
    RegistrationStatus Status, bool CheckedIn, DateTime RegisteredAt);
public record CheckInRequest(Guid RegistrationId);
public record AttendanceResponse(Guid EventId, int Confirmed, int Waitlisted, int Canceled, int CheckedIn);
