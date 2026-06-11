# Phase 0 Research: StageFright Community — Initial MVP

**Branch**: `001-initial-mvp` | **Date**: 2026-06-11 | **Plan**: [plan.md](./plan.md)

The spec (after three clarification passes) contains no outstanding `NEEDS CLARIFICATION` items. Research below resolves the technology choices the spec leaves open and records best-practice decisions for each mandated dependency.

---

## R1: Test framework stack

- **Decision**: xUnit (unit/integration/acceptance) + bUnit (Blazor components and UI integration) + NSubstitute (mocking).
- **Rationale**: Constitution §11.1 explicitly requires bUnit for Blazor component tests; bUnit's first-class integration target is xUnit. xUnit is the de-facto .NET standard with parallel runners (Constitution §11.2 performance budgets). NSubstitute gives terse, refactor-safe mocks of the repository/service interfaces ("mock abstractions, not implementations").
- **Alternatives considered**: MSTest/NUnit (weaker bUnit story, less idiomatic for modern .NET); Moq (SponsorLink trust concerns, more verbose setup); FluentAssertions (license change to paid in v8 — use built-in xUnit assertions instead).

## R2: Backup serialization library (FR-012 protobuf mandate)

- **Decision**: **protobuf-net** (code-first attributes on backup DTOs).
- **Rationale**: FR-012 mandates Protocol Buffers binary format. protobuf-net is code-first — `[ProtoContract]`/`[ProtoMember]` attributes on C# DTOs, no `.proto` compiler step in the build, which suits an EF-entity-mirroring backup envelope and keeps the toolchain simple. Field-number-based contracts give the forward-compatible schema versioning FR-014 requires. Mature, MIT-style licensed, .NET 10 compatible.
- **Alternatives considered**: Google.Protobuf + `protoc` (contract-first `.proto` files add a build toolchain step with no MVP benefit; better only for cross-language interop, which is out of scope); MessagePack (not protobuf — violates FR-012); JSON (explicitly rejected in spec clarification "Option B - Binary/Protobuf").

## R3: PDF generation and printing (FR-037, FR-047)

- **Decision**: **QuestPDF** (Community license) generates the PDF in C#; the app saves to a temp file and opens it with the OS default PDF handler (`Launcher.OpenAsync` / `Process.Start`) where the user prints to PDF-file or physical printer via the native print dialog.
- **Rationale**: Constitution §7.3 prohibits custom JavaScript, ruling out `window.print()`/JS-driven printing inside BlazorWebView. QuestPDF has a fluent, strongly-typed C# layout API ideal for tabular accounting reports (headers, column alignment, subtotals, grand totals, page numbers per FR-037). Community license is free for organizations under the revenue threshold — this open community project qualifies. The OS print dialog satisfies "print to PDF or physical printer" without platform-specific printing code.
- **Alternatives considered**: WebView2 `PrintAsync` (Windows-only; Mac Catalyst WKWebView printing differs — would need two platform implementations and prints the live page rather than a professional report layout); iText (AGPL — viral license unacceptable); PdfSharp/MigraDoc (MIT but low-level, much more layout code per report); Syncfusion/Telerik (commercial licensing).

## R4: Plugin assembly loading (FR-021, NFR-011)

- **Decision**: One **`AssemblyLoadContext`** (non-collectible) per plugin directory entry, with `StageFright.Plugins.Contracts` resolved to the host's copy; reflection scan for contract implementations; every load/instantiation failure caught → `PluginLoadException` → structured Serilog error → plugin skipped.
- **Rationale**: `AssemblyLoadContext` is the supported .NET isolation primitive — per-plugin dependency isolation while sharing the contracts assembly (type identity preserved by forwarding contract resolution to the default context). Meets the spec edge case "plugin load failure must not prevent startup". Contracts assembly has zero dependencies so plugins compile against it alone.
- **Alternatives considered**: `Assembly.LoadFrom` into default context (dependency version collisions between plugins); McMaster.NETCore.Plugins (archived/unmaintained wrapper around the same primitive); MEF (heavyweight, poor fit with MAUI DI).

## R5: Plugin data access extensibility (FR-042/FR-043, NFR-017)

- **Decision**: Plugins implement `IDataAccessProvider` exposing their own `DbContext` type (pointed at the shared SQLite file) plus repository registrations. At startup, after core migrations, the DAL's `PluginMigrationRunner` calls `Database.Migrate()` on each plugin context inside the discovery loop, with per-plugin failure isolation. Plugin tables use a plugin-specific prefix (e.g., `PluginName_Entity`) and each plugin context uses its own `__EFMigrationsHistory_<PluginName>` table via `MigrationsHistoryTable(...)`.
- **Rationale**: Separate DbContexts per plugin avoid model merging into the core context (core DAL never modified — FR-042). Distinct migrations-history tables prevent core/plugin migration interference on the shared database. EF Core officially supports multiple contexts on one SQLite database.
- **Alternatives considered**: Single dynamic model merging plugin entities into the core context (requires core model rebuild per plugin set; cache invalidation complexity; violates "no core modification"); separate SQLite file per plugin (violates FR-043 "same SQLite database"; breaks single-file backup).

## R6: Dark/light theming (FR-019/FR-020, NFR-006)

- **Decision**: Bootstrap 5.3 native theming — `data-bs-theme="light|dark"` attribute on the root `<html>` element, toggled by a C# `ThemeService` via Blazor (attribute set on the app root component wrapper, no JS). Pastel palette (HSL lightness 60–80 %, saturation < 50 %) defined as CSS custom properties overriding Bootstrap variables, with dark-theme variants. Preference persisted on the Settings entity and applied at startup.
- **Rationale**: Bootstrap 5.3's color-modes feature is the supported mechanism, removes hand-rolled theme CSS swapping, and lets WCAG AA contrast be verified per theme by automated tests that assert computed token pairs (NFR-006 test requirement).
- **Alternatives considered**: Two stylesheet files swapped at runtime (flash of unstyled content, duplicated palette maintenance); Radzen theme switching alone (covers Radzen components only, not Bootstrap surfaces).

## R7: CSV export (FR-041)

- **Decision**: **CsvHelper**.
- **Rationale**: FR-041 requires correct comma- and quote-escaping for special characters; CsvHelper implements RFC 4180 edge cases (embedded quotes, commas, newlines, culture-invariant formatting) that hand-rolled writers routinely get wrong. Writes from the same `ReportData` row/column structure the viewer uses. Apache-2.0/MS-PL licensed.
- **Alternatives considered**: Hand-rolled `StringBuilder` writer (escaping bugs are exactly what the FR warns about; would need its own exhaustive test matrix); `Sep`/`Sylvan.Data.Csv` (excellent but performance-oriented; CsvHelper's maturity preferred for MVP).

## R8: BlazorWebView hosting pattern (NFR-001)

- **Decision**: Single `ContentPage` (no MAUI Shell) hosting one `BlazorWebView` created **programmatically in C#** (not XAML), root component = `StageFright.UI.App`. All routing via Blazor Router `@page` directives and `NavigationManager`.
- **Rationale**: NFR-001 mandates Blazor-controlled navigation with MAUI as platform-only container. Programmatic BlazorWebView creation also avoids the XAML-initialization startup crash already diagnosed in this repo's history (commit `acdad7b` "Fix startup crash by loading BlazorWebView programmatically") — that lesson is carried forward as the standard pattern.
- **Alternatives considered**: MAUI Shell with routes (explicitly prohibited by NFR-001); multiple BlazorWebViews (breaks single-UI-entry-point mandate).

## R9: Database strategy for integration tests (Constitution §11.2, FR-044)

- **Decision**: **SQLite in-memory connections** (`Microsoft.Data.Sqlite`, `DataSource=:memory:` with the connection held open per test) for repository and integration tests; production-identical migrations applied in test setup.
- **Rationale**: Constitution §11.2 allows "in-memory databases (e.g., EF Core In-Memory Provider) or test doubles", but the EF InMemory provider does not enforce relational behavior the spec depends on (transactions/rollback for the atomic Fee+GL pattern, unique constraints like Member+Year committee membership, FK integrity, decimal handling). SQLite in-memory runs the real provider — the same engine as production — at unit-test speed, satisfying the §11.0 requirement to cover rollback and constraint-violation code paths honestly.
- **Alternatives considered**: EF Core InMemory provider (no transactions, no constraint enforcement — would fake exactly the paths the constitution says must be covered); file-based temp SQLite per test (slower, cleanup overhead; reserved for the few migration tests that need a file).

## R10: Decimal precision for money (FR-038, NFR-015)

- **Decision**: C# `decimal` end-to-end; EF Core column type `TEXT`-affinity decimal mapping via `HasConversion`/`HasPrecision(18, 2)` configured per money property; never `double`/`float`; `Math.Round(…, 2, MidpointRounding.ToEven)` applied only at presentation/report boundaries; GL balance comparisons use the spec's 0.01 tolerance.
- **Rationale**: SQLite has no native decimal type — storing decimals through the default REAL mapping introduces binary floating-point error, directly violating FR-038 "no rounding errors". EF Core's SQLite provider supports decimal-as-TEXT conversion preserving exactness while keeping LINQ usable (ordering/aggregation done client-side where required by provider limitations — acceptable at ≤500-member scale).
- **Alternatives considered**: Store cents as `long` integers (exact and fast, but invasive conversions throughout reporting/UI; spec language is decimal-centric); accept REAL storage (violates FR-038).

## R11: Observability wiring (Constitution §6, NFR-008)

- **Decision**: Serilog as the logging backbone (File sink in app-data `logs/` + Debug sink), `Serilog.Extensions.Logging` bridging `ILogger<T>`; OpenTelemetry SDK with `ActivitySource("StageFright")` spans around batch fee application, dashboard tile loads, import/export, plugin discovery, and report generation; OTel logs/traces correlated via trace IDs enriched into Serilog events. Exporters: console/file for MVP (no remote collector — offline app).
- **Rationale**: Satisfies the hybrid model the constitution mandates while honoring the offline constraint — instrumentation is in place so a collector/exporter can be added in Phase 2 without code changes.
- **Alternatives considered**: Serilog only (violates §6.3); App Center/cloud telemetry (out of scope, offline-only MVP).

## R12: DI and provider discovery for core modules

- **Decision**: Plain `Microsoft.Extensions.DependencyInjection` (MAUI's built-in container) with one `Add<Module>Module()` extension method per module registering its services and its `IDashboardTileProvider`/`IMenuItemProvider`/`ISettingsTabProvider`/`IReportProvider` implementations explicitly. No assembly-scanning library for core modules (plugins use reflection scanning per R4).
- **Rationale**: Constitution §10.2 requires DI and composition; MAUI ships MS.DI natively. Explicit per-module registration extensions match the constitution's own example (`AddMembersModule`) and keep startup deterministic and debuggable; Scrutor-style scanning adds a dependency for no MVP gain.
- **Alternatives considered**: Scrutor assembly scanning (constitution shows it as an option, but implicit registration hampers the exhaustive-coverage goal of knowing exactly what's registered); third-party containers (Autofac etc. — unnecessary).

---

## Summary of resolved unknowns

| Unknown | Resolution |
|---------|------------|
| Test stack | xUnit + bUnit + NSubstitute (R1) |
| Protobuf library | protobuf-net (R2) |
| PDF print pipeline | QuestPDF → temp file → OS print dialog (R3) |
| Plugin loading | AssemblyLoadContext per plugin, shared contracts assembly (R4) |
| Plugin DB extensibility | Per-plugin DbContext + own migrations-history table on shared SQLite (R5) |
| Theming mechanism | Bootstrap 5.3 `data-bs-theme` + CSS custom properties (R6) |
| CSV writer | CsvHelper (R7) |
| MAUI hosting | Programmatic single BlazorWebView, no Shell (R8) |
| Test database | SQLite in-memory with real migrations (R9) |
| Money handling | `decimal` with TEXT-affinity SQLite mapping (R10) |
| Logging/tracing | Serilog + OpenTelemetry, local sinks (R11) |
| DI/registration | MS.DI with per-module registration extensions (R12) |

All Technical Context entries in plan.md are now fully specified — no `NEEDS CLARIFICATION` remains.
