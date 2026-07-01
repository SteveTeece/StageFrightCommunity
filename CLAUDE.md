# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan
<!-- SPECKIT END -->

---

## Build & Test Verification

Always run `dotnet build` and the full test suite (without --no-build) after making code changes, and report the build/test results before considering a task complete.


## Commands

```bash
# Restore and build
dotnet restore
dotnet build

# Run the application (MAUI shell; database auto-migrates on first run)
dotnet run --project src/StageFright.App/

# Run all tests
dotnet test

# Run a specific test project
dotnet test tests/StageFright.Core.Tests/
dotnet test tests/StageFright.Data.Tests/
dotnet test tests/StageFright.UI.Tests/
dotnet test tests/StageFright.Integration.Tests/
dotnet test tests/StageFright.Reports.Tests/

# Run a single test by name filter
dotnet test --filter "FullyQualifiedName~MemberServiceTests"

# EF Core migrations (startup-project is the MAUI app)
dotnet ef migrations add <Name>       --project src/StageFright.Data/ --startup-project src/StageFright.App/
dotnet ef database update             --project src/StageFright.Data/ --startup-project src/StageFright.App/
dotnet ef migrations remove           --project src/StageFright.Data/ --startup-project src/StageFright.App/
```

The solution file is `StageFrightCommunity.slnx` in the repo root.

During development the SQLite database is written to `<repo-root>/TestData/stagefright.db` (auto-created). Logs are written to rolling daily files under the MAUI app-data directory.

---

## Architecture

### Project layout

| Project | Role |
|---------|------|
| `StageFright.App` | MAUI Blazor Hybrid host — composition root only. Hosts a single `BlazorWebView`; zero application logic. |
| `StageFright.Core` | Domain entities, enums, custom exceptions, repository/service contracts, application services (module slices). |
| `StageFright.Data` | Centralized DAL — `StageFrightDbContext`, EF Core migrations, one repository per entity, `UnitOfWork`. |
| `StageFright.Plugins.Contracts` | Extension-point interfaces consumed by both core and external plugins. Leaf assembly with no dependencies. |
| `StageFright.Reports` | Report infrastructure — `ReportProviderRegistry`, `PdfReportRenderer` (QuestPDF), `CsvReportExporter` (CsvHelper), shared `ReportData` model. |
| `StageFright.UI` | Razor class library — ALL Blazor UI. `App.razor` owns the router; `ShellLayout.razor` owns nav. |
| `tests/StageFright.Core.Tests` | xUnit unit tests for services and domain logic. |
| `tests/StageFright.Data.Tests` | Integration tests hitting SQLite in-memory connections. |
| `tests/StageFright.UI.Tests` | bUnit component tests. |
| `tests/StageFright.Integration.Tests` | Cross-layer user-journey tests. |
| `tests/StageFright.Reports.Tests` | Report-provider and PDF/CSV renderer tests. |
| `tests/StageFright.TestPlugin` | Sample plugin fixture (tile + report + entity). |

### Navigation

Blazor Router owns **all** navigation. Every screen has a `@page` directive. `NavigationManager.NavigateTo` is the only way to transition between pages. MAUI Shell routing is disabled — MAUI is a platform-only container. First-run detection redirects to `/setup` before the dashboard loads.

### Module structure inside `StageFright.Core`

Application logic lives in `StageFright.Core/Modules/<ModuleName>/`. Each module slice contains its services, request/response models, and menu/tile providers. Repositories are *not* module-owned; they live centrally in `StageFright.Data/Repositories/` (this is a spec-mandated deviation from pure vertical-slice, required by FR-042).

Current modules: `AuditTrail`, `Dashboard`, `Events`, `Finance`, `Members`, `Rehearsals`, `Settings`.

### Extension points (plugin contracts)

All extension points are defined as interfaces in `StageFright.Plugins.Contracts`:

- `IDashboardTileProvider` — provides one or more dashboard tiles.
- `ISettingsTabProvider` — adds a tab to the Settings page.
- `IMenuItemProvider` — contributes items to the navigation bar.
- `IReportProvider` — delivers a named report as `ReportData`.
- `IDataAccessProvider` — supplies a plugin `DbContext` that the migration runner merges into the same SQLite database.

MVP providers register in `MauiProgram.RegisterCoreServices`. External plugins are discovered at runtime from the `Plugins/` directory via `AssemblyLoadContext`; failures are caught, logged, and skipped — they never block startup.

### Finance / GL integrity

Every fee or payment write wraps fee creation + paired GL debit/credit + balance assertion in one `DbContext` ACID transaction. A `GLBalanceException` is thrown and the transaction rolled back if the sum of debits ≠ sum of credits. GL is the authoritative source for member balances: `outstanding = Σ(debits) − Σ(credits)` per member. Financial records (`Fee`, `Payment`, `Transaction`) are **immutable and never deleted** — corrections use GL reversing pairs.

### Reports pipeline

`IReportProvider` → `ReportData` (rows/columns/sections/subtotals) → `ReportViewer.razor` (modal "Generating…", synchronous) → `PdfReportRenderer` (QuestPDF) or `CsvReportExporter` (CsvHelper). Cancel appears after 5 s. All six MVP reports (`IncomeStatement`, `TrialBalance`, `AccountRegister`, `MemberAccountSummary`, `MemberList`, `Committee`) follow this single pipeline.

### Data model highlights

- **13 entities** in `StageFright.Core/Entities/`: `Member`, `CommitteeMembership`, `Rehearsal`, `AttendanceRecord`, `Event`, `EventType`, `ParticipationRecord`, `Fee`, `Payment`, `Transaction`, `Category`, `Settings`, `AuditTrailEntry`.
- All PKs are `Guid`. All entities carry `CreatedAt`; most carry `UpdatedAt`.
- **Soft-delete** (`IsDeleted`, `DeletedAt`, `DeletedBy`) is present on every entity *except* `Fee`, `Payment`, `Transaction` (financial exemption).
- `AttendanceRecord` carries soft-delete fields but they are never set by any MVP workflow — records are permanently immutable once saved.

---

## Key rules (non-negotiable)

**One class per file.** Every C# class, interface, record, struct, or enum lives in its own file named exactly after the type. Private nested types are the only exception.

**Blazor component structure.** Every `.razor` component MUST have a paired `.razor.cs` code-behind file containing all C# logic — `@code { }` blocks in `.razor` files are prohibited. A `.razor.css` CSS isolation file is added only when the component requires styles that are genuinely scoped to that component; most CSS belongs in the global stylesheet (`wwwroot/css/`).

**No custom JavaScript.** All business logic and UI interaction is in C#/Blazor. No `.js` files, no JS interop for business logic. Javascript that is part of an existing pre-written control or nuget package is permitted.

**Custom exceptions at every boundary.** Raw framework exceptions (`DbException`, `IOException`, etc.) must be caught and re-thrown as project-defined custom exceptions before crossing layer boundaries. Exception types live in `StageFright.Core/Exceptions/`.

**Exhaustive code-path test coverage.** Every reachable code path — success, validation failure, exception, boundary/null — must have automated tests before merge. Tests follow the `Should_[ExpectedBehavior]_When_[Condition]` naming convention. Test method names use `_Integration` suffix to distinguish integration tests from unit tests.

**Soft-delete everywhere (except finance).** Never hard-delete application data. Financial records (`Fee`, `Payment`, `Transaction`) are explicitly exempt — they carry no soft-delete fields and must never be deleted at all.

## Tech Stack & Conventions section.

This is a MAUI Blazor project using BlazorBootstrap for charts/UI controls and double-entry accounting for finances; prefer existing patterns (e.g. month-name dropdowns, BlazorBootstrap charts) over custom SVG/Radzen.

When summing financial amounts, only sum payment-related credit entries, not all GL credit entries, to avoid double-counting in double-entry accounting.

## Known Gotchas section.

Watch for MAUI WebView quirks: Settings tabs require the Bootstrap JS bundle and may need lazy rendering / StateHasChanged handling to avoid concurrent DbContext access and OnShown callback failures.
