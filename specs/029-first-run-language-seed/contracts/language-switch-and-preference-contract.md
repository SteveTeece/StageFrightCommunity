# Contract: Language Switch, Preference Store & Startup Routing

This is a UI/service contract (not a public API/CLI) — it documents the identifiers other components and tests code against: the new interface, the new component method, the new route, and the amended startup ladder.

## `ILanguagePreferenceStore` (`StageFright.Core.Contracts`)

```csharp
public interface ILanguagePreferenceStore
{
    string? Get();
    void Set(string cultureCode);
}
```

- `Get()` returns the last-recorded BCP-47 culture code, or `null` if none was ever recorded or the underlying store could not be read.
- `Set(cultureCode)` persists `cultureCode` as the new value. Idempotent; last write wins. Never throws — a write failure is caught inside the implementation and logged, not surfaced to the caller (matching `ISystemCultureProvider`/`IDeviceThemePreferenceProvider`'s existing never-throw contract).
- Implementation: `MauiLanguagePreferenceStore` (`StageFright.App`), backed by `Microsoft.Maui.Storage.Preferences.Default`. Registered as a DI singleton.

**Consumers**:
| Caller | When | Effect |
|---|---|---|
| `FirstRunLanguageScreen` | On confirm | `Set(selectedCode)` |
| `GeneralSettingsTab.HandleSaveAsync` | After a successful save where the language changed | `Set(selectedCode)` |
| `App.razor.cs` (`OnInitializedAsync`) | Every launch, before any navigation, only when setup is incomplete | `Get()` — routes to `/language-select` when null/blank, else `/setup` |
| `LanguageProvider.ResolveStartupCultureAsync` | Every launch, ladder step 2 | `Get()` — used only when step 1 (explicit `Settings.LanguageCode`) yielded nothing |

## `CultureProvider.Switch(CultureInfo culture)` (`StageFright.UI.Layout`)

```csharp
public void Switch(CultureInfo culture)
```

- Synchronous. Sets the two process-wide globals `CultureInfo.DefaultThreadCurrentCulture` and `CultureInfo.DefaultThreadCurrentUICulture` to `culture` — and **only** those, never the `AsyncLocal`-backed `CultureInfo.CurrentCulture` / `CurrentUICulture`. A per-execution-context value pinned on the latter pair shadows the globals on the Blazor renderer's own execution context, so later renders keep the pre-switch language until a full process restart (spec 029's T036 defect — an in-session switch that persisted and navigated on but left the destination in the old language). `MauiProgram.RunStartupSequence` is held to the same "globals only" rule so nothing pins an override. Then updates the public `CurrentCulture` property to `culture`; calls `StateHasChanged()`.
- Does not persist anything — persistence (`ILanguagePreferenceStore.Set` / `SettingsService.SaveAsync`) is always the caller's responsibility, called immediately before or after `Switch` at each of its two call sites.
- Consumed via `[CascadingParameter] private CultureProvider? CultureProvider { get; set; }`, the same pattern already used for `[CascadingParameter] private ThemeProvider? ThemeProvider { get; set; }` on `SetupWizard`/`GeneralSettingsTab`/`ReviewTab`.
- Re-render guarantee: because `CultureProvider.razor` wraps `ChildContent` in `<CascadingValue Value="this" IsFixed="false">` and is the outermost wrapper in `ShellLayout.razor` (around `ThemeProvider` and `@Body`), a `Switch` call's `StateHasChanged()` propagates a re-render through the entire routed page — every `@L["Key"]` and `MoneyFormatter.Format(...)` call site re-evaluates against the new ambient culture. "Ambient culture" here is `CultureInfo.CurrentUICulture` / `CurrentCulture`, which — with no per-execution-context override anywhere (see the `Switch` note above and the matching rule in `MauiProgram.RunStartupSequence`) — read straight through to the `DefaultThreadCurrent*` globals `Switch` just set, so the new value is live on the very next render regardless of which execution context runs it. No other component needs to change to pick up the switch; this holds for every existing and future page, since `ShellLayout` is the router's one `DefaultLayout`.

**Consumers**:
| Caller | When |
|---|---|
| `FirstRunLanguageScreen` | Immediately on confirm, before continuing to `/setup` or the sample-data seeding step |
| `GeneralSettingsTab.HandleSaveAsync` | Immediately after a successful save where the language changed |

## Route: `/language-select` (`FirstRunLanguageScreen`, `StageFright.UI.Pages.Setup`)

- New page, `@page "/language-select"`, rendered inside the existing `ShellLayout` (the Router's sole `DefaultLayout` — no new layout is introduced).
- Reachable only via `App.razor.cs`'s startup redirect (see below) — no menu item, no link to it from anywhere else in the app (FR-005: it must never be shown again once a preference is recorded).
- On confirm:
  - Always: `ILanguagePreferenceStore.Set(selectedCode)`, then `CultureProvider.Switch(...)`.
  - If "Load sample data" was ticked (Debug builds only, `IDebugDataSeeder` resolved): `SetupService.InitializeAsync(placeholderRequest)` (guarded by `!IsSetupCompleteAsync()`, so it runs at most once) → `IDebugDataSeeder.SeedAsync(progress)` → `Nav.NavigateTo("/dashboard")` on success; on any failure, show the error and stay on `/language-select` (FR-015). Re-pressing Confirm after a failed seed retries `SeedAsync` only — `InitializeAsync` is skipped because setup is already complete.
  - Otherwise: `Nav.NavigateTo("/setup")`.

## Amended startup routing (`App.razor.cs`)

```text
OnInitializedAsync:
  if Diagnostics.HasStartupError → "/startup-error" (unchanged)
  else if NOT IsSetupCompleteAsync():
      if ILanguagePreferenceStore.Get() is null/blank → "/language-select"   (NEW)
      else                                              → "/setup"           (unchanged target, new guard)
  else → no redirect (unchanged)
```

## Amended startup culture ladder (`ILanguageProvider.ResolveStartupCultureAsync`, FR-006)

```text
1. Explicit Settings.LanguageCode naming a shipped language        (unchanged — requires the database)
2. ILanguagePreferenceStore.Get() naming a shipped language        (NEW)
3. OS display language, when a matching resource set ships          (was step 2)
4. en-AU (SupportedLanguagesCatalog.DefaultCultureCode)              (was step 3, ultimate fallback)
```

Every step keeps the existing try/catch/fall-through contract: an unreadable/unresolvable value at any step drops to the next step rather than throwing (FR-017 of spec 027, unchanged).
