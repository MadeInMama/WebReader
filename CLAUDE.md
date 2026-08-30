# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

WebReader is a .NET 9 ASP.NET Core web application that serves as a file reader platform. It stores books/files in buckets (S3-compatible object storage), supports PDF, ZIP-with-images, and FB2 file types, tracks per-user reading progress, and runs scheduled background jobs for syncing storage and auto-downloading new chapters via Puppeteer.

## Architecture

### Layered Structure

```
WebReader/
├── Controllers/        # MVC + REST + API controllers
│   ├── Rest/           # JSON REST endpoints (AccountRest, FileRest)
│   └── *.cs            # MVC and HTML controllers
├── Repositories/       # EF Core data access (one per aggregate)
├── Services/           # Business logic; orchestrates repos + MinIO
├── Models/
│   ├── Entities/       # EF entities (Bucket, File, CustomUser, ...)
│   ├── Dtos/           # View models and request/response DTOs
│   ├── Signal/         # SignalR hubs (ScheduledTaskHub)
│   ├── Extended/       # Composite/decorator entities
│   └── *.cs            # Enums (TaskType, TaskStatus, TaskCron, RoleType, FileType)
├── Data/               # ApplicationDbContext + entity configuration
├── Background/         # Hosted services + IBackgroundTasked impls
│   ├── AutoDownloadNewParts/  # Puppeteer-driven chapter downloaders
│   ├── SyncDbWithS3/          # DB↔S3 reconciliation jobs
│   └── Delete/                # Old-task cleanup jobs
├── Migrations/         # EF Core migrations (timestamp-prefixed)
├── Helpers/            # Stateless utility classes
└── Configuration/      # Strongly-typed config sections
```

### Key Components

1. **Entities** (`Models/Entities/`)
   - `Bucket` — storage bucket with `AccessRoles`, `IsHidden`, `IsSystem`, owner `UserId`, cached `Size`. A user's `personal-<id>` bucket and the global `mybucket` exist; system buckets (e.g. `covers`) are hidden.
   - `File` — stored file with `Type` (PDF/ZIP/FB2), `BucketId`, `NextPartId`/`NextPart` for chained parts, `CurrentPartName`/`CurrentPartNumber`, optional `CoverName`, and a JSON `Settings` column mapped to `jsonb`.
   - `CustomUser` — owns a 1:1 `Bucket`; has `Roles: RoleType[]`.
   - `UserReading` — per-user reading progress (page, scale, `IsDone`).
   - `ScheduledTask` / `ScheduledTaskConfig` — see Background Task System below.
   - `SubscriberTg` — Telegram subscribers.
   - `BaseEntity` — sets `CreatedDate`/`UpdatedDate` via `ApplicationDbContext.SaveChanges` interceptor.

2. **Repositories** (`Repositories/`)
   - One per aggregate root: `BucketRepository`, `FileRepository`, `CustomUserRepository`, `UserReadingRepository`, `ScheduledTaskRepository`, `ScheduledTaskConfigRepository`.
   - All implement `IRepository` (CRUD: `AllAsync`, `FirstOrDefaultAsync`, `AddAsync`, `SaveChangesAsync`).
   - `ScheduledTaskRepository` has task-specific methods: `GetNextTaskAsync`, `GetLastTaskByConfigIdAsync`, `SetStatusProgressResultAsync`.
   - DbContext is registered as both `AddDbContext` and `AddDbContextFactory`; background tasks resolve via the scope factory.

3. **Services** (`Services/`)
   - `FileService`, `FileUploadService`, `FileControllerService` — file CRUD, part-chain handling, presigned URL/cache logic.
   - `BucketService` — bucket CRUD, size aggregation.
   - `UserService` — user CRUD, password hashing via `StaticFunctions.HashPassword` (currently unsalted SHA256 — see Known Issues).
   - `MinioService` — MinIO/S3 operations (uses singleton `IMinioClient`).
   - `AuthRestService` — JWT issuance/validation for REST controllers.

4. **Background Tasks** (`Background/`)
   - `BackgroundTaskManager` is an `IHostedService` that:
     - **Spreads** configs into `ScheduledTask` rows on a 10-minute timer (respects `TaskCron.EveryHour/Day/Week/Month` by walking the last task's `HaveToStartAt`).
     - **Drains** pending tasks on a 1-second timer, executing them via `[FromKeyedServices] IBackgroundTasked` resolved by `TaskType` enum.
     - Each execution has a 60-minute timeout, ends by writing `TaskStatus` + progress + result via the repo, then broadcasts to `ScheduledTaskHub`.
   - All `IBackgroundTasked` implementations derive from `AbstractBackgroundTasked` (handles `UpdateProgress`).
   - Per-type implementations live in subfolders by domain: `AutoDownloadNewParts/` (Puppeteer), `SyncDbWithS3/`, `Delete/`.
   - **Disabled by default** via `BackgroundTasks:Enabled` in `appsettings.json` (flip to `true` to run jobs).

5. **Real-time**
   - `ScheduledTaskHub` mapped at `/ScheduledTaskHub` broadcasts task lifecycle events. **No hub authorization is configured** — see Known Issues.

6. **Storage layer**
   - `IMinioClient` is a singleton built with `WithSSL(false)` (hardcoded). Garage S3 is an alternative in `compose.yaml` (`dxflsys/garage`).
   - `MinioService` is the only consumer.

### Database

- PostgreSQL via Npgsql (`Npgsql.EntityFrameworkCore.PostgreSQL` 9.0.4).
- EF Core 10 tools. `File.Settings` is mapped to `jsonb` explicitly in `OnModelCreating`.
- Composite unique index on `(File.Name, File.BucketId)`.
- Migrations live in `Migrations/` with `yyyyMMddHHmmss_*.cs` naming; `ApplicationDbContext.OnConfiguring` calls `UseSeeding` to insert default user, buckets, and `ScheduledTaskConfig` rows on first run.

### Authentication

- Dual scheme: **Cookies** (default, 30-min sliding, `HttpOnly`+`SameSite=Strict`, `LoginPath=/Account/SignIn`) and **JWT Bearer** (HS256, `ClockSkew=0`).
- A `CookiesOrJwt` policy scheme auto-forwards to JWT when an `Authorization: Bearer …` header is present; otherwise cookies apply.
- Default policy requires `RequireAuthenticatedUser`. Role-based checks are mostly done inline in controllers (no `[Authorize(Roles=...)]` widely used).

### HTTP middleware order (Program.cs)

`UseForwardedHeaders` → `UseExceptionHandler` (routes to `/Account/CustomNotFound`) → `UseHsts` → `UseHttpsRedirection` → CSP/security-headers middleware (issues per-request nonce, hard-strips and rewrites `CSP`/`X-Frame-Options`/etc.) → `UseResponseCompression` → `UseStaticFiles` → `UseHttpMethodOverride` → `UseRateLimiter` (fixed-window 5/2min `LoginPolicy`) → `UseRouting` → `UseAuthentication` → `UseAuthorization` → `MapControllers` + `MapHub<ScheduledTaskHub>` → buffering middleware.

The security-headers middleware is **additive but strips** any CSP/COEP/COOP/HSTS set earlier, then re-appends its own values. It is the single source of truth for browser security headers.

## Development Commands

### Build
```bash
dotnet build
```

### Run (development)
```bash
dotnet run
```

### Run (release)
```bash
dotnet run --configuration Release
```

### Apply database migrations
```bash
dotnet ef database update --project WebReader --startup-project WebReader
```
Migrations run automatically on startup (`context.Database.Migrate()` in `Program.cs`).

### Generate a new migration
```bash
dotnet ef migrations add <Name> --project WebReader --startup-project WebReader
```

### Restore / Publish
```bash
dotnet restore
dotnet publish -c Release -o ./publish
```

### Tests
There is **no test project** in the solution today (`WebReader.sln` contains only the `WebReader` project). The CI workflow likewise does not run `dotnet test`. When tests are added, prefer an xUnit `WebReader.Tests` project (see Known Issues).

### Docker / compose
```bash
docker compose up -d --build
```
`compose.yaml` brings up `webreader` + `db` (Postgres 16) + `minio` (or `garage` S3 alternative). It reads `.env`, `.env_pg`, `.env_s3` from the parent directory and mounts TLS certs into the app to generate a PFX at startup.

## Key Configuration

`appsettings.json` sections (all bound in `Program.cs` to classes in `Configuration/`):

- `Kestrel` — HTTP listen on `http://*:80`. HTTPS terminates via the PFX generated from mounted certs in compose.
- `DbConfig` — PostgreSQL connection string.
- `MinioConfig` — endpoint + access/secret key.
- `Telegram` — bot token + webhook URL; webhook controller is `TelegramWebhookController` at `/api/tgwh`.
- `JwtConfig` — `Key`, `Issuer`, `Audience`, `ExpiryMinutes` (default 1440 = 24h).
- `BackgroundTasks` — `{ "Enabled": false }` flips the `BackgroundTaskManager` hosted service on/off. **Must be `true` for scheduled/auto-download jobs to run.**
- `Logging` — console formatter; per-namespace overrides for `CspController` (Info), `S3Controller` (Trace), `LogRequestAttribute` (Trace).

## Background Task System

Task types in `Models/TaskType.cs`:

| Group | Type | Default schedule |
|---|---|---|
| Sync DB↔S3 | `RemoveBucketsThatNotExistsInDb`, `MakeUnavailableBucketsThatNotExistsInS3`, `RemoveFilesThatNotExistsInDb` | EveryHour |
| Sync DB↔S3 | `UpdateBucketData`, `UpdateFilesData` | EveryHour |
| Auto-download (Puppeteer) | `AutoDownloadNewPartsOmniscientReader` (EveryDay), `AutoDownloadNewPartsSoloLeveling` (EveryWeek), `AutoDownloadNewPartsWorldAfterDestruction` (EveryWeek) | per config |
| Cleanup | `DeleteOldCompletedTasks` (24h), `DeleteOldErroredTasks` (48h), `DeleteOldInProgressTasks` (12h) | per config |

Schedules: `TaskCron` = `EveryHour | EveryDay | EveryWeek | EveryMonth | Manually`. Manual tasks are not created by the spread loop — they are created via `ScheduledTaskController` (no `ScheduledTaskConfig` link).

Lifecycle:
1. `BackgroundTaskManager.RunSpreadTasks` (every 10 min) reads active `ScheduledTaskConfig` rows grouped by `Cron`, walks the last `ScheduledTask.HaveToStartAt`, and inserts new `ScheduledTask` rows with the next due time.
2. `RunAsSoonAsPossible` (every 1 s) calls `ScheduledTaskRepository.GetNextTaskAsync` (orders by `Priority` index, then `HaveToStartAt`), resolves the keyed `IBackgroundTasked` by `TaskType`, and runs it under a 60-min linked cancellation token.
3. Final status (`Completed` | `Error` | `Canceled`) + progress + result text are written back; `ScheduledTaskHub` is notified.
4. Auto-download implementations extend `AbstractAutoDownloadNewParts` and use `PuppeteerSharp`. They share settings like `max_size` via the `JsonDocument` on the task row.

## File Types Supported

- `FileType.Pdf` — PDF
- `FileType.Zip` — ZIP containing images (split/trimmed via `Helpers/ImageSplitter`, `ImageTrimmer`, `ImageEmptyChecker`, `SixLabors.ImageSharp`)
- `FileType.Fb2` — FB2 (FictionBook2)

## Known Issues (worth checking before changing related code)

These are the live defects called out in the project improvement plan (`.cursor/plans/webreader_improvements_plan_b72ec613.plan.md`); future work should assume them unless told otherwise.

- **Admin delete bug** — `Controllers/FileApiController.cs:78` is missing `return` on the `if (!User.GetUserRoles().Contains(RoleType.Admin))` check; non-admins can hit the delete path.
- **Password hashing** — `Helpers/StaticFunctions.cs:103` uses unsalted SHA256. Plan calls for ASP.NET `PasswordHasher<T>` (or PBKDF2/Argon2) with rehash-on-login migration.
- **Public endpoints** — `TelegramWebhookController` does not validate `X-Telegram-Bot-Api-Secret-Token`; `ScheduledTaskHub` has no `[Authorize]`.
- **REST error envelope** — `FileRestController.GetReading` lacks `IsFailed` check; `AccountRestController` doesn't enforce `ModelState.IsValid`; `/api/*` errors fall through to the HTML 404 handler.
- **Background task manager scope** — `BackgroundTaskManager.RunAsSoonAsPossible` keeps one `IServiceScope` for the whole loop (scoped repos/DbContext live for hours). Plan: fresh scope per tick and per task.
- **Atomic task claiming** — `ScheduledTaskRepository.GetNextTaskAsync` uses `AsNoTracking` then updates separately; race under multiple instances. Plan: `SELECT ... FOR UPDATE SKIP LOCKED`.
- **Auto-download** — new Puppeteer browser per chapter (should launch once per task); `BrowserProcessKiller` kills all Chrome on the host (track child PIDs); unawaited `Task.Delay`; `GC.Collect` in hot path; `AbstructAutoDownloadNewParts.cs` filename typo.
- **EF schema drift** — `ScheduledTask.ScheduledTaskConfigId` (entity nullable vs migration non-nullable), `UserReading.Scale` default 100 vs EF config 1.
- **Async/N+1** — `FileControllerService` uses `Task.WaitAll`/`.Result` in cache callback; `FileService` per-file loops with multiple DB roundtrips; `UserReadingRepository` opens a new DbContext per update; `FileControllerService` loads all readings and filters in memory.
- **MinIO SSL hardcoded off** in `Program.cs:71`; **forwarded-headers trust** clears `KnownProxies/KnownNetworks` (any proxy trusted); **upload limits** set to `long.MaxValue`; **HybridCache** default TTL is 1s.
- **Cookie auth package** pinned to 2.3.11 while the rest of the stack is on .NET 9.
- **`ServerGarbageCollection=false`** in csproj — relevant because Puppeteer long-lived.
- **Secrets in `appsettings.json`** — DB password, MinIO credentials, Telegram bot token, JWT signing key are all checked in. Treat as compromised; rotate before any real deploy.

## Conventions

- Nullable reference types are on (`<Nullable>enable</Nullable>`). New code should be null-safe.
- JSON serialization keeps PascalCase (`PropertyNamingPolicy = null` in `Program.cs`).
- Logging is console-only with `yyyy-MM-dd HH:mm:ss` local time (UTC off in config).
- `BaseEntity` audit timestamps are populated by `ApplicationDbContext.SetCreateAndUpdateTime` — do not set them manually.
- `ServerGarbageCollection` is intentionally disabled in the csproj (Puppeteer workload consideration).
