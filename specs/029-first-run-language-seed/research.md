# Phase 0 Research: First-Run Language Selection & Optional Sample-Data Seeding

Every open question the spec leaves to design (no `NEEDS CLARIFICATION` markers remained in spec.md itself — this phase resolves *how*, not *what*).

## Decision 1 — No-database preference storage: MAUI `Preferences` API behind a new `ILanguagePreferenceStore`

**Decision**: Add `ILanguagePreferenceStore` to `StageFright.Core/Contracts/` (`string? Get()`, `void Set(string cultureCode)` — synchronous, never throws) and a `MauiLanguagePreferenceStore` implementation in `StageFright.App` backed by `Microsoft.Maui.Storage.Preferences.Default`. Registered as a singleton in `MauiProgram.RegisterCoreServices`, exactly like its two siblings.

**Rationale**: The spec's own Assumptions section calls for "the platform's standard per-application key–value preference storage (persists across launches, needs no database, is local to the device)" — that is a direct description of MAUI Essentials' `Preferences` API, which already ships with the app (no new package). The codebase already has the exact shape of abstraction needed for this: `ISystemCultureProvider`/`SystemCultureProvider` (reads OS culture) and `IDeviceThemePreferenceProvider`/`MauiDeviceThemePreferenceProvider` (reads OS theme) are both a thin, synchronous, try/catch-wrapped Core-contract-plus-App-implementation pair with no other dependencies, registered as a singleton. `ILanguagePreferenceStore` is the same shape, just read *and* write.

**Alternatives considered**:
- *Flat file under `FileSystem.AppDataDirectory`* — rejected: would need a hand-rolled read/write/corruption-handling path for a single string, when `Preferences` already provides exactly that with OS-appropriate storage (registry-backed on Windows) and zero new code for the persistence mechanics themselves.
- *A new SQLite table* — rejected outright by the spec ("No new database table or column is introduced") and would defeat the purpose (FR-003 explicitly requires a store that doesn't need the database, since it must be readable before the database is known to exist).

## Decision 2 — In-session culture switch: extend `CultureProvider`, not a page reload

**Decision**: Add a synchronous `Switch(CultureInfo culture)` method to `CultureProvider` (`StageFright.UI/Layout/CultureProvider.razor.cs`) that sets `CultureInfo.DefaultThreadCurrentCulture`, `DefaultThreadCurrentUICulture`, `CurrentCulture`, and `CurrentUICulture` (mirroring exactly what `MauiProgram.RunStartupSequence` already does once at startup), updates the `CurrentCulture` property, then calls `StateHasChanged()`.

> **Correction (2026-09-02 — T036 defect fix).** Setting `CultureInfo.CurrentCulture` / `CurrentUICulture` here (and at startup) was wrong. Those two are `AsyncLocal`-backed: the value set on an event-handler continuation is unwound before the queued render runs, and the value `RunStartupSequence` pinned on the startup thread *shadowed* the `DefaultThreadCurrent*` globals on the Blazor renderer's own execution context — so `Switch` updated the globals yet every later render kept resolving the pre-switch culture until a full process restart (live T036 check: `/setup` stayed English after confirming French). **Fix**: both `RunStartupSequence` and `CultureProvider.Switch` now assign **only** `CultureInfo.DefaultThreadCurrentCulture` / `DefaultThreadCurrentUICulture` (plain process-wide statics). With no per-context override anywhere, `CurrentUICulture` reads straight through to them on every render and the switch is immediate. The `StateHasChanged()`-driven cascade re-render is unchanged — only the culture-assignment lines were.

**Rationale**: `CultureProvider`'s own doc comment already names it as "the seam for a future in-session live language switch" and it already wraps its `ChildContent` in `<CascadingValue Value="this" IsFixed="false">`. With `IsFixed="false"`, Blazor subscribes every descendant that reads it as a cascading value to change notifications; calling `StateHasChanged()` on the provider re-renders its `ChildContent` render fragment, which — because `CultureProvider` is the outermost wrapper in `ShellLayout.razor` around `ThemeProvider` and `@Body` — walks the entire routed page tree and re-invokes every `IStringLocalizer` indexer (`@L["Key"]`) and `MoneyFormatter.Format` call (which itself reads `CultureInfo.CurrentCulture` fresh on every call — see `MoneyFormatter.cs`) against the newly-set culture. No component besides `CultureProvider` itself needs to change to *support* the re-render; only the two save/confirm call sites (first-run screen, Settings) need to *invoke* `Switch`. ~~Because Blazor Hybrid renders on a single dispatcher, setting `CurrentCulture` on the calling thread (an event-handler continuation) reaches the same thread the next render executes on, matching how the existing startup assignment already works reliably today.~~ *(This last sentence proved false — see the Correction above. The re-render itself works; the culture must ride on the `DefaultThreadCurrent*` globals, not on `CurrentCulture`/`CurrentUICulture`, for the next render to see it.)*

**Alternatives considered**:
- *`NavigationManager.NavigateTo(Nav.Uri, forceLoad: true)`* — rejected: `forceLoad: true` reloads the whole `BlazorWebView`, a visible flash/flicker and a much heavier operation than a render pass; it also contradicts the spirit of "no restart" even though the *process* wouldn't restart.
- *A synchronous method vs. an `async Task SwitchAsync` (to mirror `ThemeProvider.ToggleAsync`)* — rejected: `ThemeProvider.ToggleAsync` is `async` because it awaits a `SettingsService` round-trip; `CultureProvider.Switch` does only in-memory `CultureInfo` assignment and a `StateHasChanged()` call, so an `async` signature would add ceremony with nothing to await. Call sites that are already inside an `async` handler simply call it as a plain statement.

## Decision 3 — Startup routing: `App.razor.cs` gains a preference check before the `/setup` redirect

**Decision**: `App.OnInitializedAsync` keeps its startup-error check first, then — only when `SetupService.IsSetupCompleteAsync()` is false — checks `ILanguagePreferenceStore.Get()`. Empty/null routes to the new `/language-select`; a recorded value routes straight to `/setup` as today (Acceptance Scenario 6). When setup *is* complete, behavior is unchanged (no redirect; the router proceeds to whatever route was requested, e.g. the dashboard).

**Rationale**: This is the minimum change that satisfies FR-001 and FR-005 without touching `ISetupService`'s contract (which correctly stays about the database-backed setup state only) or introducing a `Settings`-side "first run" flag (impossible before the database exists — exactly why FR-003 mandates a non-database store in the first place).

**Alternatives considered**:
- *Add a boolean to `ISetupService`* — rejected: setup completeness and language-preference presence are two independent, differently-scoped facts (one DB-backed, one not); conflating them into one service would blur `ISetupService`'s single responsibility.

## Decision 4 — Startup culture ladder gains a tier, not a rewrite

**Decision**: `LanguageProvider.ResolveStartupCultureAsync` takes a constructor dependency on the new `ILanguagePreferenceStore` and inserts its read as the new step 2 (between the existing explicit-`Settings.LanguageCode` step and the OS-language step), matching FR-006 exactly. Steps 1, 3 (renumbered 4) stay byte-for-byte the same try/catch/fallthrough shape already established (`SafeCulture`, `MatchOperatingSystemCulture`) — this is additive, not a rewrite.

**Rationale**: Keeps `LanguageProvider` a single, linear ladder exactly as its own doc comment already documents (just with one more rung), preserving the "never throws, always falls through" guarantee the existing tests already pin down.

**Alternatives considered**: None — the spec's FR-006 states the exact four-step order; there is only one place this logic can live without duplicating the ladder.

## Decision 5 — Setup wizard: delete the language/sample-data widgets and the bypass mechanism outright

**Decision**: Remove `LanguageSelectionTab` and `SampleDataTab` (components + tests) entirely, remove `SetupWizard`'s `_seedWithTestData`/`_debugSeeder`/`IsTabBypassed`/`HandleSeedWithTestDataChanged` and the `Disabled="@_seedWithTestData"` tab attributes, and remove `ReviewTab`'s `DebugSeederAvailable`/`SeedWithTestData` parameters and its "Load sample data" summary row. `SetupWizard.HandleValidSubmitAsync` sources `SetupRequest.LanguageCode` from the cascaded `CultureProvider.CurrentCulture.Name` instead of `SetupFormModel.LanguageCode` (the field itself can be dropped from `SetupFormModel`, since nothing sets it anymore once the tab is gone).

**Rationale**: FR-016/FR-017 are explicit — no language selector, no sample-data option, no variation in the step list — and FR-014 confines sample-data loading to the first-run screen alone. Since `IDebugDataSeeder` is never invoked from the wizard anymore, `SetupWizard` no longer needs to resolve it at all.

**Alternatives considered**:
- *Keep the components but hide them* — rejected: dead code that can silently regress (e.g. a future tab reintroducing the removed picker by copy-paste) is worse than deletion; the spec explicitly retires this behavior, it doesn't hide it.

## Decision 6 — Sample-data seeding: reuse the existing "initialise then seed" sequence verbatim, from the new screen

**Decision**: The first-run screen's confirm handler, when "Load sample data" is ticked, builds a `SetupRequest` using `SetupFormModel`'s existing defaults (annual fee 0, currency `"AUD"`, tax off, etc.) plus a short non-localized placeholder organisation name, the chosen `LanguageCode`, and `Theme` from the cascaded `ThemeProvider`, then calls `SetupService.InitializeAsync(request)` followed by `IDebugDataSeeder.SeedAsync(progress)` — the exact same two calls, in the exact same order, with the exact same seeding-overlay UX (`_seedingInProgress`/`_seedingProgress`, the `setup-seeding-overlay` CSS class), that `SetupWizard.HandleValidSubmitAsync` already performs today.

**Rationale**: The spec's own Assumptions section states this explicitly — "This feature invokes the existing 'initialise then seed' sequence from the first-run screen; it does not change the dataset's contents" — and `DebugDataSeeder.ApplyGeneratedOrganisationSettingsAsync` already overwrites organisation name/fees/renewal months unconditionally, so the placeholder request's values are never seen by the user; they exist only to satisfy `SetupService.InitializeAsync`'s validation before the seeder overwrites them. The placeholder organisation name follows the same precedent as `DebugDataSeeder`'s own hardcoded English sample content (member names, "Clarence Valley Community Choir") — synthetic seed data, not app-authored UI text, so it is not subject to the localization rule.

**Alternatives considered**:
- *A new "seed-only" service bypassing `SetupService.InitializeAsync`* — rejected: `DebugDataSeeder.SeedAsync` already hard-requires a `Settings` row to exist (`_settingsService.GetAsync(ct) ?? throw new InvalidOperationException(...)`) and the spec forbids changing what the seeder does or how it's invoked.
- *Fixing the pre-existing gap where a seed failure after `InitializeAsync` succeeds still leaves `IsSetupCompleteAsync()` true* — noted, not fixed: this is an existing property of the current wizard flow (an already-committed `Settings` row survives a subsequent seeding exception), not something spec 029 asks to change; it is preserved as-is rather than silently "fixed" as a side effect of this feature.

## Decision 7 — Settings: switch-and-persist on save, restart notice deleted

**Decision**: `GeneralSettingsTab.HandleSaveAsync`, immediately after a successful `SettingsService.SaveAsync` when the language changed, calls `ILanguagePreferenceStore.Set(_selectedLanguageCode)` and `await CultureProvider.Switch(...)` (new `[CascadingParameter]`, same pattern as the existing `ThemeProvider` cascading parameter already on this component). The `LanguageChanged`/`_initialLanguageCode` restart-notice plumbing and the `Settings_General_LanguageRestartNotice` resx key/alert block are deleted outright (FR-010, FR-020, SC-007) — there is nothing to replace the notice with, because the change is visible the moment it is saved.

**Rationale**: Directly implements FR-020/FR-021; reuses the exact cascading-parameter pattern the component already has for `ThemeProvider`, so no new plumbing style is introduced.

**Alternatives considered**: None — this is the one place Settings' language save logic lives.

## Decision 8 — First-run screen: new component, existing resource file, existing seeding UX

**Decision**: `FirstRunLanguageScreen.razor`/`.razor.cs` at `@page "/language-select"`, living in `StageFright.UI/Pages/Setup/` beside `SetupWizard`. It resolves its pre-selected language via `ILanguageProvider.ResolveStartupCultureAsync()` mapped through `ISupportedLanguagesCatalog` (identical logic to the deleted `LanguageSelectionTab.OnInitializedAsync`, since pre-setup this ladder already reduces to "OS language, else en-AU" — FR-002's requirement exactly). Its "Load sample data" control resolves `IDebugDataSeeder` optionally via `IServiceProvider.GetService` (only registered in Debug builds), same as `SetupWizard.OnInitialized` does today, and is rendered with `RadzenSwitch`. New strings are added to `SetupResource` rather than a new resx marker file, since this screen is onboarding text that precedes the wizard.

**Rationale**: Reuses two already-proven pieces of logic (default-language resolution, optional-seeder resolution) verbatim rather than inventing new ones, keeping the change additive.

**Alternatives considered**:
- *A new dedicated resx area (e.g. `FirstRunResource`)* — rejected: adds a 13th area-scoped resource file (plus its own `.qps-ploc`/`en-US`/`fr-FR` culture variants) for a handful of strings that are thematically indistinguishable from `SetupResource`'s existing "before you can use the app" content.
