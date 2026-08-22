# Architecture Guide

## Overview

StageFright Community is a .NET MAUI Blazor Hybrid desktop application. It is **not** a vertical-slice-per-layer system (each module does not get its own `Domain/Application/Infrastructure/UI` folder tree). Instead it uses a **layered solution with module slices inside the Core layer**:

- One project per architectural layer (`StageFright.App`, `StageFright.Core`, `StageFright.Data`, `StageFright.UI`, `StageFright.Reports`, `StageFright.Plugins.Contracts`).
- Inside `StageFright.Core`, each business capability ("module") gets its own folder under `Modules/` containing its services, request/response models, and menu-item provider.
- Repositories are **not** module-owned. They live centrally in `StageFright.Data/Repositories/` — one repository per entity — which is a deliberate, spec-mandated deviation from pure vertical-slice architecture (see FR-042 in `specs/001-initial-mvp/`).
- Dashboard-tile providers live in `StageFright.UI/Modules/<ModuleName>/` (not Core), because a tile provider must reference a Blazor component `Type` for its tile content, and `StageFright.Core` has no reference to `StageFright.UI`.

This guide documents the real, current shape of the solution. For the historical/original decision record, see `specs/001-initial-mvp/` and the constitution at `.specify/memory/constitution.md`.

---

## Solution Layout

| Project | Role |
|---------|------|
| `StageFright.App` | MAUI Blazor Hybrid host — composition root only. Hosts a single `BlazorWebView`; zero application logic. |
| `StageFright.Core` | Domain entities, enums, custom exceptions, repository/service contracts (`Contracts/`), and application services organized into module slices (`Modules/`). |
| `StageFright.Data` | Centralized DAL — `StageFrightDbContext`, EF Core migrations, one repository per entity, `UnitOfWork`. |
| `StageFright.Plugins.Contracts` | Extension-point interfaces consumed by both core and external plugins. Leaf assembly with no project dependencies. |
| `StageFright.Reports` | Report infrastructure — `ReportProviderRegistry`, `IReportProvider` implementations, `PdfReportRenderer` (QuestPDF), `CsvReportExporter` (CsvHelper), shared `ReportData` model. |
| `StageFright.UI` | Razor class library — **all** Blazor UI. `App.razor` owns the router; `Layout/ShellLayout.razor` owns the sidebar navigation. Dashboard-tile providers also live here (see above). |
| `tests/StageFright.Core.Tests` | xUnit unit tests for services and domain logic. |
| `tests/StageFright.Data.Tests` | Integration tests hitting SQLite connections. |
| `tests/StageFright.UI.Tests` | bUnit component tests. |
| `tests/StageFright.Integration.Tests` | Cross-layer user-journey tests. |
| `tests/StageFright.Reports.Tests` | Report-provider and PDF/CSV renderer tests. |
| `tests/StageFright.TestPlugin` | Sample plugin fixture (tile + report + entity), used to exercise the plugin-discovery pipeline. |

Project references flow one way: `StageFright.App` → `{Core, Data, UI, Plugins.Contracts, Reports}`; `StageFright.UI` → `{Core, Plugins.Contracts, Reports}`; `StageFright.Data` → `{Core}`; `StageFright.Reports` → `{Core, Plugins.Contracts}`. Nothing depends on `StageFright.App`, keeping it a pure composition root.

Target framework: `net10.0-windows10.0.19041.0` (Windows) / `net10.0-maccatalyst` (Mac Catalyst), set in `StageFright.App.csproj`. There is no `appsettings.json` — the SQLite path and log path are computed in code at startup (see below), not configuration-driven.

---

## Composition Root: `StageFright.App`

`MauiProgram.CreateMauiApp()` does all wiring, in this order:

1. Compute `TestData/stagefright.db` (walking up from the executable directory to find the repo root by locating `*.slnx` or `.git`) and the log path under `FileSystem.AppDataDirectory/logs/`.
2. Configure Serilog (console + rolling daily file, 7-day retention) and OpenTelemetry (tracing + runtime metrics, console exporter).
3. Register `StageFrightDbContext` against SQLite, `IStartupDiagnosticService` (singleton, must exist before the startup sequence runs), then all repositories and core services via explicit `services.AddScoped<TInterface, TImpl>()` calls — **there is no assembly-scanning/auto-registration** (no Scrutor `.Scan()`, no MediatR). Every service is registered by hand in `RegisterRepositories`/`RegisterCoreServices`.
4. Discover and register plugin providers (`DiscoverAndRegisterPlugins` → `PluginLoader.DiscoverAndRegister`) — this must happen **before** `builder.Build()`, because the built `ServiceProvider` doesn't implement `IServiceCollection` and late registrations would never resolve (see the comment in `MauiProgram.cs`, issue #273).
5. Build the app, then run the startup sequence: apply EF Core migrations (falling back to the `StartupError` page on `DbException`/`DbUpdateException` instead of crashing), run plugin migrations via `PluginMigrationRunner`, and purge audit-trail entries older than the configured retention (best-effort — a failure here does not block startup).

---

## Module Structure inside `StageFright.Core`

Each module lives in `StageFright.Core/Modules/<ModuleName>/` and contains its services, request/response DTOs, and menu-item provider (repositories and UI live elsewhere — see above). Current modules: **Agm, AuditTrail, Dashboard, Events, Finance, Members, Rehearsals, Settings**.

```
StageFright.Core/Modules/Finance/
├── FeeService.cs                       # IFeeService implementation
├── PaymentService.cs                   # IPaymentService implementation
├── ExpensePaymentService.cs
├── BankDepositService.cs
├── BankReconciliationService.cs
├── GeneralJournalService.cs
├── OpeningBalanceService.cs
├── AccountService.cs / AccountBalanceService.cs
├── FinanceSummaryService.cs / FinanceSummary.cs
├── RecordPaymentRequest.cs, RecordIncomeRequest.cs, ...   # request DTOs
├── OutstandingFee.cs, MemberBalance.cs, ...                # response/view DTOs
└── FinanceMenuItemProvider.cs          # IMenuItemProvider implementation
```

Interfaces for these services live in `StageFright.Core/Contracts/` (e.g. `IFeeService.cs`, `IPaymentRepository.cs`), not alongside the implementation — this keeps the "one type per file" rule intact while grouping contracts in one place developers can scan.

### Communication between modules

Modules communicate only through interfaces from `StageFright.Core/Contracts/`, injected via DI — never by importing another module's concrete service class or reaching into `StageFright.Data` repositories directly from UI code.

---

## Data Access Layer (`StageFright.Data`)

- `StageFrightDbContext` — the single EF Core context for core entities. `StageFrightDbContextFactory` supports `dotnet ef` design-time tooling.
- One repository class per entity in `Repositories/`, all implementing an interface from `StageFright.Core/Contracts/` (e.g. `MemberRepository : IMemberRepository`). Most derive from `BaseRepository<TEntity>` (generic CRUD) or `SoftDeletableBaseRepository<TEntity>` (adds soft-delete filtering); a few (`GLRepository`, `BackupRepository`, `AuditTrailRepository`) implement their interface directly for entity-specific query shapes.
- `UnitOfWork` (`IUnitOfWork.ExecuteInTransactionAsync`) wraps a delegate in an EF Core database transaction, rolling back on any exception and translating unexpected ones to `DataAccessException`. This is what Finance services use to keep fee/payment/GL writes atomic (see [Finance / GL integrity](#finance--gl-integrity) below).
- **Exception translation is mandatory at this boundary.** `BaseRepository<TEntity>` catches raw EF Core/ADO exceptions and re-throws project-defined exceptions (`DataAccessException`, `DuplicateEntityException` on a `UNIQUE` constraint violation, `ConcurrencyException` on `DbUpdateConcurrencyException`):

```csharp
public virtual async Task<TEntity> AddAsync(TEntity entity, CancellationToken ct = default)
{
    try
    {
        var entry = await _db.Set<TEntity>().AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
        return entry.Entity;
    }
    catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true)
    {
        throw new DuplicateEntityException($"A {typeof(TEntity).Name} with these values already exists.",
            typeof(TEntity).Name, nameof(AddAsync), null, ex);
    }
    catch (Exception ex) when (ex is not DuplicateEntityException and not DataAccessException)
    {
        throw new DataAccessException(ex.Message, typeof(TEntity).Name, nameof(AddAsync), null, ex);
    }
}
```

- Plugin-owned schemas (`IDataAccessProvider`) are merged into the same SQLite file by `PluginMigrationRunner`, each with its own `__EFMigrationsHistory_{PluginName}` table and `{PluginName}_`-prefixed tables (`StageFright.Data/PluginData/`).

---

## Extension Points (Plugin Contracts)

All extension points are interfaces in `StageFright.Plugins.Contracts` (a leaf assembly with no project references, so plugins can reference it without dragging in the whole solution):

| Interface | Purpose | Key members |
|---|---|---|
| `IDashboardTileProvider` | Contributes a dashboard tile. Core tiles use `DisplayOrder` 0–99, plugin tiles 100+. | `TileId`, `Title`, `ModuleName`, `TileComponentType`, `TileSize` (defaults to `OneByOne`), `NavigateRoute?`, `ActionText?`, `GetTileDataAsync(ct)` |
| `ISettingsTabProvider` | Contributes a tab to the Settings page. **No core module implements this** — the built-in tabs (General, Tax, Committee, Event Types, Backup & Restore) are hardcoded directly in `SettingsPage.razor`; this contract exists solely for plugin-added tabs, rendered after the hardcoded ones. | `TabTitle`, `TabKey` (deep-link `/settings?tab={TabKey}`), `DisplayOrder`, `SettingsComponentType` |
| `IMenuItemProvider` | Contributes items to the shell sidebar. Rendering order: Dashboard → module items by `DisplayOrder` → plugin items → Settings (always last). | `ModuleName`, `DisplayOrder`, `GetMenuItems()` → `IReadOnlyList<MenuItem>` |
| `IReportProvider` *(defined in `StageFright.Reports.Registry`, not `Plugins.Contracts`, to avoid a circular project reference — Reports already references Plugins.Contracts)* | Delivers a named report as `ReportData`. | `ReportId`, `ReportName`, `ModuleName`, `Filters`, `GenerateAsync(filters, ct)` |
| `IDataAccessProvider` | Supplies a plugin `DbContext` merged into the shared SQLite database. | `PluginName`, `DbContextType`, `RegisterServices(services)` |

Each interface's failure mode is documented on the interface itself and is uniform: a throwing/duplicate provider is caught, logged, and skipped — it never blocks startup or the other providers (tiles load in parallel; a failing one renders "Unable to load").

Core providers register explicitly in `MauiProgram.RegisterCoreServices`. External plugins are discovered at runtime from the `Plugins/` directory (under `FileSystem.AppDataDirectory`) — see below.

---

## Plugin Discovery & Loading

`PluginLoader.DiscoverAndRegister` (in `StageFright.App`) scans every `*.dll` in the `Plugins/` directory:

1. Loads each assembly into its own (non-collectible) `AssemblyLoadContext`, isolating plugin dependency versions from the host.
2. Reflects over the assembly's concrete types and registers any type implementing `IDashboardTileProvider`, `ISettingsTabProvider`, `IMenuItemProvider`, `IDataAccessProvider`, or `IReportProvider` as a DI **singleton** against that interface.
3. Wraps the whole per-assembly attempt in a `try/catch`: a load failure is wrapped in a `PluginLoadException`, logged, and that assembly is skipped — it never blocks startup or the other plugins.

`tests/StageFright.TestPlugin` is the reference fixture exercising this pipeline end-to-end (a tile provider + a report provider + a plugin entity).

---

## Navigation

Blazor Router owns **all** navigation — `App.razor`'s `<Router AppAssembly>` wraps every route in an `ErrorBoundary`, defaults to `Layout.ShellLayout`, and shows a `NotFound` page linking back to `/dashboard`. Every screen has a `@page` directive; `NavigationManager.NavigateTo` is the only way to transition between pages. MAUI Shell routing is disabled — MAUI is a platform-only container (a single `BlazorWebView` in `MainPage.xaml`). First-run detection redirects to `/setup` before the dashboard loads.

The shell itself (`Layout/ShellLayout.razor`) is a **fixed vertical sidebar**, not a top nav bar: it injects `IEnumerable<IMenuItemProvider>`, orders providers by `DisplayOrder`, and renders each provider's `MenuItem`s (with expandable sub-item groups that auto-expand while a child route is active, and badge counts). A `RadzenSwitch` in the top bar toggles light/dark theme app-wide (hidden on `/setup`, which has its own theme control per FR-022 of spec 017).

---

## Dashboard Tiles

Tiles opt into one of four sizes via the `DashboardTileSize` enum (`StageFright.Plugins.Contracts`), by overriding `IDashboardTileProvider.TileSize`:

| Enum value | Grid footprint | CSS class (`app.css`) |
|---|---|---|
| `OneByOne` (default) | 1 column × 1 row | `.tile-size-1x1` |
| `OneByTwo` | 2 columns × 1 row (double width) | `.tile-size-1x2` |
| `TwoByOne` | 1 column × 2 rows (double height) | `.tile-size-2x1` |
| `TwoByTwo` | 2 columns × 2 rows | `.tile-size-2x2` |

`Dashboard.razor.cs` maps the enum to the CSS class; the grid itself (`.sf-dash-grid` in `StageFright.App/wwwroot/app.css`) is `display: grid; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); grid-auto-flow: dense;` and collapses every tile to a single column below 576px. Resizing a tile only needs the provider's `TileSize` override plus its own inner chart/layout sizing — no `Dashboard.razor` or grid CSS changes are needed.

Tile providers live in `StageFright.UI/Modules/<ModuleName>/` (e.g. `MembersDashboardTileProvider.cs`), not in `StageFright.Core`, because `TileComponentType` must reference a Blazor component `Type` and Core has no UI reference.

---

## Settings System

The Settings page (`/settings`) is a tabbed core feature. Unlike the plugin-oriented tab contract might suggest, the **built-in tabs are not routed through `ISettingsTabProvider`** — `SettingsPage.razor` hosts them directly (General, Tax, Committee, Event Types, Backup & Restore), and separately resolves `IEnumerable<ISettingsTabProvider>` to append any plugin-contributed tabs after them, skipping duplicate `TabKey`s with a warning log. Deep-linking uses `/settings?tab={TabKey}`.

See [Known Gotchas in `CLAUDE.md`](../CLAUDE.md#known-gotchas) for the MAUI WebView quirks around Settings tab rendering (Bootstrap JS bundle requirement, lazy-render/`StateHasChanged` handling to avoid concurrent `DbContext` access).

---

## Reports Pipeline

`IReportProvider` → `ReportData` (rows/columns/sections/subtotals) → `ReportViewer.razor` (modal "Generating…", synchronous; cancel appears after 5s) → `PdfReportRenderer` (QuestPDF) or `CsvReportExporter` (CsvHelper). All ten current reports follow this single pipeline:

`IncomeStatement`, `TrialBalance`, `AccountRegister`, `MemberAccountSummary`, `MemberList`, `Committee`, `BalanceSheet`, `BankReconciliation`, `TaxSummary`, `GeneralLedger` — each is a class in `StageFright.Reports/Providers/` implementing `IReportProvider`, registered individually in `MauiProgram.RegisterCoreServices` and aggregated by `IReportProviderRegistry`.

In QuestPDF-rendered checkbox-style cells (e.g. `AttendanceRollPdfRenderer`), a checked box is a bordered `Container` with a centered "✓" glyph, never a solid filled box.

---

## Finance / GL Integrity

Every fee or payment write wraps fee creation + paired GL debit/credit + balance assertion in one `DbContext` transaction via `IUnitOfWork.ExecuteInTransactionAsync`. `GLBalanceException` is thrown and the transaction rolled back if Σdebits ≠ Σcredits:

```csharp
public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken ct = default)
{
    IDbContextTransaction? tx = null;
    try
    {
        tx = await _db.Database.BeginTransactionAsync(ct);
        await operation(ct);
        await tx.CommitAsync(ct);
    }
    catch (GLBalanceException)
    {
        if (tx is not null) await SafeRollbackAsync(tx);
        throw;
    }
    // ... other exception translation, see UnitOfWork.cs
}
```

GL is the authoritative source for member balances: `outstanding = Σ(debits) − Σ(credits)` per member. When summing financial amounts, only sum **payment-related** credit entries, not all GL credit entries, to avoid double-counting in double-entry accounting.

Financial records (`Fee`, `Payment`, `Transaction`) are **immutable and never deleted** — corrections use GL reversing pairs, not edits or deletes.

---

## Data Model

**20 entities** in `StageFright.Core/Entities/`: `Member`, `CommitteeTerm`, `CommitteePositionRecord`, `CommitteeOfficeHolderType`, `AnnualGeneralMeeting`, `AgmAttendanceRecord`, `Rehearsal`, `AttendanceRecord`, `Event`, `EventType`, `ParticipationRecord`, `Account`, `Fee`, `Payment`, `Transaction`, `JournalEntry`, `BankReconciliation`, `ReconciliationLine`, `Settings`, `AuditTrailEntry`.

- All primary keys are `Guid`, e.g.:

```csharp
public class Member
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}".Trim();          // computed, not mapped
    public MemberStatus Status { get; set; } = MemberStatus.Active;

    // Soft-delete
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

- All entities carry `CreatedAt`; most carry `UpdatedAt`.
- **Soft-delete** (`IsDeleted`, `DeletedAt`, `DeletedBy`) is present on every entity *except* `Fee`, `Payment`, `Transaction` (financial exemption), `JournalEntry` (immutable GL header, same exemption), `AuditTrailEntry` (governed by retention purge instead), `ReconciliationLine`, and `CommitteeTerm` — see each entity's doc-comment for its specific rationale.
- `AttendanceRecord` carries soft-delete fields but they are never set by any MVP workflow — records are permanently immutable once saved.
- **13 enums** in `StageFright.Core/Enums/`: `AccountType`, `AuditAction`, `FeeType`, `JournalEntryType`, `MemberStatus`, `PaymentMethod`, `PaymentType`, `PlatformThemePreference`, `ReconciliationStatus`, `ReportColumnAlignment`, `ReportFilterType`, `TaxCode`, `Theme`. (`CategoryType`/`Category` no longer exist — fully replaced by `Account`/`AccountType`, see the `ConvertCategoriesToAccounts` migration.)

---

## Exception Hierarchy & Boundary Translation

Custom exceptions live in `StageFright.Core/Exceptions/`: `ConcurrencyException`, `DataAccessException`, `DataIntegrityException`, `DuplicateEntityException`, `EntityNotFoundException`, `GLBalanceException`, `ImportException`, `PluginLoadException`, `ReconciliationException`, `ValidationException`. Raw framework exceptions (`DbException`, `DbUpdateException`, `IOException`, etc.) are caught and re-thrown as one of these before crossing a layer boundary — see the `BaseRepository<TEntity>` example above, and `PluginLoader`'s `PluginLoadException` wrapping.

---

## Data Grid, List Box, and Toggle Standards

- **Data grids**: every tabular view uses `RadzenDataGrid<TItem>` (never a plain `<table>`), following `MemberList.razor` as the reference: `AllowSorting="true" AllowPaging="true" PageSize="15" class="rz-shadow-0"`. `ReportViewer.razor` is the one exception — its dynamic columns and subtotal rows don't fit RadzenDataGrid's typed-column model, so it hand-rolls paging (also fixed at page size 15).
- **List boxes**: every bordered list box (queued items, role lists, read-only summaries) uses `BorderedListBox<TItem>` (`StageFright.UI/Shared/BorderedListBox.razor`) — takes `Items`, a `RowTemplate`, an optional `OnRemove` (unset → read-only), and `EmptyText`. See the Setup Wizard's Chart of Accounts, Committee, and Review tabs.
- **Toggles**: every on/off toggle uses `<RadzenSwitch>` (`@bind-Value` + `Change` callback, not `@bind:after`) — see the shell's own light/dark toggle above, or the Members List "show inactive" switch. `RadzenSwitch` renders no native `onchange`-wired `<input>`; drive it in bUnit via `cut.Find("[role=switch]").Click()` and assert `aria-checked`. The Setup Wizard's theme control (a Light/Dark `<select>`, FR-022 of spec 017) is a deliberate, documented exception — not a new default.

---

## Key Rules (non-negotiable)

- **One class per file.** Every class, interface, record, struct, or enum lives in its own file named exactly after the type. Private nested types are the only exception. Any PR with multiple public types in one file is rejected in review.
- **Simple over clever.** Prefer the simplest approach; keep code easily readable.
- **Blazor components are always paired.** Every `.razor` file has a `.razor.cs` code-behind — `@code { }` blocks in `.razor` files are prohibited. A `.razor.css` isolation file is added only when styles are genuinely scoped to that component; most CSS lives in `StageFright.App/wwwroot/app.css`.
- **No custom JavaScript.** All business logic and UI interaction is C#/Blazor. JavaScript bundled with a pre-written control/NuGet package (Radzen, Blazor.Bootstrap) is permitted; hand-written `.js` files and JS interop for business logic are not.
- **Custom exceptions at every boundary** (see above).
- **Exhaustive code-path test coverage.** Success, validation failure, exception, and boundary/null paths are all tested before merge. Tests follow `Should_[ExpectedBehavior]_When_[Condition]`; integration tests use the `_Integration` suffix.
- **Soft-delete everywhere except finance** (see [Data Model](#data-model)).

---

## Testing Strategy

Test frameworks: **xUnit v3** (`xunit.v3`), **bUnit** for Blazor components, **NSubstitute** for mocking (not Moq — there is no Moq package reference in the solution).

### Unit tests (single layer, mocked dependencies)

```csharp
[Fact]
public async Task Should_ReturnActiveMembersOnly_When_GetActiveMembersAsync_Called()
{
    var repository = Substitute.For<IMemberRepository>();
    repository.GetAllAsync(Arg.Any<CancellationToken>())
        .Returns(new List<Member> { new() { Id = Guid.NewGuid(), Status = MemberStatus.Active } });

    var service = new MemberService(repository, /* other deps */);

    var result = await service.GetActiveMembersAsync(TestContext.Current.CancellationToken);

    Assert.Single(result);
}
```

Note: NSubstitute's `Arg.Is<T>(x => ...)` predicate lambdas make the lambda parameter nullable-oblivious under `#nullable enable` — this is expected; append `!` at the flagged expression per the codebase convention (see `CLAUDE.md`'s Known Gotchas) rather than restructuring the assertion.

### Integration tests (`StageFright.Data.Tests`, `_Integration` suffix)

Use a real `StageFrightDbContext` against a SQLite connection (in-memory or file-backed), not EF Core's `UseInMemoryDatabase` provider — SQLite-specific behavior (transactions, unique constraints) must be exercised for real.

### UI/component tests (bUnit, `StageFright.UI.Tests`)

```csharp
[Fact]
public void Should_DisplayMembersList_When_MembersProvided()
{
    var service = Substitute.For<IMemberService>();
    service.GetActiveMembersAsync(Arg.Any<CancellationToken>())
        .Returns(new List<Member> { new() { FirstName = "John", LastName = "Doe" } });

    using var ctx = new TestContext();
    ctx.Services.AddSingleton(service);
    var cut = ctx.RenderComponent<MemberList>();

    Assert.Contains("John", cut.Markup);
}
```

See `CLAUDE.md`'s Known Gotchas for the nested-`<EditForm>` bUnit limitation and the flaky "fee"-substring test note.

---

## Best Practices

### ✅ DO
- Keep module services focused on a single business capability
- Inject repositories/services via constructor DI (interfaces from `StageFright.Core/Contracts/`)
- Translate exceptions at every layer boundary
- Write tests covering every reachable code path
- Communicate cross-module only through published interfaces

### ❌ DON'T
- Import another module's concrete service class directly
- Reach into `StageFright.Data` repositories from `StageFright.UI` — always go through a `StageFright.Core` service
- Put business logic in `.razor` markup or code-behind beyond orchestration/validation-display
- Hardcode configuration or use magic strings where an enum/constant exists
- Add a hand-written `.js` file for anything business-logic-related

---

## Adding a New Module

1. **Entities** (if any new ones): `StageFright.Core/Entities/<Entity>.cs`, one type per file.
2. **Enums** (if the concept is shared/foundational): `StageFright.Core/Enums/<Enum>.cs`.
3. **Contracts**: `StageFright.Core/Contracts/I<Service>.cs`, `I<Entity>Repository.cs`.
4. **Repository**: `StageFright.Data/Repositories/<Entity>Repository.cs`, deriving from `BaseRepository<TEntity>` or `SoftDeletableBaseRepository<TEntity>` where possible.
5. **EF Core mapping/migration**: add configuration under `StageFright.Data/Configurations/` if needed, then `dotnet ef migrations add <Name> --project src/StageFright.Data/ --startup-project src/StageFright.App/`.
6. **Module service(s)**: `StageFright.Core/Modules/<ModuleName>/<Service>.cs`, implementing the contract, using injected repositories/other services only.
7. **Menu item provider**: `StageFright.Core/Modules/<ModuleName>/<ModuleName>MenuItemProvider.cs` implementing `IMenuItemProvider`.
8. **UI**: paired `.razor`/`.razor.cs` pages under `StageFright.UI/Pages/<ModuleName>/`, and any dashboard tile under `StageFright.UI/Modules/<ModuleName>/`.
9. **Register everything** explicitly in `MauiProgram.RegisterRepositories`/`RegisterCoreServices` — there is no auto-discovery for core (non-plugin) types.
10. **Tests**: unit tests for the service (`StageFright.Core.Tests`), repository integration tests (`StageFright.Data.Tests`), component tests (`StageFright.UI.Tests`) covering every reachable path.

---

## Summary

The layered-with-module-slices architecture provides:

- **Clarity** — one project per layer, one folder per business capability within Core
- **Testability** — services, repositories, and components are all independently testable
- **Extensibility** — the five plugin-contract interfaces let external assemblies add tiles, settings tabs, menu items, reports, and schema without touching core code
- **Consistency** — RadzenDataGrid, BorderedListBox, and RadzenSwitch as the single UI idiom per concern, and one report pipeline for every report

For more details, see:
- [Setup Guide](SETUP.md)
- [UI Component Style Guide](UI_COMPONENT_STYLE_GUIDE.md)
- [XML Documentation Standards](XML-DOCUMENTATION-STANDARDS.md)
- [Contributing Guide](../CONTRIBUTING.md)
- [Constitution](../.specify/memory/constitution.md)
