# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

WebReader is a .NET 9 ASP.NET Core web application that serves as a file reader platform. It stores books/files in buckets (similar to S3-compatible storage), supports PDF, ZIP with images, and FB2 file types, and provides reading progress tracking with scheduled background tasks for auto-downloading content.

## Architecture

### Layered Structure

```
WebReader/
├── Controllers/        # API and web controllers
├── Repositories/       # Data access layer
├── Services/           # Business logic services
├── Models/             # Entities, DTOs, enums
├── Migrations/         # EF Core migrations
├── Background/         # Background tasks and scheduled jobs
├── Helpers/            # Utility classes
└── Configuration/      # Configuration classes
```

### Key Components

1. **Entities** (Models/Entities/)
   - `Bucket`: Represents storage buckets with access roles (User/Admin)
   - `File`: Represents stored files with parts support for large files
   - Both support hierarchical file structures via `NextPartId`

2. **Repositories** (Repositories/)
   - Implement data access for Buckets, Files, Users, and ScheduledTasks
   - Use `ApplicationDbContext` with PostgreSQL

3. **Services** (Services/)
   - `FileService`: File operations (upload, delete, retrieve)
   - `MinioService`: MinIO S3-compatible storage operations
   - `UserService`: User management
   - `BucketService`: Bucket management

4. **Background Tasks** (Background/)
   - `BackgroundTaskManager`: Hosted service that orchestrates scheduled tasks
   - Task types defined in `TaskType` enum (auto-download, cleanup, sync)
   - Tasks registered via `AddKeyedTransient<IBackgroundTasked, ...>`

5. **SignalR Hub**
   - `ScheduledTaskHub`: Real-time notifications for task status updates

### Database

- PostgreSQL via Npgsql
- Entity Framework Core 9 with migration support
- Connection string in `appsettings.json`

### Storage

- MinIO (S3-compatible object storage)
- Configuration in `appsettings.json`

## Development Commands

### Build
```bash
dotnet build
```

### Run
```bash
dotnet run
```

### Run with specific environment
```bash
dotnet run --configuration Release
```

### Apply database migrations
```bash
dotnet ef database update
```

### Run migrations from project directory
```bash
dotnet ef database update --project WebReader --startup-project WebReader
```

### Run a specific test
```bash
dotnet test --filter "FullyQualifiedName~TestName"
```

### Run all tests
```bash
dotnet test
```

### Run tests with detailed output
```bash
dotnet test --logger "console;verbosity=detailed"
```

### Restore packages
```bash
dotnet restore
```

### Publish
```bash
dotnet publish -c Release -o ./publish
```

## Key Configuration

### appsettings.json sections

- `Kestrel`: HTTP server configuration (port 80)
- `DbConfig`: PostgreSQL connection string
- `MinioConfig`: MinIO endpoint and credentials
- `Telegram`: Telegram bot integration (token, webhook URL)
- `JwtConfig`: JWT authentication settings
- `Logging`: Console output formatting and log levels

## Background Task System

Tasks run via `BackgroundTaskManager` hosted service:

1. **Scheduled tasks** (EveryHour, EveryDay, EveryWeek, EveryMonth)
2. **Manual tasks** (Manually)

Task lifecycle:
1. `ScheduledTaskConfig` defines task type, cron schedule, and settings
2. `BackgroundTaskManager` creates `ScheduledTask` instances from configs
3. Tasks execute via `IBackgroundTasked` implementations registered by type
4. Results stored in `ScheduledTask` entity with status (Pending, InProgress, Error, Canceled, Completed)
5. SignalR hub broadcasts status updates to connected clients

## File Types Supported

- PDF (`.pdf`)
- ZIP with images (`.zip`)
- FB2 (`.fb2`)

## Authentication

- Cookie-based authentication with 30-minute expiration
- JWT bearer token support
- Default policy requires authenticated users

## Important Notes

- Large file uploads configured with `long.MaxValue` limits
- Response compression enabled (Brotli)
- Hybrid cache configured with 1-second default expiration
- Background tasks have 60-minute timeout
- Telegram bot integration for notifications

## Migration Notes

- Migrations are in `Migrations/` folder
- Each migration has timestamp-based name
- Use `dotnet ef migrations list` to see all migrations
- Use `dotnet ef database update` to apply pending migrations