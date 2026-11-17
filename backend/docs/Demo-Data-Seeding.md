# Development demo data

The demo seeder adds fictional accounts, organizations, events, registrations, and audit history to the configured development database. It is an explicit one-shot command: it exits after completion, does not start another API server, and does not run automatically during normal startup.

The seeder supports the project requirements for initial data and demonstrable organization/event/registration workflows. It imports a consistent development snapshot, including published/completed states, rather than inventing a history of users performing those transitions through the API. Seed audit actions are explicitly named `DemoUserSeeded`, `DemoOrganizationSeeded`, `DemoMembershipSeeded`, `DemoEventSeeded`, and `DemoRegistrationSeeded`.

## Run it

Start the existing PostgreSQL service and configure the backend connection string as described in the [README](../README.md#local-setup). Apply the already-reviewed migrations to the intended development database first. The seeder refuses to run while migrations are pending and never creates, resets, or migrates the database.

From the repository root, preview the target and planned additions without writing data:

```powershell
.\scripts\seed-demo.ps1 -DryRun
```

Check the printed host, port, and database name, then populate it:

```powershell
.\scripts\seed-demo.ps1
```

The runner uses the API's existing `http` launch profile, which sets Development and loads its normal backend configuration/User Secrets. It builds Release output so an already-running Debug API can stay open. It does not require a JWT signing key for the seed command because it does not start the web host or issue tokens. The ordinary API still requires JWT configuration.

If `DemoSeed__Password` is already set in the process environment, the runner uses it. Otherwise it reads the ignored root `.env.demo-seed.local` file; on the first real run, it creates that file with a cryptographically generated password. The same supplied password is securely hashed separately for each newly created demo account. The runner does not print the password and restores the previous process environment afterward.

Open `.env.demo-seed.local` locally to obtain the password when signing in. It contains a single `DemoSeed__Password=...` entry. Keep this file private; the existing `.env.*` Git ignore rule excludes it. Dry runs do not require a password or create the file. The seed password is used only for new accounts; changing it on a later run does not reset existing account passwords.

If local PowerShell script policy prevents invoking the runner, the direct .NET command below is also supported.

## Direct .NET command

Configure a private `DemoSeed:Password` through .NET User Secrets, or provide the equivalent `DemoSeed__Password` environment variable. Do not put it in committed appsettings files or scripts. The direct command does not read `.env.demo-seed.local`; automatic loading/generation belongs to the PowerShell runner.

```powershell
# Read-only preview, no seed password required:
dotnet run --project backend/UbbEvents.Api --configuration Release --launch-profile http -- --seed-demo --dry-run

# Add data, using the configured seed password:
dotnet run --project backend/UbbEvents.Api --configuration Release --launch-profile http -- --seed-demo
```

On other platforms the same `dotnet run` commands work. With `--no-launch-profile`, explicitly set `DOTNET_ENVIRONMENT=Development` or `ASPNETCORE_ENVIRONMENT=Development` and provide the development connection configuration. Production/Staging environments are refused. A successful seed/dry run exits with code 0; configuration, migration, identity, or database failures exit with code 1. Target summaries never print the connection string, credentials, or signing key.

## Accounts and organization roles

All names are fictional development samples. Public signup is unchanged and continues to create STUDENT accounts only.

| Email | Global role | Organization roles |
| --- | --- | --- |
| `global.admin@demo.ubbevents.test` | ADMIN | Global administration across both organizations |
| `tech.admin@demo.ubbevents.test` | STUDENT | ORG_ADMIN in Demo UBB Tech Society |
| `arts.admin@demo.ubbevents.test` | STUDENT | ORG_ADMIN in Demo UBB Arts Collective; ORGANIZER in Tech Society |
| `tech.organizer@demo.ubbevents.test` | STUDENT | ORGANIZER in both organizations |
| `arts.organizer@demo.ubbevents.test` | STUDENT | ORGANIZER in Arts Collective |
| `student.one@demo.ubbevents.test` | STUDENT | Participant |
| `student.two@demo.ubbevents.test` | STUDENT | Participant |
| `student.three@demo.ubbevents.test` | STUDENT | Participant |
| `student.four@demo.ubbevents.test` | STUDENT | Participant |
| `student.five@demo.ubbevents.test` | STUDENT | Participant |

The distinct roles of `arts.admin` across organizations demonstrate that organization permissions are tenant-specific. All newly seeded accounts use the generated/configured demo password. Previously seeded accounts retain their current password hashes and global roles.

## Events and registrations

Dates are UTC and relative to the first creation of each event. Upcoming events start at 16:00 UTC and end at 18:00 UTC. Rerunning does not move existing dates forward.

| Title | Organization | Initial state | Schedule | Capacity and sample registrations |
| --- | --- | --- | --- | --- |
| Demo: Organizer planning workshop | Tech Society | DRAFT | +5 days | 20; no participants; visible only to authorized staff |
| Demo: Campus technology conference | Tech Society | PUBLISHED | +14 days | 80; registration not open |
| Demo: Build your first web API | Tech Society | REGISTRATION_OPEN | +7 days | 10; students one/two confirmed, student three canceled |
| Demo: PostgreSQL concurrency lab | Tech Society | REGISTRATION_OPEN | +10 days | 2; students one/two confirmed; three/four/five waitlisted in that order |
| Demo: Creative campus meetup | Arts Collective | REGISTRATION_OPEN | +3 days | 12; students three/four/five confirmed; student three already checked in |
| Demo: Photography walk recap | Arts Collective | COMPLETED | -7 days | 15; students one/three/five confirmed; one and three checked in |
| Demo: Student film evening | Arts Collective | CANCELLED | +5 days | 25; students two/four canceled |
| Demo: University culture night | Arts Collective | PUBLISHED | +21 days | 60; registration not open |

A fresh database receives **10 users, 2 organizations, 6 memberships, 8 events, 16 registrations, and 42 audit entries**. Among the registrations, 10 are confirmed, 3 waitlisted, and 3 canceled; 3 confirmed records are checked in. The open meetup's early check-in follows the current policy: check-in requires open registration but has no scheduled-time window.

Useful walkthroughs after seeding:

1. Sign in as `global.admin` and inspect both organizations and all draft/public events.
2. Sign in as `arts.admin` and compare organization administration in Arts with organizer-only operations in Tech.
3. Sign in as `student.one`, cancel the concurrency-lab registration, and observe student three's FIFO promotion.
4. Sign in as `student.three` to inspect confirmed, waitlisted, and canceled history across events.
5. Sign in as an organizer to list participants, check in an eligible participant, and compare attendance totals.

Use the full email addresses from the table when logging in. Frontend page reloads clear the current in-memory session, so sign in again if needed.

## Idempotency and existing data

The catalog has deterministic UUIDs derived from a versioned demo namespace. A READ COMMITTED transaction and the same PostgreSQL advisory lock used by the development administrator bootstrap serialize concurrent seeding runs. New records and their audit entries commit together.

- Existing demo users retain their names, passwords, global roles, and timestamps.
- Existing memberships retain their current roles. Missing catalog memberships can be added again on a rerun.
- Existing events retain their details, capacity, status, version, and dates. Their registrations are not inserted again or replenished, so participant cancellations, promotions, and check-ins remain intact.
- Registration samples are inserted only for newly created catalog events, whose initial counts respect capacity and check-in constraints. These new rows are not visible to normal application requests until the seed transaction commits.
- Unrelated users, organizations, events, and registrations remain untouched. No rows are deleted.
- A reserved demo email/name already belonging to another ID causes refusal. An unexpected email for a catalog user ID, organization name for a catalog organization ID, or organization/creator for a catalog event ID also causes refusal. The command does not adopt, rename, or elevate such records.
- A second unchanged run reports zero additions, including zero new audits. No automatic reset or password-reset option is provided.

Keep the local password file after the first run if you want the runner to reuse it. If it is lost and regenerated, existing account passwords still do not change; use an existing administrator/controlled development procedure to recover those accounts rather than deleting the database.

## Implementation and verification

- [DemoDataCatalog.cs](../backend/UbbEvents.Api/Data/Seeding/DemoDataCatalog.cs): deterministic IDs, sample entities, relative schedules, and queue order.
- [DevelopmentDataSeeder.cs](../backend/UbbEvents.Api/Data/Seeding/DevelopmentDataSeeder.cs): environment/password/migration checks, collision checks, idempotent planning, hashing, atomic writes, and result counts.
- [DemoSeedCommand.cs](../backend/UbbEvents.Api/Data/Seeding/DemoSeedCommand.cs): safe CLI target/summary/error output and exit status.
- [Program.cs](../backend/UbbEvents.Api/Program.cs): `--seed-demo` / `--dry-run` dispatch before web-host startup; normal startup preserves the separate optional admin bootstrap.
- [scripts/seed-demo.ps1](../scripts/seed-demo.ps1): local credential handling and command invocation.
- [DevelopmentDataSeederTests.cs](../backend/UbbEvents.Api.Tests/DevelopmentDataSeederTests.cs): PostgreSQL-backed dataset, authentication, dry-run, rerun preservation, collision/rollback, concurrency, environment/password guards, pending-migration refusal, and CLI exit tests.

No schema or migration change is needed. Tests create separate databases inside the existing disposable Testcontainers test server, never inside the developer's normal PostgreSQL server.

```powershell
dotnet test UbbEvents.sln --configuration Release --filter FullyQualifiedName~DevelopmentDataSeederTests
```

GenAI usage for this addition: Codex generated the catalog, seeder, runner, tests, and documentation from the existing model and user request. Design decisions include using reserved fictional identities, a supplied/generated local password, one explicit command, additive inserts, and preserving changed registrations. Automated tests and a dry-run target check validate the generated work; human review remains the team's responsibility.

### Executed validation on 16 November 2026

| Check | Result |
| --- | --- |
| `dotnet restore UbbEvents.sln` | Passed |
| `dotnet build UbbEvents.sln --configuration Release --no-restore` | Passed, zero warnings/errors |
| `dotnet test UbbEvents.sln --configuration Release --no-build --logger "console;verbosity=minimal" --logger trx --results-directory TestResults` | 75 passed, zero failed/skipped; includes 11 new seeder cases against isolated PostgreSQL |
| `dotnet ef migrations has-pending-model-changes --project backend/UbbEvents.Api --startup-project backend/UbbEvents.Api --configuration Release --no-build` | No pending model changes; no new migration required |
| Runner syntax and `git check-ignore .env.demo-seed.local` | PowerShell parses; local credentials file is ignored |
| `.\scripts\seed-demo.ps1 -DryRun` | Confirmed Development target `127.0.0.1:5433/ubb_events`, applied migrations, no collisions, and planned additions |
| `.\scripts\seed-demo.ps1` | Added 10 users, 2 organizations, 6 memberships, 8 events, 16 registrations, 42 audits to the verified local database |
| Same command a second time | Zero additions across every entity/audit category |
| Existing local API authentication/history | Demo global administrator logged in as ADMIN; demo student one logged in and retrieved three registrations |

The first runner attempt used Debug output and encountered a file lock held by the already-running API. The runner now uses Release; its preview and both real runs succeeded without stopping that API. Existing database records were preserved, no migration/reset was run, and no frontend source was changed. The credential file and TRX reports remain local ignored artifacts.
