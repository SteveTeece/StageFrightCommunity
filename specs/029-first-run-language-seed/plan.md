# Implementation Plan: First-Run Language Selection & Optional Sample-Data Seeding

**Branch**: `029-first-run-language-seed` | **Date**: 2026-08-30 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/029-first-run-language-seed/spec.md`

## Summary

A new pre-wizard screen (`/language-select`) lets a first-time user pick their display language — and, in Debug builds only, opt into sample-data seeding — before the setup wizard ever renders; the language-selection and sample-data controls are removed from the wizard itself, which becomes a fixed, always-fully-shown flow. The technical core of the feature is making a language change apply **in the running Blazor session with no restart**: `CultureProvider` (already the documented "seam" left by spec 027) gains a `Switch` method that reassigns the process `CultureInfo` and calls `StateHasChanged()`, which its existing `IsFixed="false"` `CascadingValue` propagates as a re-render through the whole app tree it wraps. The choice is additionally persisted outside the database via a new `ILanguagePreferenceStore` (backed by the MAUI `Preferences` API) so a launch that happens before setup completes still comes up in the right language, and `LanguageProvider`'s FR-006 startup ladder gains this as its second tier. Settings' language save adopts the same switch-and-persist call and drops its now-obsolete "restart required" notice.

## Technical Context

**Language/Version**: C# 14, .NET (existing MAUI Blazor Hybrid stack — no change)

**Primary Dependencies**: Existing Blazor/Radzen/BlazorBootstrap UI stack; MAUI Essentials `Microsoft.Maui.Storage.Preferences` for the new no-database language-preference store (ships with MAUI, no new package reference)

**Storage**: SQLite via EF Core (unchanged — no new table/column); MAUI `Preferences` (new) for the single no-database language-preference key, mirroring the existing `IDeviceThemePreferenceProvider`/`ISystemCultureProvider` platform-read pattern

**Testing**: xUnit v3, bUnit, NSubstitute (existing conventions — no change)

**Target Platform**: Windows desktop (MAUI Blazor Hybrid), existing

**Project Type**: Desktop app (existing layered solution — no new project)

**Performance Goals**: SC-004 — clean install to fully-seeded dashboard in under one minute in a Debug build (existing `DebugDataSeeder` budget, unchanged by this feature); SC-002/SC-006 — a language change is visible in the same UI interaction (one Blazor render pass), no perceptible delay

**Constraints**: No application restart or self-relaunch on any language change (FR-004, FR-008, FR-010, FR-020); the setup wizard's step list must never vary based on a sample-data choice (FR-017); sample-data loading must be reachable only from the first-run screen (FR-014)

**Scale/Scope**: One new screen, one new Core contract + platform implementation, edits to six existing files (`App.razor.cs`, `MauiProgram.cs`, `LanguageProvider.cs`, `CultureProvider.razor.cs`, `SetupWizard.razor`/`.razor.cs`, `GeneralSettingsTab.razor`/`.razor.cs`), removal of two wizard tab components (`LanguageSelectionTab`, `SampleDataTab`) and the wizard's tab-bypass mechanism

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Assessment | Notes |
|---|---|---|
| §4.1 Layered Architecture with Module Slices | PASS | New `ILanguagePreferenceStore` contract lives in `StageFright.Core/Contracts/` (centralized, per existing convention); its platform implementation (`MauiLanguagePreferenceStore`) lives in `StageFright.App`, alongside its sibling `SystemCultureProvider`/`MauiDeviceThemePreferenceProvider`; the new screen lives in `StageFright.UI/Pages/Setup/`. No cross-module reach-arounds. |
| §3.2.1 / §4.5 One Class Per File | PASS | Every new type (`ILanguagePreferenceStore`, `MauiLanguagePreferenceStore`, `FirstRunLanguageScreen`) gets its own file, named to match. |
| §4.7 Blazor Component Patterns | PASS | `FirstRunLanguageScreen.razor` + `.razor.cs` paired, no `@code` blocks; no new `.razor.css` needed (reuses the existing `setup-seeding-overlay` global styles). |
| Toggle control standard (CLAUDE.md) | PASS | The new first-run "Load sample data" control uses `RadzenSwitch`, not a raw Bootstrap checkbox — this is new UI, not a migration, so it follows the current standard (the old `SampleDataTab`'s raw checkbox is deleted, not copied). |
| Localization (CLAUDE.md / §11) | PASS | All new user-facing strings resolve through `IStringLocalizer<SetupResource>`; no hardcoded literals. The sample-data seed's own placeholder organisation name follows existing precedent (`DebugDataSeeder`'s hardcoded English sample content is data, not app-authored UI text, and is already unlocalized). |
| §5.2 Custom Exceptions / Exception Boundaries | PASS | No new failure surface — the first-run screen's confirm handler follows the same try/catch shape already used by `SetupWizard.HandleValidSubmitAsync` (`ValidationException` → message; anything else → generic localized error). |
| §11 Testing Standards | PASS (planned) | Every new code path (screen render, confirm with/without sample data, seeding failure, startup routing branches, in-session culture switch, Settings save) gets unit/bUnit/integration coverage in the Tasks phase — see Project Structure below for target test files. |
| Soft-delete / financial immutability | N/A | No entity, no financial data touched. |

No violations — Complexity Tracking is not needed.

## Project Structure

### Documentation (this feature)

```text
specs/029-first-run-language-seed/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md         # Phase 1 output
├── contracts/            # Phase 1 output
│   └── language-switch-and-preference-contract.md
├── quickstart.md         # Phase 1 output
└── tasks.md              # Phase 2 output (/speckit-tasks — not created here)
```

### Source Code (repository root)

```text
src/StageFright.Core/
├── Contracts/
│   └── ILanguagePreferenceStore.cs          # NEW — Get()/Set() over the no-DB preference
└── Modules/Localization/
    └── LanguageProvider.cs                  # EDIT — FR-006 ladder gains the preference-store tier

src/StageFright.App/
├── MauiLanguagePreferenceStore.cs           # NEW — Preferences.Default-backed implementation
└── MauiProgram.cs                           # EDIT — register ILanguagePreferenceStore (singleton)

src/StageFright.UI/
├── App.razor.cs                             # EDIT — route to /language-select vs /setup vs neither
├── Layout/
│   └── CultureProvider.razor.cs             # EDIT — add Switch(CultureInfo) in-session re-render
└── Pages/Setup/
    ├── FirstRunLanguageScreen.razor         # NEW — @page "/language-select"
    ├── FirstRunLanguageScreen.razor.cs      # NEW
    ├── SetupWizard.razor                    # EDIT — drop LanguageSelectionTab/SampleDataTab, tab-bypass attrs
    ├── SetupWizard.razor.cs                 # EDIT — drop _seedWithTestData/_debugSeeder/IsTabBypassed; LanguageCode from CultureProvider
    └── Tabs/
        ├── LanguageSelectionTab.razor       # DELETE
        ├── LanguageSelectionTab.razor.cs    # DELETE
        ├── SampleDataTab.razor              # DELETE
        ├── SampleDataTab.razor.cs           # DELETE
        └── ReviewTab.razor(.cs)             # EDIT — drop DebugSeederAvailable/SeedWithTestData row

src/StageFright.UI/Pages/Settings/
└── GeneralSettingsTab.razor(.cs)            # EDIT — Switch + preference-store write on save; drop restart notice

src/StageFright.UI/Resources/Strings/
└── SetupResource.resx (+ culture variants)  # EDIT — new first-run screen strings; drop restart-notice key from SettingsResource

tests/StageFright.Core.Tests/
├── Localization/LanguageProviderTests.cs               # EDIT — new ladder tier
└── (new) Contracts or Modules/Localization test for the store's Core-side contract shape

tests/StageFright.App.Tests or StageFright.Data.Tests equivalent/
└── (new) MauiLanguagePreferenceStoreTests.cs (if a project exists that can host a MAUI-App-layer unit test; otherwise covered via the Core contract + an integration-level check)

tests/StageFright.UI.Tests/
├── Layout/CultureProviderTests.cs (new) / ThemeProviderTests.cs-equivalent  # NEW — Switch() re-render behaviour
├── Pages/Setup/FirstRunLanguageScreenTests.cs                              # NEW
├── Pages/Setup/SetupWizardTests.cs (existing, extend)                     # EDIT — no language tab, LanguageCode sourced from CultureProvider
├── Pages/Setup/Tabs/SampleDataTabTests.cs                                  # DELETE
├── Localization/LanguagePickerRenderTests.cs                               # EDIT — no LanguageSelectionTab reference
└── Pages/Settings/GeneralSettingsTabTests.cs (existing, extend)            # EDIT — no restart notice, switch+store-write assertions

tests/StageFright.Integration.Tests/Scenarios/
├── V1_FirstRunSetupTests.cs               # EDIT — routing now passes through /language-select
└── (new) V22_InSessionLanguageSwitchTests.cs — first-run + Settings in-session switch, no restart
```

**Structure Decision**: The feature is additive within the existing five-project layered solution — no new project. The one genuinely new abstraction (`ILanguagePreferenceStore`) follows the established Core-contract / App-platform-implementation split already used for `ISystemCultureProvider` and `IDeviceThemePreferenceProvider`. The one genuinely new UI surface (`FirstRunLanguageScreen`) sits beside `SetupWizard` in `StageFright.UI/Pages/Setup/` since it is onboarding UI shown immediately before it, reusing `SetupResource` rather than introducing a new resx marker file.
