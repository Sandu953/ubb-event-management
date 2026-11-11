using Microsoft.EntityFrameworkCore;
using UbbEvents.Api.Data;
using UbbEvents.Api.Infrastructure;
using UbbEvents.Api.Models;

namespace UbbEvents.Api.Repositories;

public sealed class EventLockRepository(AppDbContext db)
{
    public async Task<Event> LockAsync(Guid id, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("An event lock requires an active transaction.");
        await db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '5s'", ct);
        // All writers acquire this lock BEFORE reading registrations. Use a fresh scoped DbContext per request.
        return await db.Events.FromSqlInterpolated($"SELECT * FROM \"Events\" WHERE \"Id\" = {id} FOR UPDATE")
            .SingleOrDefaultAsync(ct) ?? throw ApiException.NotFound("Event");
    }
}
