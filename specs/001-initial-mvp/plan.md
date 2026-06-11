# Implementation Plan: StageFright Community — Initial MVP

**Branch**: `001-initial-mvp` | **Date**: 2026-06-11 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-initial-mvp/spec.md`

**Note**: This plan is produced by the `/speckit-plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

StageFright Community MVP is a single-user desktop application (Windows + macOS) for small performing arts groups, covering member registration, rehearsal attendance with automatic fee accrual, event participation tracking, double-entry (GL paired-transaction) finance with four standard accounting reports, committee tracking, backup/restore (protobuf), and a plugin architecture (dashboard tiles, settings tabs, menu items, reports, data access).

Technical approach: .NET MAUI Blazor Hybrid shell (MAUI is a platform-only container; a single `BlazorWebView` renders all UI), Blazor Router/NavigationManager for 100% of navigation, EF Core + SQLite behind a centralized DAL with repository contracts, vertical-slice module organization with provider contracts (`IDashboardTileProvider`, `ISettingsTabProvider`, `IMenuItemProvider`, `IReportProvider`, `IDataAccessProvider`) enabling plugin extensibility without core modification. Reports render through one shared viewer with QuestPDF print-to-PDF and CsvHelper CSV export. Serilog + OpenTelemetry observability; xUnit/bUnit test stack with exhaustive code-path coverage per Constitution §11.

## Technical Context

**Language/Version**: C# 14 on .NET 10.0

**Primary Dependencies**:
- .NET MAUI (BlazorWebView host; Windows + Mac Catalyst targets)
- Blazor Hybrid (all UI, routing via Blazor Router + `NavigationManager`)
- Radzen.Blazor (free components, permitted per Constitution §7.2)
- Bootstrap 5.3 (styling, `data-bs-theme` dark/light switching)
- Entity Framework Core 10 + `Microsoft.EntityFrameworkCore.Sqlite`
- Serilog (structured logging) + OpenTelemetry (traces/metrics)
- protobuf-net (backup/restore serialization — see research.md R2)
- QuestPDF (report print-to-PDF — see research.md R3)
- CsvHelper (CSV export with correct escaping — see research.md R7)

**Storage**: Local SQLite database file (app data directory); EF Core code-first migrations; semver `schemaVersion` recorded for import/export manifests; plugin entities migrate into the same database via `IDataAccessProvider`

**Testing**: xUnit (unit + integration), bUnit (Blazor component + UI integration), NSubstitute (mocking), SQLite in-memory connections for repository/integration tests (see research.md R9)

**Target Platform**: Windows 10.0.19041.0+ and macOS 10.15+ (Mac Catalyst); desktop only — no mobile, no web hosting

**Project Type**: desktop-app (MAUI Blazor Hybrid, single window, single user)

**Performance Goals**: Advisory only per NFR-003 — dashboard visible within ~3 s of startup (SC-002), reports within ~5 s for typical data (≤500 members, ≤3 years history); no enforced SLAs, synchronous blocking report generation with modal indicator

**Constraints**: Offline/local-only (no cloud, no auth); no custom JavaScript (Constitution §7.3); WCAG AA contrast in both themes; financial records immutable (corrections via GL reversing pairs only); soft-delete everywhere except Fee/Payment/Transaction (exempt per Constitution §3.4); one class per file (Constitution §3.2.1); atomic ACID transactions wrapping every Fee/Payment + GL pair

**Scale/Scope**: ≤500 members, ≤3 years of data per organization; 7 core modules (Dashboard, Members, Rehearsals, Events, Finance, Reports, Settings); ~13 entities; 6 MVP reports; ~25 screens/pages

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Gate (Constitution ref) | Status | How this plan complies |
|---|--------------------------|--------|------------------------|
| 1 | Clean code / SOLID (§3.1, §3.2) | ✅ PASS | Layered projects + DI everywhere; services small and single-purpose; interfaces per contract |
| 2 | One class per file (§3.2.1, §4.5) | ✅ PASS | File naming = type name; enforced via code review checklist and analyzer task in tasks.md |
| 3 | Separation of concerns (§3.3) | ✅ PASS | `Core` (domain/application) / `Data` (infrastructure) / `UI` (presentation) / `App` (platform shell) projects; no cross-boundary leakage |
| 4 | Soft-delete pattern (§3.4) | ✅ PASS | All entities carry `IsDeleted`/`DeletedAt`/`DeletedBy` **except** Fee, Payment, Transaction (explicit constitutional exemption — immutable financial records, corrections via GL reversals) |
| 5 | Member & financial data preservation (§3.5, §3.6) | ✅ PASS | Members soft-delete only; financial records never edited/deleted; reversing GL transaction pairs for all corrections (data-model.md) |
| 6 | Vertical slice modules, no MediatR/CQRS (§4.1) | ✅ PASS | Module folders (Members, Rehearsals, Events, Finance, Settings, Dashboard, Reports) inside each layer project; direct service injection; repositories centralized in DAL per FR-042 (spec-mandated exception to module-owned infrastructure — see Structure Decision) |
| 7 | Dashboard tiles / Settings tabs / Menu providers (§4.2, §4.3, §4.6) | ✅ PASS | `IDashboardTileProvider`, `ISettingsTabProvider`, `IMenuItemProvider` contracts in `StageFright.Plugins.Contracts` (contracts/plugin-contracts.md) |
| 8 | Custom exceptions at boundaries (§5) | ✅ PASS | Exception taxonomy (`ValidationException`, `DataAccessException`, `ImportException`, `PluginLoadException`, `EntityNotFoundException`, `GLBalanceException`, …) in `StageFright.Core.Exceptions`; infrastructure adapters translate before returning |
| 9 | Serilog + OpenTelemetry (§6) | ✅ PASS | Configured in App startup; structured logs for all required events (spec Observability Requirements); spans for batch ops, tile loads, import/export |
| 10 | Tech stack: MAUI Blazor Hybrid, C# 14, Radzen, no custom JS, no web hosting (§7) | ✅ PASS | Single BlazorWebView; printing/CSV handled in C# (QuestPDF/CsvHelper), no JS interop business logic |
| 11 | UI components in separate class library (§7.2) | ✅ PASS | `StageFright.UI` Razor class library consumed by the MAUI `App` host |
| 12 | Plugin architecture: contracts + runtime discovery (§8) | ✅ PASS | `Plugins/` directory scan via `AssemblyLoadContext`; provider contracts only; failures logged and skipped |
| 13 | Testing: exhaustive code-path coverage, bUnit, integration, acceptance (§11) | ✅ PASS | Test projects per layer + integration/acceptance suites; every user story mapped to UI integration tests; merge gate per NFR-005 |

**Initial gate result: PASS — no violations; Complexity Tracking not required.**

**Post-design re-check (after Phase 1)**: PASS. The data model (data-model.md) honors soft-delete exemptions exactly as the constitution prescribes; all contracts (contracts/) are interface-based extension points; the centralized DAL deviation from §4.1 module-owned infrastructure is required verbatim by FR-042/NFR-017 (spec-level mandate, documented in Structure Decision below) and preserves SRP via per-entity repositories.

## Project Structure

### Documentation (this feature)

```text
specs/001-initial-mvp/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
│   ├── plugin-contracts.md       # IDashboardTileProvider, ISettingsTabProvider, IMenuItemProvider, IDataAccessProvider
│   ├── report-contracts.md       # IReportProvider, ReportData structures, viewer contract
│   ├── data-access-contracts.md  # Repository interfaces, unit-of-work, DbContext shape
│   └── backup-format.md          # Protobuf backup manifest + entity envelope schema
└── tasks.md             # Phase 2 output (/speckit-tasks command — NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
StageFrightCommunity.sln
src/
├── StageFright.App/                     # MAUI Blazor Hybrid host (platform-only container)
│   ├── MauiProgram.cs                   # DI composition root, Serilog/OTel, plugin discovery
│   ├── MainPage.xaml(.cs)               # Hosts single BlazorWebView (loaded programmatically)
│   ├── Platforms/                       # Windows / MacCatalyst bootstrap only
│   └── wwwroot/                         # index.html, bootstrap, app.css (light/dark themes)
│
├── StageFright.Core/                    # Domain + application logic (no EF, no UI)
│   ├── Entities/                        # Member.cs, Fee.cs, Transaction.cs, … (one type per file)
│   ├── Enums/                           # MemberStatus.cs, FeeType.cs, PaymentMethod.cs, …
│   ├── Exceptions/                      # ValidationException.cs, DataAccessException.cs, …
│   ├── Contracts/                       # Repository + service interfaces (IMemberRepository.cs, …)
│   └── Modules/                         # Vertical slices: application services per module
│       ├── Members/                     # MemberService, AgeCalculationService, CommitteeService, …
│       ├── Rehearsals/                  # RehearsalService, AttendanceService
│       ├── Events/                      # EventService, ParticipationService
│       ├── Finance/                     # PaymentService, FeeService, GLAccountAssignmentService,
│       │                                #   FifoAllocationService, ReactivationForgivenessService
│       ├── Settings/                    # SettingsService, CommitteeAnnualResetService, BackupService
│       ├── Dashboard/                   # Core tile providers (Members/Rehearsals/Events/Finance tiles)
│       └── Reports/                     # Report data generators (per-module IReportProvider impls)
│
├── StageFright.Data/                    # Centralized DAL (FR-042): EF Core + SQLite
│   ├── StageFrightDbContext.cs
│   ├── Migrations/
│   ├── Repositories/                    # MemberRepository.cs, FeeRepository.cs, GLRepository.cs, …
│   ├── Configurations/                  # IEntityTypeConfiguration<T> per entity
│   └── PluginData/                      # Plugin DbContext discovery + migration runner
│
├── StageFright.Plugins.Contracts/       # Extension points (referenced by core AND plugins)
│   ├── IDashboardTileProvider.cs
│   ├── ISettingsTabProvider.cs
│   ├── IMenuItemProvider.cs
│   ├── IReportProvider.cs
│   └── IDataAccessProvider.cs
│
├── StageFright.Reports/                 # Shared report infrastructure
│   ├── Models/                          # ReportData.cs, ReportColumn.cs, ReportFilter.cs
│   ├── Rendering/                       # PdfReportRenderer.cs (QuestPDF), CsvReportExporter.cs
│   └── Registry/                        # ReportProviderRegistry.cs (discovery + error isolation)
│
└── StageFright.UI/                      # Razor class library — ALL application UI
    ├── App.razor                        # Blazor Router (single UI entry point)
    ├── Layout/                          # ShellLayout.razor (brand strip + nav bar), ThemeProvider
    ├── Pages/                           # Module pages with @page directives
    │   ├── Dashboard/ Members/ Rehearsals/ Events/ Finance/ Reports/ Settings/ Setup/
    ├── Shared/                          # ReportViewer.razor, TabControl.razor, ConfirmDialog.razor, …
    └── wwwroot/                         # CSS isolation bundles, theme variables

tests/
├── StageFright.Core.Tests/              # Unit tests: services, calculations, validation, exceptions
├── StageFright.Data.Tests/              # Integration: repositories, migrations, GL balance, FIFO
├── StageFright.Reports.Tests/           # PDF/CSV rendering, provider registry error isolation
├── StageFright.UI.Tests/                # bUnit: components, pages, navigation, themes
├── StageFright.Integration.Tests/       # Cross-layer workflows + UI user journeys per story
└── StageFright.TestPlugin/              # SC-007 fixture: sample plugin (tile + report + entity)

Plugins/                                 # Auto-created at runtime (FR-021); not in source tree
```

**Structure Decision**: Layered solution with vertical-slice module folders inside each layer. The spec's own namespace standard (spec §5: `StageFright.Core.*`, `StageFright.Data.Repositories`, `StageFright.Plugins.Contracts`, `StageFright.Reports.*`, `StageFright.UI.*`) prescribes this shape, and FR-042/NFR-017 mandate a **centralized DAL** — so module slices own their Domain/Application/UI pieces (Constitution §4.1) while infrastructure (repositories) is consolidated in `StageFright.Data`. `StageFright.Plugins.Contracts` is a leaf assembly with no dependencies so external plugins reference only it. `StageFright.App` contains zero application logic (NFR-001): it composes DI, discovers plugins, and hosts the single `BlazorWebView` programmatically (avoiding the previously-fixed XAML startup crash, commit `acdad7b`).

## Architecture Notes (key implementation decisions)

1. **Navigation**: Blazor Router owns all routing (`@page` directives); `NavigationManager.NavigateTo` for every transition; MAUI Shell routing disabled (single root ContentPage hosting BlazorWebView). First-run detection redirects to `/setup` before the dashboard.
2. **Financial integrity**: Every Fee/Payment operation wraps creation + paired GL transactions + balance verification in one ACID `DbContext` transaction (spec §6 Pass #3 Q5). `GLBalanceException` thrown and rolled back on imbalance. GL is the source-of-truth for balances: `outstanding = Σ(debits, member) − Σ(credits, member)`.
3. **Provider discovery**: Core (MVP) providers register via DI at startup; plugin assemblies in `Plugins/` load via per-plugin `AssemblyLoadContext`, scanned for contract implementations; every failure is caught → structured log → skip (never blocks startup).
4. **Reports pipeline**: module `IReportProvider` → `ReportData` (rows/columns/headers + sections/subtotals) → shared `ReportViewer.razor` (modal "Generating report…", synchronous, no caching) → `PdfReportRenderer` (QuestPDF) or `CsvReportExporter` (CsvHelper). Cancel option appears after 5 s (FR-047).
5. **Theming**: Bootstrap 5.3 `data-bs-theme` attribute toggled on root element; pastel palette via CSS custom properties; preference persisted in Settings entity; automated WCAG AA contrast tests (NFR-006).
6. **Backup/restore**: protobuf-net envelope with `schemaVersion` (semver), timestamp, and per-entity-type collections; import = validate version + completeness of all 10 entity types → mandatory pre-import backup → user confirmation → single-transaction PK-based upsert.
7. **Audit trail**: `AuditTrailService` invoked from repositories/services on every mutation (user fixed to `"system"` in MVP); 12-month retention purged at startup with failure tolerance (FR-022).

## Complexity Tracking

> Fill ONLY if Constitution Check has violations that must be justified

*No violations — table intentionally empty.*
