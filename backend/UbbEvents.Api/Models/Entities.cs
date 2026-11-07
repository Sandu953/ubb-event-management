namespace UbbEvents.Api.Models;

public enum GlobalRole { ADMIN, STUDENT }
public enum OrgRole { ORG_ADMIN, ORGANIZER }

public enum EventStatus
{
    DRAFT,
    PUBLISHED,
    REGISTRATION_OPEN,
    COMPLETED,
    CANCELLED
}

public enum RegistrationStatus
{
    CONFIRMED,
    WAITLISTED,
    CANCELED
}

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string FullName { get; set; } = "";
    public GlobalRole GlobalRole { get; set; } = GlobalRole.STUDENT;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class Organization
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class OrganizationMember
{
    public Guid UserId { get; set; }
    public Guid OrganizationId { get; set; }
    public OrgRole OrgRole { get; set; }
}

public class Event
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public string Title { get; set; } = "";
    public string Type { get; set; } = "";
    public string Description { get; set; } = "";
    public string Venue { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int MaxCapacity { get; set; }
    public EventStatus Status { get; set; } = EventStatus.DRAFT;
    public int Version { get; set; }
    public Guid CreatedBy { get; set; }
}

public class Registration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Guid UserId { get; set; }
    public RegistrationStatus Status { get; set; }
    public bool CheckedIn { get; set; } = false;
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
}