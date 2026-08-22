# Plugin Contracts — Living Spec

> [DRAFT] Surface-first draft from existing code — every requirement is observed from the code surface unless tagged otherwise. Review before trusting.

## Purpose

`StageFright.Plugins.Contracts` is the shared vocabulary that lets core modules and external plugins extend the dashboard, settings, navigation, and data layer without either side depending on the other's implementation. Without it, every extension point (tiles, settings tabs, menu items, plugin schemas) would need a direct reference into the concrete host or module code, making the app un-pluggable and forcing a rebuild of the host to add any new feature.

## Requirements

### The contracts assembly stays a dependency-free leaf

The assembly SHALL depend on nothing but DI abstractions (`Microsoft.Extensions.DependencyInjection.Abstractions`) and never reference `StageFright.Core`, `StageFright.Data`, `StageFright.UI`, or `StageFright.App`. This is what makes it safe for both the host and third-party plugin assemblies to reference: neither pulls in the other's implementation or transitive dependency graph.

#### Scenario: a third-party plugin project references the contracts assembly
- **WHEN** a plugin author adds a package/project reference to `StageFright.Plugins.Contracts`
- **THEN** no core application logic, EF Core context, or UI framework code is pulled in transitively
- **AND** the plugin can be compiled and distributed independently of the host application's internals

### Dashboard tiles are contributed without the dashboard knowing concrete tile types

A provider SHALL declare a stable identity, a Blazor component type to render, and a `DashboardTileSize`, while the host aggregates and lays out tiles using only that declared metadata. Core-owned tiles and plugin-owned tiles SHALL coexist by convention (core `DisplayOrder` 0–99, plugin 100+) rather than by any hardcoded list.

#### Scenario: a plugin adds a dashboard metric tile
- **WHEN** a plugin registers an `IDashboardTileProvider` with `DisplayOrder` ≥ 100
- **THEN** its tile is sorted alongside core tiles purely by `DisplayOrder`, with no dashboard code change required
- **AND** the tile renders at its declared `TileSize` using the same layout grid as core tiles

#### Scenario: a tile provider throws while loading data
- **WHEN** `GetTileDataAsync` throws for one provider
- **THEN** that tile shows an error/"unable to load" state
- **AND** every other tile still loads and renders normally [inferred: verified in `DashboardService.LoadTileAsync`, which lives in `StageFright.Core`, outside this assembly — this contracts assembly only documents the guarantee it makes to plugin authors]

### Settings tabs are contributed as self-contained, independently-failing units

A provider SHALL supply its own tab chrome (title, icon, key) and a single component type that owns its own content, validation, and save/cancel — the Settings page SHALL NOT need to know what is inside a tab to host it. Core tabs occupy the 0–99 `DisplayOrder` band and plugin tabs 100+, matching the dashboard-tile convention so the two extension points behave predictably together.

#### Scenario: a plugin adds an account-configuration tab
- **WHEN** a plugin registers an `ISettingsTabProvider` with a unique `TabKey`
- **THEN** the tab becomes deep-linkable at `/settings?tab={TabKey}` without any change to the Settings page shell
- **AND** the tab's own component is fully responsible for its internal state and persistence

### Menu items are contributed declaratively, including nested navigation

A provider SHALL describe its navigation contribution as data (`MenuItem`, optionally with `SubItems`) rather than rendering markup itself, so the shell owns the single source of truth for nav chrome, expansion behavior, and ordering across modules and plugins. Rendering order SHALL place Dashboard first, then module items by `DisplayOrder`, then plugin items, then Settings last.

#### Scenario: a plugin contributes a top-level nav entry with children
- **WHEN** a plugin's `IMenuItemProvider` returns a `MenuItem` with populated `SubItems`
- **THEN** the shell renders it as an expandable group that auto-expands while a child route is active, using the same rendering the core modules get

#### Scenario: two providers declare the same route
- **WHEN** a plugin's menu item collides on `Route` with an existing item
- **THEN** the conflict is logged and the later registrant is skipped rather than silently overriding or crashing navigation

### Reports are contributed as data, decoupled from rendering and export

A provider SHALL produce report content as a `ReportData` value (rows/columns/sections) rather than a rendered document, so the single reports pipeline (viewer → PDF/CSV renderer) works identically for core and plugin reports. `IReportProvider` is documented at this leaf's extension-point boundary but its concrete definition lives in `StageFright.Reports` (which already depends on this assembly) to avoid a circular project reference — `StageFright.Reports` cannot be referenced from here without breaking the leaf-assembly rule above. [NEEDS CLARIFICATION: should `IReportProvider` be relocated into this assembly (with `ReportData` following it) now that it's treated as a first-class extension point, or is the current split-location design intentional and permanent?]

#### Scenario: a plugin adds a custom report
- **WHEN** a plugin registers an `IReportProvider` with a unique `ReportId`
- **THEN** the report appears in the Reports menu grouped under its `ModuleName`
- **AND** generating it reuses the same viewer, PDF renderer, and CSV exporter as every core report, with no plugin-specific rendering code

#### Scenario: report generation fails
- **WHEN** `GenerateAsync` throws for a given report
- **THEN** the caller catches the failure and surfaces a user-friendly error instead of the report page crashing

### Plugins bring their own schema without colliding with core or other plugins

A provider SHALL declare a unique `PluginName` used both as the table-name prefix and as the suffix of its own EF Core migrations-history table, and SHALL register its own repositories/services rather than the host wiring them up by hand. This lets the migration runner merge an arbitrary number of plugin schemas into the single shared SQLite database safely.

#### Scenario: two plugins each ship a `DbContext`
- **WHEN** two `IDataAccessProvider` implementations with distinct `PluginName` values are loaded
- **THEN** each plugin's tables and migration history live in non-colliding, prefixed/suffixed namespaces within the same database file
- **AND** neither plugin's schema changes require a change to the core `StageFrightDbContext`

### Every identity a provider declares is deduplicated, never silently overwritten

Every extension point that exposes a stable identifier (`TileId`, `TabKey`, `ReportId`) or a routable path (`MenuItem.Route`) SHALL treat a collision as a skip-and-log event, not a crash and not a silent replace. This is what lets an unrelated, possibly buggy plugin coexist with core registrations without corrupting the surface a well-behaved plugin or module already claimed.

#### Scenario: a plugin reuses a `TileId` already claimed by a core tile
- **WHEN** the plugin's `IDashboardTileProvider.TileId` matches an already-registered tile
- **THEN** the duplicate is skipped with a warning log
- **AND** the original registration keeps rendering unaffected

### A plugin assembly that fails to load never blocks application startup

The host SHALL isolate failures at the granularity of a single plugin assembly: a plugin that throws during discovery, type-loading, or registration SHALL be caught, logged as a plugin-specific exception, and skipped, while the rest of the application — including other plugins — continues starting normally. This is the promise the whole contracts surface is built on: implementing one of these interfaces is safe to get wrong. [inferred: the catch/log/skip behavior itself is implemented in the host's plugin loader outside this assembly, not in `StageFright.Plugins.Contracts`; this assembly's interfaces are what make that isolation meaningful by keeping each extension point small and independently instantiable]

#### Scenario: a plugin assembly throws during load
- **WHEN** loading a `.dll` from the plugins directory raises any exception (bad IL, missing dependency, constructor failure, etc.)
- **THEN** the failure is wrapped and logged
- **AND** every other plugin assembly and the host application continue starting normally

## Uncovered

_None — every file in the area was read._
