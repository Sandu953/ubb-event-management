using UbbEvents.Api.Infrastructure;
using UbbEvents.Api.Models;
using UbbEvents.Api.Services;

namespace UbbEvents.Api.Tests;

public class EventLifecycleTests
{
    public static IEnumerable<object[]> AllTransitions() =>
        from fromState in Enum.GetValues<EventStatus>()
        from toState in Enum.GetValues<EventStatus>()
        select new object[] { fromState, toState };

    [Theory, MemberData(nameof(AllTransitions))]
    public void Only_documented_transitions_are_allowed(EventStatus from, EventStatus to)
    {
        var allowed = new HashSet<(EventStatus, EventStatus)>
        {
            (EventStatus.DRAFT, EventStatus.PUBLISHED),
            (EventStatus.PUBLISHED, EventStatus.REGISTRATION_OPEN),
            (EventStatus.REGISTRATION_OPEN, EventStatus.COMPLETED),
            (EventStatus.PUBLISHED, EventStatus.CANCELLED),
            (EventStatus.REGISTRATION_OPEN, EventStatus.CANCELLED)
        };
        var lifecycle = new EventLifecycle();
        if (allowed.Contains((from, to))) lifecycle.EnsureTransition(from, to);
        else Assert.Equal(409, Assert.Throws<ApiException>(() => lifecycle.EnsureTransition(from, to)).StatusCode);
    }

    [Fact]
    public void New_event_starts_in_draft() => Assert.Equal(EventStatus.DRAFT, new Event().Status);
}
