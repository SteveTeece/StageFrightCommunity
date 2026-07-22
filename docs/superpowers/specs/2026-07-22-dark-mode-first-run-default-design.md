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

`SetupService.InitializeAsync` (`src/StageFright.Core/Modules/Settings/SetupService.cs:67`) currently sets `Theme = Theme.Light` unconditionally. It changes to inject `IDeviceThemePreferenceProvider` and map:

- `PlatformThemePreference.Light` → `Theme.Light`
- `PlatformThemePreference.Dark` → `Theme.Dark`
- `PlatformThemePreference.Unspecified` → `Theme.Dark` (fallback)

This is the value written to the `Settings` row and is what persists from the very first launch onward.

## Change 3: Pre-setup fallback in `ThemeProvider`

Before the Settings row exists (i.e., while the user is still filling out the Setup Wizard itself), `ThemeProvider.OnInitializedAsync` (`src/StageFright.UI/Layout/ThemeProvider.razor.cs:29,33`) currently falls back to `Theme.Light`. It changes to use the same `IDeviceThemePreferenceProvider` + fallback-to-Dark mapping as Change 2, so the Setup Wizard itself renders in the OS-preferred/Dark theme rather than flashing Light and then switching after setup completes.

## Testing

- `SetupServiceTests`: three new/updated cases covering `PlatformThemePreference.Light`, `.Dark`, and `.Unspecified` (mocked `IDeviceThemePreferenceProvider`), asserting the resulting `Settings.Theme`.
- `ThemeProviderTests`: replace the two tests that currently assert a hardcoded Light fallback (`Renders_DataBsTheme_Light_ByDefault_WhenSettingsNull`, `CurrentTheme_IsLight_WhenSettingsNull`) with equivalents covering all three `PlatformThemePreference` branches via a mocked provider.
- `V10_ThemeTests.DefaultTheme_IsLight_AfterFirstRunSetup`: rename/rewrite — Light is no longer the unconditional post-setup default; assert the OS-preference-driven behavior instead (e.g. parameterized over the three preference values, or split into `DefaultTheme_FollowsOsPreference_AfterFirstRunSetup` / `DefaultTheme_IsDark_WhenOsPreferenceUnspecified_AfterFirstRunSetup`).
- No new tests needed for persistence across sessions — already covered.

## Out of scope

- No MAUI-level (`Application.Current.UserAppTheme`) app-wide theme switching — theming stays entirely CSS/Blazor-level as it is today (`data-bs-theme` + Bootstrap variables + conditional Radzen dark CSS).
- No new UI for choosing a theme during the Setup Wizard — the default is applied silently; the user can still toggle it afterward via the existing sidebar/Settings controls.
