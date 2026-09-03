# StageFright Community — Project Setup Guide

## Overview

StageFright Community is a desktop application built with .NET MAUI and Blazor Hybrid, designed to streamline operations for small performing arts groups (member management, rehearsal/event attendance, double-entry finance, reporting). This guide covers developer setup. For how the solution is structured internally, see [ARCHITECTURE.md](ARCHITECTURE.md).

## System Requirements

### Minimum Requirements
- **OS**: Windows 10, build 19041 (10.0.19041.0) or later, or macOS (Mac Catalyst)
- **.NET**: .NET 10.0 SDK, with the MAUI workload installed (`dotnet workload install maui`)
- **IDE**: Visual Studio 2022 (17.14+) or Visual Studio Code with the C# Dev Kit
- **Database**: SQLite (bundled via `Microsoft.EntityFrameworkCore.Sqlite`) — no separate install needed

### Recommended
- Visual Studio 2022 Community or Professional
- 8 GB+ RAM, SSD for faster build times

## Project Structure

```
StageFrightCommunity/
├── src/
│   ├── StageFright.App/               # MAUI Blazor Hybrid host (composition root; MauiProgram.cs)
│   │   ├── Platforms/                 # Platform-specific startup code
│   │   ├── Resources/                 # Icons, fonts, splash
│   │   ├── Seeding/                   # DEBUG-only data seeder
│   │   ├── wwwroot/                   # app.css (the app's design system), index.html
│   │   └── MauiProgram.cs             # DI registration, startup sequence, plugin discovery
│   ├── StageFright.Core/              # Domain entities, enums, exceptions, contracts, module services
│   │   ├── Entities/                  # 20 entities (Member, Fee, Payment, JournalEntry, ...)
│   │   ├── Enums/                     # MemberStatus, AccountType, PaymentMethod, ...
│   │   ├── Exceptions/                # Custom exception hierarchy
│   │   ├── Contracts/                 # I<Service>/I<Entity>Repository interfaces
│   │   └── Modules/                   # Agm, AuditTrail, Dashboard, Events, Finance, Members, Rehearsals, Settings
│   ├── StageFright.Data/              # Centralized DAL
│   │   ├── Repositories/              # One repository per entity
│   │   ├── Migrations/                # EF Core migrations
│   │   ├── PluginData/                # Plugin schema merge (PluginMigrationRunner)
│   │   └── StageFrightDbContext.cs
│   ├── StageFright.Plugins.Contracts/ # Extension-point interfaces (leaf assembly, no deps)
│   ├── StageFright.Reports/           # Report pipeline: Providers/, Registry/, Rendering/ (QuestPDF, CsvHelper)
│   └── StageFright.UI/                # Razor class library — all Blazor UI
│       ├── Pages/                     # Dashboard, Events, Finance, Members, Rehearsals, Reports, Settings, Setup
│       ├── Modules/                   # Dashboard-tile providers per module
│       ├── Shared/                    # BorderedListBox, ReportViewer, AddAccountForm, ...
│       └── Layout/                    # ShellLayout (sidebar nav), ThemeProvider
├── tests/
│   ├── StageFright.Core.Tests/        # Unit tests (xUnit v3 + NSubstitute)
│   ├── StageFright.Data.Tests/        # SQLite-backed integration tests
│   ├── StageFright.UI.Tests/          # bUnit component tests
│   ├── StageFright.Integration.Tests/ # Cross-layer user-journey tests
│   ├── StageFright.Reports.Tests/     # Report provider + PDF/CSV renderer tests
│   └── StageFright.TestPlugin/        # Sample plugin fixture for the discovery pipeline
├── docs/                              # This documentation set
├── specs/                             # Spec Kit feature specs, one folder per feature (spec.md, plan.md, tasks.md, ...)
├── .specify/                          # Spec Kit tooling + constitution.md
└── StageFrightCommunity.slnx          # Solution file
```

## Getting Started

### 1. Clone the Repository

```bash
git clone https://github.com/SteveTeece/StageFrightCommunity.git
cd StageFrightCommunity
```

### 2. Install the MAUI Workload and Restore Dependencies

```bash
dotnet workload install maui
dotnet restore
```

Key dependencies (versions centrally managed in `Directory.Packages.props` — see [Central Package Management](#central-package-management) below):
- **Microsoft.Maui.Controls** / **Microsoft.AspNetCore.Components.WebView.Maui** — MAUI + Blazor Hybrid host
- **Microsoft.EntityFrameworkCore.Sqlite** — ORM and SQLite provider
- **Radzen.Blazor** — data grids, switches, and most interactive controls
- **Blazor.Bootstrap** — tabs, sortable lists, theme-switcher JS assets
- **QuestPDF** — PDF report rendering; **CsvHelper** — CSV export
- **Serilog** (+ Console/File sinks) — structured logging
- **OpenTelemetry** — tracing and runtime metrics (console exporter)
- **xunit.v3**, **bunit**, **NSubstitute** — testing (there is no Moq reference — use NSubstitute for all mocking)

### 3. Build the Solution

```bash
dotnet build
```

Release configuration:

```bash
dotnet build --configuration Release
```

> **Always judge warning counts from a full rebuild** (`dotnet build -t:Rebuild`, or delete `bin`/`obj` first) — an incremental build after a small change only recompiles touched files and can look artificially clean.

### 4. Run Tests

Run everything:

```bash
dotnet test
```

Run a specific project:

```bash
dotnet test tests/StageFright.Core.Tests/
dotnet test tests/StageFright.Data.Tests/
dotnet test tests/StageFright.UI.Tests/
dotnet test tests/StageFright.Integration.Tests/
dotnet test tests/StageFright.Reports.Tests/
```

Run a single test by name filter:

```bash
dotnet test --filter "FullyQualifiedName~MemberServiceTests"
```

**Always run without `--no-build`** when verifying a change is complete — `dotnet build`/`dotnet test` only report warnings for files actually recompiled in that pass.

### 5. Run the Application

```bash
dotnet run --project src/StageFright.App/
```

The database auto-migrates on first run, and first-run detection redirects the UI to the `/setup` wizard before the dashboard loads.

## Database Setup

There is no `appsettings.json` connection-string configuration — the database path and log path are computed in code at startup (`MauiProgram.cs`):

- **Database file**: `FileSystem.AppDataDirectory/stagefright.db`, auto-created on first run — the MAUI app-data directory, not the repo.
- **Logs**: rolling daily files (`stagefright-YYYYMMDD.log`, 7-day retention) under `FileSystem.AppDataDirectory/logs/`.
- **Plugins**: loaded from `FileSystem.AppDataDirectory/Plugins/`, auto-created if missing.

### Apply / Create / Remove Migrations

The startup-project for `dotnet ef` is always the MAUI app project:

```bash
# Apply pending migrations
dotnet ef database update --project src/StageFright.Data/ --startup-project src/StageFright.App/

# Add a new migration after changing entities/configurations
dotnet ef migrations add <Name> --project src/StageFright.Data/ --startup-project src/StageFright.App/

# Remove the last (unapplied) migration
dotnet ef migrations remove --project src/StageFright.Data/ --startup-project src/StageFright.App/
```

The application also applies pending migrations automatically on startup — see `MauiProgram.RunStartupSequence`. If migration fails because the database file is corrupted or inaccessible, the app shows the `StartupError` recovery page instead of crashing.

### Reset the Database

1. Close the application.
2. Delete `stagefright.db` from the MAUI app-data directory (`FileSystem.AppDataDirectory`). On Windows (unpackaged head) that is `%LOCALAPPDATA%\StageFright Community\com.stagefright.community\Data\` — the repo-root `delete-database.cmd` script removes it (and its `-wal`/`-shm` sidecars) for you. The script also deletes the sibling `Settings\preferences.dat` (the MAUI `Preferences` store), which holds the recorded display-language choice outside the database — without that, `App.razor.cs` skips the spec 029 `/language-select` screen straight to `/setup` on the next launch.
3. Run the app again (or `dotnet ef database update`) — the schema, the first-run `/language-select` screen and the `/setup` wizard all come back clean.

## Central Package Management

NuGet package versions are centrally managed via the root `Directory.Packages.props` (`ManagePackageVersionsCentrally`). When adding a new package reference:

1. Add `<PackageReference Include="..." />` (**no** `Version` attribute) to the `.csproj`.
2. Add the matching `<PackageVersion Include="..." Version="..." />` entry to `Directory.Packages.props`.

Never pin a version directly in a `.csproj`.

## Development Workflow

### Feature Development with Spec Kit

Every non-trivial feature starts with a specification under `specs/<NNN-feature-name>/` (`spec.md`, `plan.md`, `tasks.md`, plus `data-model.md`/`contracts/`/`research.md` as needed), using the Spec Kit slash commands (`/speckit.specify`, `/speckit.clarify`, `/speckit.plan`, `/speckit.tasks`, `/speckit.implement`). Branches follow `NNN-descriptive-name` (e.g. `017-setup-wizard-tabs`). **When a code change touches behavior a spec doc describes, update that doc in the same task** — including small, presentation-only tweaks.

### Local Development

1. Create a feature branch from `dev` (not `master`).
2. Make changes, following [ARCHITECTURE.md](ARCHITECTURE.md) and the [UI Component Style Guide](UI_COMPONENT_STYLE_GUIDE.md).
3. Run `dotnet build` and the full `dotnet test` suite (without `--no-build`) and confirm both are green before considering the task complete.
4. Commit with a message matching the existing `git log` style.
5. Push and open a PR **against `dev`** — PRs targeting `master` are rejected by convention (see [CONTRIBUTING.md](../CONTRIBUTING.md#pull-request-process)).

### Adding a New Module

See [ARCHITECTURE.md § Adding a New Module](ARCHITECTURE.md#adding-a-new-module) for the concrete file-by-file walkthrough (entity → contract → repository → migration → module service → menu provider → UI → manual DI registration → tests).

### Code Style

- PascalCase for types/methods, camelCase for locals/parameters.
- XML documentation (`///`) is **mandatory** for public types, methods, properties, and enum values — see [XML-DOCUMENTATION-STANDARDS.md](XML-DOCUMENTATION-STANDARDS.md).
- One class/interface/record/struct/enum per file, file name matching the type exactly.
- Async/await for all I/O.

## Continuous Integration

### GitHub Actions

`.github/workflows/ci.yml` runs on `windows-latest`:

1. Sets up .NET 10 and installs the MAUI workload.
2. `dotnet restore StageFrightCommunity.slnx -r win-x64`.
3. `dotnet build StageFrightCommunity.slnx --configuration Release`.
4. Runs each test project separately (Core, Data, Reports, UI, Integration), each emitting a `.trx` result file into `TestResults/`.

Triggers: push to `master`, `main`, `dev`, or `001-initial-mvp`; pull requests targeting `master`, `main`, or `dev`.

> All pull requests must target `dev`, not `master`. PRs opened against `master` are rejected — see [CONTRIBUTING.md](../CONTRIBUTING.md#pull-request-process).

## Common Tasks

### Rebuild Everything

```bash
dotnet clean
dotnet build
```

### Run with Debugging

In Visual Studio: set breakpoints, press **F5**.

From the command line:

```bash
dotnet run --project src/StageFright.App/ --configuration Debug
```

In `DEBUG` builds only, `IDebugDataSeeder` is registered and BlazorWebView developer tools are enabled.

### Update a Single Package

```bash
dotnet add src/StageFright.Core/ package PackageName
```

Then add the matching `<PackageVersion>` entry to `Directory.Packages.props` (see [Central Package Management](#central-package-management)).

## Troubleshooting

### Build Fails with "Project not found"

Confirm you're at the repo root (where `StageFrightCommunity.slnx` lives) before running `dotnet build`/`dotnet run`.

### Tests Won't Run

1. `dotnet --version` — confirm the .NET 10 SDK is installed.
2. `dotnet restore`
3. `dotnet build`
4. `dotnet test` (full rebuild if you need accurate warning counts too — see above)

### Database Migration Issues

```bash
dotnet ef migrations remove --project src/StageFright.Data/ --startup-project src/StageFright.App/
dotnet ef migrations add MigrationName --project src/StageFright.Data/ --startup-project src/StageFright.App/
dotnet ef database update --project src/StageFright.Data/ --startup-project src/StageFright.App/
```

### MAUI Application Won't Start

1. Check logs under `FileSystem.AppDataDirectory/logs/` (on Windows, typically under `%LOCALAPPDATA%\Packages\...` or the unpackaged app's local data folder, since `WindowsPackageType` is `None`).
2. Confirm `FileSystem.AppDataDirectory` is writable — the database is created there.
3. Try: `dotnet clean && dotnet build && dotnet run --project src/StageFright.App/`.
4. If the database file itself is corrupted, the app should show the `StartupError` recovery page rather than crash — check that page's message before assuming a build problem.

## Additional Resources

- [ARCHITECTURE.md](ARCHITECTURE.md) — solution layout, module structure, extension points, data model
- [UI_COMPONENT_STYLE_GUIDE.md](UI_COMPONENT_STYLE_GUIDE.md) — design tokens and component standards
- [XML-DOCUMENTATION-STANDARDS.md](XML-DOCUMENTATION-STANDARDS.md) — `///` comment requirements
- [specs/001-initial-mvp/spec.md](../specs/001-initial-mvp/spec.md) — original MVP specification
- [.NET MAUI Documentation](https://learn.microsoft.com/en-us/dotnet/maui/)
- [Entity Framework Core Documentation](https://learn.microsoft.com/en-us/ef/core/)
- [Blazor Documentation](https://learn.microsoft.com/en-us/aspnet/core/blazor/)

## Getting Help

- Check existing [GitHub Issues](https://github.com/SteveTeece/StageFrightCommunity/issues)
- Review [Pull Requests](https://github.com/SteveTeece/StageFrightCommunity/pulls) for similar prior work
- Check log files under the MAUI app-data `logs/` directory for runtime error details

## Contributing

1. Fork or branch from `dev`.
2. Write tests for new behavior (every reachable code path — success, validation failure, exception, boundary).
3. Ensure `dotnet build` and `dotnet test` are both green.
4. Open a pull request against `dev` with a clear description.

For more details, see [CONTRIBUTING.md](../CONTRIBUTING.md).
