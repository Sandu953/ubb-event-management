namespace UbbEvents.Api.Models;

public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ActorUserId { get; set; }
    public Guid? OrganizationId { get; set; }
    public string EntityType { get; set; } = "";
    public Guid EntityId { get; set; }
    public string Action { get; set; } = "";
    public string? Details { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
