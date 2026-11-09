using UbbEvents.Api.Data;
using UbbEvents.Api.Models;

namespace UbbEvents.Api.Services;

public sealed class AuditService(AppDbContext db)
{
    // Added to the same unit of work as the business change, never saved separately.
    public void Record(Guid actor, Guid? organization, string entityType, Guid entityId, string action, string? details = null)
        => db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actor, OrganizationId = organization, EntityType = entityType,
            EntityId = entityId, Action = action, Details = details
        });
}
