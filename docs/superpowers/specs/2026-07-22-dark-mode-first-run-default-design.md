# Dark Mode First-Run Default — Design

## Problem

The app should default to Dark mode on first run (following the OS/device theme preference where available, falling back to Dark when the OS preference can't be determined), and the user's chosen theme must persist across sessions.

Theme persistence across sessions is already fully implemented (see "Existing behavior" below) — the only gap is that `SetupService.InitializeAsync` unconditionally hardcodes `Theme.Light` for every new install, and `ThemeProvider`'s pre-setup fallback does the same.

## Existing behavior (no changes needed)

- `Theme` enum (`src/StageFright.Core/Enums/Theme.cs`): `Light` / `Dark`.
- `Settings.Theme` (`src/StageFright.Core/Entities/Settings.cs:72`) is a column on the singleton `Settings` row, persisted via `SettingsRepository.SaveAsync` (`src/StageFright.Data/Repositories/SettingsRepository.cs`), stored as TEXT since the initial migration.
- `ThemeProvider` (`src/StageFright.UI/Layout/ThemeProvider.razor(.cs)`) reads `Settings.Theme` on init, renders `data-bs-theme="light|dark"`, and `ToggleAsync()` persists changes back through `ISettingsService`. Two UI toggles already call it: the sidebar (`ShellLayout`) and the Settings page (`GeneralSettingsTab`).
- This round-trip (toggle → save → reload on next launch) is already covered by `tests/StageFright.Integration.Tests/Scenarios/V10_ThemeTests.cs`.

## Change 1: Platform theme detection abstraction

`StageFright.Core` has no MAUI dependency (confirmed via its `.csproj`), so OS theme detection must be exposed through an interface Core can consume without referencing MAUI types.

- **`StageFright.Core/Enums/PlatformThemePreference.cs`** — new enum: `Unspecified`, `Light`, `Dark`. Mirrors MAUI's `AppTheme` without Core depending on it.
- **`StageFright.Core/Contracts/IDeviceThemePreferenceProvider.cs`** — new interface: `PlatformThemePreference GetPreference();`
- **`StageFright.App/Platform/MauiDeviceThemePreferenceProvider.cs`** (or similar path under `StageFright.App`) — implements the interface by reading `Application.Current!.RequestedTheme` and mapping `AppTheme.Light → Light`, `AppTheme.Dark → Dark`, `AppTheme.Unspecified → Unspecified`. Registered as a singleton in `MauiProgram.RegisterCoreServices` (`src/StageFright.App/MauiProgram.cs:151`).
- This implementation class is a one-line MAUI API passthrough and is intentionally not unit tested — consistent with the rest of `StageFright.App`, which has zero test coverage today as a composition-root-only project. The actual branching logic (what happens for each preference value) lives in, and is tested via, the two consumers below.

## Change 2: First-run default in `SetupService`

**Superseded during implementation — see addendum below.** `SetupService.InitializeAsync` (`src/StageFright.Core/Modules/Settings/SetupService.cs:67`) currently sets `Theme = Theme.Light` unconditionally. It changes to persist `request.Theme` verbatim (a new field on `SetupRequest`) rather than computing the OS-preference mapping itself — that mapping now lives solely in `ThemeProvider` (Change 3), whose `CurrentTheme` the Setup Wizard reads when building the request. See the addendum for why.

## Change 3: Pre-setup fallback in `ThemeProvider`

Before the Settings row exists (i.e., while the user is still filling out the Setup Wizard itself), `ThemeProvider.OnInitializedAsync` (`src/StageFright.UI/Layout/ThemeProvider.razor.cs:29,33`) currently falls back to `Theme.Light`. It changes to inject `IDeviceThemePreferenceProvider` and map:

- `PlatformThemePreference.Light` → `Theme.Light`
- `PlatformThemePreference.Dark` → `Theme.Dark`
- `PlatformThemePreference.Unspecified` → `Theme.Dark` (fallback)

so the Setup Wizard itself renders in the OS-preferred/Dark theme rather than flashing Light and then switching after setup completes. `ThemeProvider.CurrentTheme` (this same fallback value, or the user's in-wizard toggle choice — see addendum) is what the Setup Wizard passes into `SetupRequest.Theme` for `SetupService` to persist (Change 2).

## Testing

- `SetupServiceTests`: a new theory covering `Theme.Light` and `Theme.Dark` requested via `SetupRequest.Theme`, asserting the resulting `Settings.Theme` matches verbatim.
- `ThemeProviderTests`: replace the two tests that currently assert a hardcoded Light fallback (`Renders_DataBsTheme_Light_ByDefault_WhenSettingsNull`, `CurrentTheme_IsLight_WhenSettingsNull`) with equivalents covering all three `PlatformThemePreference` branches via a mocked provider.
- `V10_ThemeTests.DefaultTheme_IsLight_AfterFirstRunSetup`: renamed/rewritten to `DefaultTheme_MatchesRequestedTheme_AfterFirstRunSetup`, a theory over `Theme.Light`/`Theme.Dark` that calls `SetupService.InitializeAsync` directly (the original version never exercised `SetupService` at all).
- No new tests needed for persistence across sessions — already covered.

## Out of scope

- No MAUI-level (`Application.Current.UserAppTheme`) app-wide theme switching — theming stays entirely CSS/Blazor-level as it is today (`data-bs-theme` + Bootstrap variables + conditional Radzen dark CSS).

## Addendum: Setup Wizard theme toggle (added during implementation)

GitHub issue #248 explicitly asked for a theme toggle on the Setup Wizard itself, which this design's original "no new UI" scoping missed. Confirmed with the user mid-implementation before writing the implementation plan; the plan's Task 3 added a `RadzenSwitch` toggle to `SetupWizard.razor`, bound to the cascaded `ThemeProvider.CurrentTheme`/`ToggleAsync()`. This is why `SetupService` was changed to trust `SetupRequest.Theme` verbatim (Change 2) instead of independently recomputing the OS-preference mapping: the wizard's `ThemeProvider` instance is the single source of truth for "what theme is currently selected," whether that's still the OS-preference default or the user's in-wizard override.
