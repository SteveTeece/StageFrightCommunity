# App Host — Living Spec

> [DRAFT] Surface-first draft from existing code — every requirement is observed from the code surface unless tagged otherwise. Review before trusting.

## Purpose

The app-host capability is the MAUI composition root that boots the process, wires every service/repository/provider into one DI container, migrates the database, discovers plugins, and hands control to a single Blazor Hybrid surface. Without it there is no running application: no window, no database connection, no navigable UI, and no extension-point wiring for plugins.

## Requirements

### The host is a thin platform shell around one Blazor surface

`StageFright.App` MUST contain no business/domain logic of its own. Its only jobs are: create the native window, host exactly one `BlazorWebView`, and hand rendering off to `StageFright.UI`. MAUI Shell/page navigation is not used — the Blazor `Router` inside the hosted component owns all in-app navigation.

#### Scenario: application launches on any supported platform
- **WHEN** the OS activates the app
- **THEN** a single native window is created sized to the device's current display bounds (via `DeviceDisplay.MainDisplayInfo`, density-corrected)
- **AND** that window's content is one `BlazorWebView` whose root component is the `StageFright.UI` app shell, started at `/dashboard`

### DI composition happens once, in a fixed dependency order, before any UI renders

The composition root MUST register repositories, then core application services, then menu/dashboard/report/plugin extension points, then build the container, and only after that run migrations and plugin discovery against the already-built provider. This ordering exists so every service registered by `RegisterCoreServices` can safely depend on repositories registered by `RegisterRepositories`, and so startup tasks can resolve fully-formed services from a scope.

#### Scenario: a new module is added to the app
- **WHEN** a module's services, repositories, and providers are registered in `MauiProgram`
- **THEN** they become resolvable from any scope created after `builder.Build()` without further wiring elsewhere

### The database auto-migrates at startup, and startup distinguishes recoverable from fatal failures

On every launch the host MUST run EF Core migrations against the SQLite database before the UI is allowed to treat the app as ready. A database-layer failure (connection/update error) MUST be recorded to a diagnostic service and startup MUST continue so the UI can present a recovery path, rather than crashing; any other unexpected failure during migration MUST be treated as fatal and stop startup.

#### Scenario: the database file is corrupted or locked
- **WHEN** `Database.Migrate()` throws a database-level exception
- **THEN** the error is recorded on the startup diagnostic service with the database path
- **AND** the app continues starting instead of terminating

#### Scenario: migration fails for a non-database reason
- **WHEN** `Database.Migrate()` throws an exception that isn't a recognized database error
- **THEN** the exception is logged as fatal and rethrown, stopping startup

### A recorded startup error takes priority over every other routing decision

The Blazor shell MUST check for a recorded startup error before it checks whether first-run setup is complete, so a broken database always routes to a recovery page rather than into the setup wizard or dashboard.

#### Scenario: app has a recorded startup error and setup was never completed
- **WHEN** the Blazor shell initializes
- **THEN** it navigates to the startup-error page
- **AND** it does not evaluate or act on the first-run/setup-complete check at all

### First-run detection redirects to setup before any operational page loads

The app shell MUST ask a setup-completion service whether initial configuration has happened, and if not, redirect to the setup wizard before the dashboard or any other page is reachable.

#### Scenario: app is launched for the first time
- **WHEN** no startup error is recorded and setup has not been completed
- **THEN** the shell navigates to `/setup`

#### Scenario: app has already been configured
- **WHEN** setup is complete and there is no startup error
- **THEN** the shell leaves the requested route (e.g. `/dashboard`) untouched

### The setup wizard captures required configuration through validated steps before anything else can run

The wizard MUST require organisation identity, fee/renewal configuration, and sales-tax treatment (applicability, rate, and per-fee tax codes) before allowing submission, gating advancement on a per-step validation pass rather than only validating at final submit. Completion MUST persist configuration through one setup service call and then route straight into the app.

#### Scenario: user attempts to advance past an incomplete step
- **WHEN** required fields for the current step fail validation
- **THEN** the wizard does not advance to the next step

#### Scenario: user completes all steps
- **WHEN** the final step is submitted successfully
- **THEN** configuration is persisted and the user is navigated to `/dashboard`

### Optional sample-data seeding is a debug-only, opt-in, non-blocking capability

Sample-data seeding MUST NOT be offered or reachable in release builds, MUST require explicit user opt-in even when available, and MUST run without freezing the setup UI, reporting incremental progress back to it. It MUST also be safe to invoke against a database that already has data. The opt-in control lives on the Organisation Settings tab (the wizard's first tab), and selecting it disables the Chart of Accounts, Opening Balances, and Committee tabs — sample data supplies that information itself, so manual entry on those tabs is bypassed rather than merely optional.

#### Scenario: release build reaches the setup wizard
- **WHEN** no debug seeder is registered in the container
- **THEN** the sample-data checkbox is not shown at all, on any tab, and no tab is ever disabled

#### Scenario: user opts into sample data in a debug build
- **WHEN** the checkbox is checked on the Organisation Settings tab and setup is submitted
- **THEN** seeding runs off the UI thread while progress messages update the wizard in place, and normal navigation to `/dashboard` still follows once seeding finishes

#### Scenario: seeding runs against a database that already has member data
- **WHEN** the seeder is invoked and active members already exist
- **THEN** it exits without creating duplicate data

#### Scenario: coordinator selects sample data before reaching the manual-entry tabs
- **WHEN** the checkbox is checked on the Organisation Settings tab
- **THEN** the Chart of Accounts, Opening Balances, and Committee tab headers become unavailable and Next advances directly from Organisation Settings to Review

#### Scenario: coordinator had already queued manual entries before selecting sample data
- **WHEN** the checkbox is checked after an account, opening balance, or committee title was already queued
- **THEN** every queued entry is discarded so it is never submitted alongside the seeded sample data

### Plugin assemblies are discovered and loaded in isolation from each other and from the host

The host MUST scan a known plugins directory for assemblies at startup, load each in its own isolated load context, and contain any single assembly's load failure so it neither aborts discovery of the remaining assemblies nor crashes startup. A missing plugins directory MUST NOT be treated as an error.

#### Scenario: one plugin assembly is malformed or has an unresolvable dependency
- **WHEN** that assembly fails to load or fails while being scanned for provider types
- **THEN** the failure is logged and that assembly is skipped
- **AND** every other assembly in the directory is still loaded and scanned

#### Scenario: the plugins directory doesn't exist yet
- **WHEN** discovery runs before any plugin has ever been installed
- **THEN** the directory is created automatically and discovery reports no plugins found, without failing startup

### Discovered plugin types extend the app only through named extension-point contracts

A plugin assembly MUST contribute behavior solely by implementing one of the host's published extension-point interfaces (dashboard tiles, settings tabs, menu items, or a data-access provider for plugin-owned persistence); the host discovers and wires up any such implementation without the host being recompiled for that plugin.

#### Scenario: a plugin implements a supported extension-point interface
- **WHEN** its assembly loads successfully
- **THEN** every concrete type in it that implements a supported extension-point interface is registered against that interface

### Plugin-owned persistence merges into the same database as core data

A plugin that supplies a data-access provider MUST have its schema migrated into the same SQLite database the core app uses, and this MUST happen only after the core schema has migrated successfully.

#### Scenario: a loaded plugin registers a data-access provider
- **WHEN** core migration succeeds
- **THEN** the plugin's migrations are applied to the same database file before startup continues

### Blazor's Router owns all in-app navigation and isolates render failures per navigation

Exactly one `Router` component MUST own routing for the hosted UI, and it MUST wrap rendered content in an error boundary so an unhandled exception in one page's render produces a recoverable in-app error state instead of a blank or crashed webview, with the boundary reset on every subsequent navigation.

#### Scenario: a page throws during render
- **WHEN** an unhandled exception occurs while rendering the matched route
- **THEN** an in-app error message is shown with a retry action instead of the app becoming unusable

#### Scenario: user navigates away after an error was shown
- **WHEN** the next navigation occurs
- **THEN** the error boundary is reset so the new page gets a fresh chance to render

### The shell layout builds navigation chrome entirely from registered menu-item providers

Sidebar navigation content MUST be assembled at render time from every registered menu-item provider rather than being hard-coded, ordered by each provider's and item's declared order, with support for grouped/expandable entries whose expansion state tracks the active route until the user explicitly overrides it.

#### Scenario: a module (core or plugin) registers a menu-item provider
- **WHEN** the shell renders
- **THEN** that provider's items appear in the sidebar in their declared order, without any change to the shell itself

#### Scenario: user is on a route nested under a group
- **WHEN** the shell renders that route
- **THEN** the containing group is expanded automatically

### The active UI theme is resolved once from persisted settings with a device-preference fallback, then shared app-wide

Theme MUST be read from persisted settings on startup; when no settings exist yet, it MUST fall back to the platform's reported light/dark preference, and MUST default to dark when neither source is available. Once resolved, the theme MUST be available to every descendant component and toggling it MUST persist the change and propagate to all consumers immediately. The host-specific way of reading the OS preference MUST be injected as an abstraction so the shared UI layer stays platform-agnostic.

#### Scenario: settings exist with a saved theme
- **WHEN** the shell initializes
- **THEN** the saved theme is applied and used for the Bootstrap/Radzen theme attribute

#### Scenario: no settings exist yet (pre-setup) and the OS reports a preference
- **WHEN** the shell initializes before setup has run
- **THEN** the OS-reported preference is used

#### Scenario: no settings and no usable device preference
- **WHEN** neither source yields a value
- **THEN** dark is used as the default

## Uncovered

- `src/StageFright.App/Platforms/` — platform-scaffold boilerplate (Android/iOS/MacCatalyst/Windows manifests, entry points), largely generated by the MAUI project template rather than hand-authored app behavior; skipped per scope.

### Sample-data seeding produces no audit trail entries for the records it creates

The debug data seeder SHALL suppress audit trail logging for its entire run, so the synthetic records it creates are not indistinguishable from a burst of real user activity in the audit trail.

#### Scenario: sample data is seeded
- **WHEN** the debug data seeder runs (via the opt-in setup-wizard checkbox)
- **THEN** no audit trail entry is created for any member, rehearsal, attendance, fee, payment, event, AGM, expense, income, deposit, or account record it creates
- **AND** audit trail logging for actions taken after seeding finishes is unaffected
