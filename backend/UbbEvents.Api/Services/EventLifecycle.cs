using UbbEvents.Api.Infrastructure;
using UbbEvents.Api.Models;

namespace UbbEvents.Api.Services;

public sealed class EventLifecycle
{
    public bool CanTransition(EventStatus from, EventStatus to) => (from, to) is
        (EventStatus.DRAFT, EventStatus.PUBLISHED) or
        (EventStatus.PUBLISHED, EventStatus.REGISTRATION_OPEN) or
        (EventStatus.REGISTRATION_OPEN, EventStatus.COMPLETED) or
        (EventStatus.PUBLISHED, EventStatus.CANCELLED) or
        (EventStatus.REGISTRATION_OPEN, EventStatus.CANCELLED);

    public void EnsureTransition(EventStatus from, EventStatus to)
    {
        if (!CanTransition(from, to)) throw ApiException.Conflict($"Cannot transition from {from} to {to}.");
    }
}
