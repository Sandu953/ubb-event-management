namespace UbbEvents.Api.Data.Seeding;

public static class DemoSeedCommand
{
    public static async Task<int> RunAsync(WebApplication app, bool dryRun, CancellationToken ct = default)
    {
        try
        {
            await using var scope = app.Services.CreateAsyncScope();
            var seeder = scope.ServiceProvider.GetRequiredService<DevelopmentDataSeeder>();
            // Do not print a connection string, account credentials, or a JWT key.
            app.Logger.LogInformation("Demo seed target: {Target}; environment: {Environment}.",
                seeder.Target, app.Environment.EnvironmentName);
            var result = await seeder.SeedAsync(dryRun, ct);
            app.Logger.LogInformation(
                "{Operation}: {Users} users, {Organizations} organizations, {Memberships} memberships, {Events} events, {Registrations} registrations, {Audits} audit entries.",
                dryRun ? "Would add (dry run)" : "Added", result.Users, result.Organizations,
                result.Memberships, result.Events, result.Registrations, result.AuditLogs);
            return 0;
        }
        catch (DemoSeedException error)
        {
            app.Logger.LogError("Demo seed refused: {Reason}", error.Message);
            return 1;
        }
        catch (Exception error)
        {
            app.Logger.LogError("Demo seed failed ({ExceptionType}). Check database access, applied migrations, and reserved demo identities. The seed transaction was not committed.",
                error.GetType().Name);
            return 1;
        }
    }
}
