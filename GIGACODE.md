# Repository Guidelines

## Project Overview

WebReader is an ASP.NET Core 9.0 MVC web application with a REST API for reading manga/comics and books (fb2, pdf, img). It supports file scraping via PuppeteerSharp, S3-compatible storage (MinIO/Garage), and Telegram bot integration.

## Project Structure

| Directory | Purpose |
|-----------|---------|
| `Controllers/` | MVC controllers and REST API endpoints (`Controllers/Rest/`) |
| `Models/` | Entities (`Entities/`), DTOs (`Dtos/`), enums, SignalR hub |
| `Services/` | Business logic (FileService, BucketService, MinioService, etc.) |
| `Repositories/` | Repository pattern over EF Core DbContext |
| `Background/` | Background tasks via IHostedService (auto-download, cleanup, S3 sync) |
| `Configuration/` | POCO config classes bound from `appsettings.json` |
| `Data/` | `ApplicationDbContext` — EF Core DbContext with seeding |
| `Helpers/` | Utility classes (image processing, caching, functions) |
| `Views/` | Razor views organized by controller (File, Account, Home, etc.) |
| `wwwroot/` | Static assets (CSS, JS, fonts) |
| `Migrations/` | Hand-written EF Core migrations |

## Build, Run & Deploy

```bash
# Build
dotnet build WebReader.sln

# Run locally (HTTPS on 443, HTTP on 80)
dotnet run

# Docker Compose (PostgreSQL, MinIO, Garage S3)
docker compose up --build

# Deploy via Docker
docker build -t webreader .
```

EF Core migrations are hand-written. To create one:

```bash
dotnet ef migrations add <MigrationName> --project WebReader.csproj
```

## Coding Conventions

- **C# 12+**, nullable reference types enabled, implicit usings enabled
- **Naming**: PascalCase for classes/methods, camelCase for fields/local variables, `_` prefix for private fields
- **Architecture**: Layered — Controllers → Services → Repositories → DbContext
- **Error handling**: FluentResults pattern (`Result<T>`) for domain errors
- **Base entity**: All entities inherit `BaseEntity` (Guid Id, CreatedDate, UpdatedDate)

## Database

- **PostgreSQL 16** via Npgsql + EF Core 10
- Entities: `CustomUser`, `Bucket`, `File`, `UserReading`, `ScheduledTask`, `ScheduledTaskConfig`, `SubscriberTg`
- Auto-seeding of test user, default buckets, and task configs in `ApplicationDbContext.OnConfiguring()`

## Frontend

Traditional ASP.NET Core MVC with Razor views. No frontend build toolchain — plain CSS and vanilla JavaScript. Libraries (pdf.js, axios) loaded as static files from `wwwroot/js/libs/`.

## Commit Guidelines

Use short, imperative-style commit messages (lowercase, no prefixes). Examples from history:

```
fix auth flow
add telegram webhook
update compose.yaml
```

## Security & Configuration

- JWT + Cookie authentication
- Configuration via `appsettings.json` — never commit secrets; use environment variables or Docker secrets
- Docker Compose includes MinIO and Garage S3 for local S3-compatible storage testing
