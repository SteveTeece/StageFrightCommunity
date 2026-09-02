# Phase 1 Data Model: First-Run Language Selection & Optional Sample-Data Seeding

No new database table or column is introduced (per spec Dependencies & the constitution's soft-delete/financial-immutability sections — neither applies, nothing new is persisted in SQLite). This document covers the one new storage concept (a non-database preference) and the shape of the entities the spec's Key Entities section names, expressed as code contracts rather than schema.

## Recorded language preference (new — no-database)

Not a database entity — a single string held in the platform's per-application key-value preference storage (MAUI `Preferences`), read/written through the new `ILanguagePreferenceStore` contract.

| Field | Type | Notes |
|---|---|---|
| Key | `string` (constant, e.g. `"DisplayLanguageCode"`) | Fixed key name inside `MauiLanguagePreferenceStore`; not exposed to callers. |
| Value | `string?` — BCP-47 culture code (e.g. `"en-AU"`, `"fr-FR"`) | `null`/absent means "no explicit choice recorded yet". Written by `FirstRunLanguageScreen`'s confirm handler (FR-003) and by `GeneralSettingsTab.HandleSaveAsync` on a changed save (FR-021). Read by `App.razor.cs` (routing decision, FR-001/FR-005) and by `LanguageProvider.ResolveStartupCultureAsync` (FR-006 step 2). |

**Validation**: None beyond what already exists — a value is only ever written from `_selectedLanguageCode`, which is always a `SupportedLanguage.CultureCode` drawn from `ISupportedLanguagesCatalog.All`. A stored value that no longer matches a shipped culture (e.g. after downgrading the app) is treated as absent by every reader, per the spec's "Stored preference names a language no longer shipped" edge case — `ISupportedLanguagesCatalog.Find` already returns `null` for an unknown code, and callers already fall through on a `null`/`Find`-miss (see `LanguageProvider.ResolveStartupCultureAsync`'s existing step-1 pattern, reused identically for the new step 2).

**State transitions**: write-only-forward — a newer choice always overwrites the prior one; there is no "clear/unset" operation exposed anywhere in this feature (matches the spec's "Overwritten only by a newer choice" note under Key Entities).

## Saved application language (existing — `Settings.LanguageCode`)

No shape change. `Settings.LanguageCode` (`string?`, `StageFright.Core/Entities/Settings.cs`) remains the database-persisted language, set once at setup completion (`SetupService.InitializeAsync`) and updated thereafter by `GeneralSettingsTab.HandleSaveAsync`. This feature only changes *what feeds it at setup time* — previously the wizard's own `LanguageSelectionTab`; now the value the first-run screen already recorded into the no-database preference, read back via the cascaded `CultureProvider.CurrentCulture.Name` at `SetupWizard` Finish (see research.md Decision 5) — and *what happens immediately after* a Settings save (Decision 7).

## Active session culture (existing concept, newly mutable — `CultureProvider.CurrentCulture`)

| Member | Type | Notes |
|---|---|---|
| `CurrentCulture` | `CultureInfo` (public, get-only from outside) | Was read-only-after-`OnInitialized` in spec 027; this feature adds the only way to change it post-initialization. |
| `Switch(CultureInfo culture)` | method (new) | Sets **only** the process-wide `CultureInfo.DefaultThreadCurrentCulture` / `DefaultThreadCurrentUICulture` globals to `culture` (never the `AsyncLocal`-backed `CurrentCulture` / `CurrentUICulture` — a per-context override there shadows the globals on the renderer's execution context and freezes the language until a restart; `MauiProgram.RunStartupSequence` follows the same rule — spec 029's T036 fix), updates the `CurrentCulture` property, calls `StateHasChanged()`. Not persisted by `CultureProvider` itself — persistence is the caller's job (`ILanguagePreferenceStore.Set` and, once the database exists, `SettingsService.SaveAsync`), keeping `CultureProvider` a pure render-layer concern exactly as its existing doc comment describes it. |

**Validation**: `culture` is always resolved by the caller from a `SupportedLanguage.CultureCode` via `CultureInfo.GetCultureInfo(...)` before being passed in — `CultureProvider.Switch` itself does no catalog lookup, keeping it a dumb, testable setter+notifier (consistent with `ThemeProvider.ToggleAsync`'s split of "compute the new value" from "apply and persist it").

## Sample dataset (existing — unchanged)

`DebugDataSeeder`'s output is unchanged in content and generation logic (per spec Out of Scope: "Changing what the sample dataset contains or how it is generated"). The only change is the caller: `FirstRunLanguageScreen` invokes the same `SetupService.InitializeAsync` + `IDebugDataSeeder.SeedAsync` sequence `SetupWizard` used to invoke, with a placeholder `SetupRequest` (see research.md Decision 6) instead of one built from wizard-tab input.

## New contract: `ILanguagePreferenceStore`

```csharp
namespace StageFright.Core.Contracts;

/// <summary>
/// Reads/writes the no-database display-language preference (spec 029, FR-003/FR-006 step 2).
/// Platform-backed (MAUI Preferences); never throws — a read/write failure is caught and
/// swallowed by the implementation, matching ISystemCultureProvider/IDeviceThemePreferenceProvider.
/// </summary>
public interface ILanguagePreferenceStore
{
    /// <summary>The recorded BCP-47 culture code, or null when none has been recorded or the store is unreadable.</summary>
    string? Get();

    /// <summary>Records <paramref name="cultureCode"/> as the current preference. Overwrites any prior value.</summary>
    void Set(string cultureCode);
}
```

Registered as `services.AddSingleton<ILanguagePreferenceStore, MauiLanguagePreferenceStore>()` in `MauiProgram.RegisterCoreServices`, beside `ISystemCultureProvider`/`IDeviceThemePreferenceProvider`.
