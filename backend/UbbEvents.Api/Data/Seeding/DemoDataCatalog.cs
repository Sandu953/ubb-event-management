using System.Security.Cryptography;
using System.Text;
using UbbEvents.Api.Models;

namespace UbbEvents.Api.Data.Seeding;

// Stable identities keep separate seed invocations idempotent without adding a seed-history table.
internal sealed record DemoDataCatalog(User[] Users, Organization[] Organizations,
    OrganizationMember[] Memberships, Event[] Events, Registration[] Registrations)
{
    internal static Guid Id(string key) => new(SHA256.HashData(Encoding.UTF8.GetBytes($"UbbEvents.Demo.v1:{key}"))[..16]);

    internal static DemoDataCatalog Create(DateTime now)
    {
        User Account(string key, string name, GlobalRole role = GlobalRole.STUDENT) => new()
        {
            Id = Id($"user:{key}"), Email = $"{key}@demo.ubbevents.test", FullName = name,
            GlobalRole = role, CreatedAt = now, UpdatedAt = now
        };
        var users = new[]
        {
            Account("global.admin", "Demo Global Administrator", GlobalRole.ADMIN),
            Account("tech.admin", "Andrei Pop (Demo)"), Account("arts.admin", "Ioana Muresan (Demo)"),
            Account("tech.organizer", "Radu Ionescu (Demo)"), Account("arts.organizer", "Mara Stan (Demo)"),
            Account("student.one", "Elena Popescu (Demo)"), Account("student.two", "Vlad Dumitrescu (Demo)"),
            Account("student.three", "Sofia Rusu (Demo)"), Account("student.four", "Mihai Marin (Demo)"),
            Account("student.five", "Ana Matei (Demo)")
        };
        var organizations = new[]
        {
            new Organization { Id = Id("org:tech"), Name = "Demo UBB Tech Society",
                Description = "Fictional development organization for technology workshops and conferences.", CreatedAt = now },
            new Organization { Id = Id("org:arts"), Name = "Demo UBB Arts Collective",
                Description = "Fictional development organization for culture, photography, and student meetups.", CreatedAt = now }
        };
        OrganizationMember Member(string user, string org, OrgRole role) => new()
        {
            UserId = Id($"user:{user}"), OrganizationId = Id($"org:{org}"), OrgRole = role
        };
        var memberships = new[]
        {
            Member("tech.admin", "tech", OrgRole.ORG_ADMIN), Member("arts.admin", "arts", OrgRole.ORG_ADMIN),
            Member("tech.organizer", "tech", OrgRole.ORGANIZER), Member("arts.organizer", "arts", OrgRole.ORGANIZER),
            Member("arts.admin", "tech", OrgRole.ORGANIZER), Member("tech.organizer", "arts", OrgRole.ORGANIZER)
        };
        Event Occasion(string key, string org, string title, string type, EventStatus status, int days, int capacity) => new()
        {
            Id = Id($"event:{key}"), OrganizationId = Id($"org:{org}"), CreatedBy = Id($"user:{org}.organizer"),
            Title = title, Type = type, Description = $"Development sample: {title}. Fictional event for exercising the application.",
            Venue = org == "tech" ? "UBB Campus - Demo Technology Room" : "UBB Campus - Demo Arts Studio",
            StartTime = now.Date.AddDays(days).AddHours(16), EndTime = now.Date.AddDays(days).AddHours(18),
            MaxCapacity = capacity, Status = status, Version = 1
        };
        var events = new[]
        {
            Occasion("planning", "tech", "Demo: Organizer planning workshop", "Workshop", EventStatus.DRAFT, 5, 20),
            Occasion("conference", "tech", "Demo: Campus technology conference", "Conference", EventStatus.PUBLISHED, 14, 80),
            Occasion("web-api", "tech", "Demo: Build your first web API", "Workshop", EventStatus.REGISTRATION_OPEN, 7, 10),
            Occasion("database-lab", "tech", "Demo: PostgreSQL concurrency lab", "Training", EventStatus.REGISTRATION_OPEN, 10, 2),
            Occasion("meetup", "arts", "Demo: Creative campus meetup", "Social", EventStatus.REGISTRATION_OPEN, 3, 12),
            Occasion("photo-walk", "arts", "Demo: Photography walk recap", "Workshop", EventStatus.COMPLETED, -7, 15),
            Occasion("film-night", "arts", "Demo: Student film evening", "Social", EventStatus.CANCELLED, 5, 25),
            Occasion("culture-night", "arts", "Demo: University culture night", "Conference", EventStatus.PUBLISHED, 21, 60)
        };
        Registration Participation(string occasion, string student, RegistrationStatus status, int order, bool checkedIn = false) => new()
        {
            Id = Id($"registration:{occasion}:{student}"), EventId = Id($"event:{occasion}"),
            UserId = Id($"user:student.{student}"), Status = status, CheckedIn = checkedIn,
            RegisteredAt = now.AddDays(occasion == "photo-walk" ? -9 : -2).AddMinutes(order)
        };
        var registrations = new[]
        {
            Participation("web-api", "one", RegistrationStatus.CONFIRMED, 1),
            Participation("web-api", "two", RegistrationStatus.CONFIRMED, 2),
            Participation("web-api", "three", RegistrationStatus.CANCELED, 3),
            Participation("database-lab", "one", RegistrationStatus.CONFIRMED, 1),
            Participation("database-lab", "two", RegistrationStatus.CONFIRMED, 2),
            Participation("database-lab", "three", RegistrationStatus.WAITLISTED, 3),
            Participation("database-lab", "four", RegistrationStatus.WAITLISTED, 4),
            Participation("database-lab", "five", RegistrationStatus.WAITLISTED, 5),
            Participation("meetup", "three", RegistrationStatus.CONFIRMED, 1, true),
            Participation("meetup", "four", RegistrationStatus.CONFIRMED, 2),
            Participation("meetup", "five", RegistrationStatus.CONFIRMED, 3),
            Participation("photo-walk", "one", RegistrationStatus.CONFIRMED, 1, true),
            Participation("photo-walk", "three", RegistrationStatus.CONFIRMED, 2, true),
            Participation("photo-walk", "five", RegistrationStatus.CONFIRMED, 3),
            Participation("film-night", "two", RegistrationStatus.CANCELED, 1),
            Participation("film-night", "four", RegistrationStatus.CANCELED, 2)
        };
        return new(users, organizations, memberships, events, registrations);
    }
}
