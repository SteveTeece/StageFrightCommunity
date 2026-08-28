# Contracts: Localization Infrastructure

**Feature**: `027-localization-support` | **Date**: 2026-08-27

No REST/CLI surface. These are the C# interfaces, marker types, and the one entity-field change that components, providers, renderers, and tests code against. The spec pins no Verbatim Constraints, so every identifier below is a plan proposal, not a user-pinned string — implementation may refine names as long as the responsibilities hold.

---

## 1. `ILocalizer` — thin facade (project: `StageFright.Core`)

`src/StageFright.Core/Localization/ILocalizer.cs`

```csharp
namespace StageFright.Core.Localization;

/// <summary>
/// Area-agnostic access to localized strings. Wraps IStringLocalizerFactory so that
/// a missing key for the active culture logs a warning and falls back to the
/// Australian English (neutral) value — never returns blank or the raw key.
/// </summary>
public interface ILocalizer
{
    string Get<TResource>(string key);
    string Get<TResource>(string key, params object[] args);          // named-placeholder formatting
    string Plural<TResource>(string key, int count, params object[] args);  // resolves key + "_One" / "_Other"
    string Enum(System.Enum value);                                   // EnumsResource["Enum_" + type.Name + "_" + value] (FR-024)
}
```

- Consumers that prefer the framework type inject `IStringLocalizer<TResource>` directly; `ILocalizer` is the convenience wrapper used where an area marker would be noisy (e.g. a loop over report columns).
- Blazor components expose a `[Inject] IStringLocalizer<TArea> L` in the code-behind; markup reads `@L["Members_List_Title"]`.
- **Enum display text** (FR-024): `ILocalizer.Enum(value)` — and a `LocalizeEnum` extension method over it — is the only sanctioned way to render an enum value to a user. It resolves `Enum_<EnumTypeName>_<MemberName>` against the shared `EnumsResource` through the same missing-key-logging path. `enum.ToString()` / interpolation of an enum at a display site is a guard-test failure. The enum's name/number stays the culture-invariant identity for storage, sorting, comparison, and `<option value>` / report-filter tokens.

### Implementation

- `MissingKeyLoggingLocalizerFactory : IStringLocalizerFactory` — decorator registered over the default factory from `AddLocalization()`. Delegates creation, wraps each returned `IStringLocalizer` so lookups inspect `LocalizedString.ResourceNotFound`; on `true` it logs `ILogger` `Warning` (`"Missing localization key {Key} for culture {Culture}; fell back to neutral"`) and returns the neutral value.
- `Localizer : ILocalizer` — resolves `IStringLocalizer<TResource>` from the wrapped factory; `Plural` picks `key + "_One"` when `count == 1` else `key + "_Other"` and formats with `{Count}` bound.

### DI registration (in `MauiProgram.RegisterCoreServices`)

```csharp
services.AddLocalization();
// decorate IStringLocalizerFactory with MissingKeyLoggingLocalizerFactory (manual decorator registration)
services.AddScoped<ILocalizer, Localizer>();
```

---

## 2. `ILanguageProvider` — startup culture resolution (project: `StageFright.Core`)

`src/StageFright.Core/Contracts/ILanguageProvider.cs`

```csharp
namespace StageFright.Core.Contracts;

public interface ILanguageProvider
{
    /// <summary>
    /// Resolves the CultureInfo to apply at startup, in order (FR-023):
    /// (1) an explicit Settings.LanguageCode that names a shipped language;
    /// (2) otherwise the OS display language, if the catalog has a match by exact
    ///     culture then by parent language;
    /// (3) otherwise the default culture (en-AU).
    /// Never throws; an unresolvable code or OS culture drops to the next step.
    /// </summary>
    Task<System.Globalization.CultureInfo> ResolveStartupCultureAsync(CancellationToken ct = default);

    /// <summary>The default (fallback) culture — en-AU.</summary>
    System.Globalization.CultureInfo DefaultCulture { get; }
}
```

```csharp
namespace StageFright.Core.Contracts;

/// <summary>Reads the operating-system / device display (UI) culture. One-line App-layer seam so
/// LanguageProvider's resolution ladder is unit-testable without MAUI. Returns CultureInfo.InvariantCulture
/// (or null) when the platform culture cannot be determined — LanguageProvider then uses en-AU.</summary>
public interface ISystemCultureProvider
{
    System.Globalization.CultureInfo GetUiCulture();
}
```

- Implementation `LanguageProvider` (in `Modules/Localization/`) depends on `ISettingsService` + `ISupportedLanguagesCatalog` + `ISystemCultureProvider`.
- `ISystemCultureProvider` is implemented in `StageFright.App` over the device culture (e.g. `CultureInfo.InstalledUICulture` / the MAUI device culture); a fake is used in tests.
- Caller: `MauiProgram` startup scope sets `CultureInfo.DefaultThreadCurrentCulture` / `DefaultThreadCurrentUICulture` from the result **before** first Blazor render.
- An explicit selection is only consulted here for resolution order; it is *not* auto-persisted from the OS language — `LanguageCode` stays `null` until the user actively picks one, so "follow the system language" remains the default behaviour across OS-language changes.

---

## 3. `ISupportedLanguagesCatalog` (project: `StageFright.Core`)

`src/StageFright.Core/Contracts/ISupportedLanguagesCatalog.cs`

```csharp
namespace StageFright.Core.Contracts;

public interface ISupportedLanguagesCatalog
{
    IReadOnlyList<SupportedLanguage> All { get; }          // ordered for the picker
    SupportedLanguage Default { get; }                     // the IsDefault == true entry (en-AU)
    SupportedLanguage? Find(string? cultureCode);          // null when code is null/blank/unknown
}
```

`SupportedLanguage` value object — see `data-model.md` §2. `SupportedLanguagesCatalog` builds `All` **at runtime** by enumerating the resource cultures actually shipped (neutral `en-AU` + any `<Marker>.<culture>.resx` satellites present in the loaded resource assemblies), endonyms from `CultureInfo.NativeName`, cultures matching the pseudo-locale pattern `qps-*` excluded — there is no hand-maintained list (FR-011, resolved 2026-08-27). v1 yields just `{ "en-AU", "English (Australia)", IsDefault = true }` because no satellite ships. `Find` still resolves `null`/blank/unknown to `null`.

Consumers: `GeneralSettingsTab` and the Setup Wizard language step bind their `<select>` to `All`; `LanguageProvider` uses `Find` / `Default`.

---

## 4. Area resource marker classes

Empty classes whose namespace + name locate the `.resx`. One file each (§3.2.1).

| Project | Marker class | `.resx` location | Owns |
|---|---|---|---|
| `StageFright.Core` | `NavigationResource` | `Modules/Localization/Resources/` | menu/tile provider `Title`, `ShortLabel` |
| `StageFright.Core` | `ValidationResource` | `Modules/Localization/Resources/` | user-facing validation + exception `Message` text |
| `StageFright.Core` | `EnumsResource` | `Modules/Localization/Resources/` | display text of user-facing enum members — `Enum_<Type>_<Member>` (FR-024); shared so UI and Reports match |
| `StageFright.Reports` | `ReportsResource` | `Resources/` | report names, filter labels, column headers, section/total labels, PDF fixed annotations |
| `StageFright.UI` | `SharedResource` | `Resources/Strings/` | cross-cutting actions (Save/Cancel/Close/Yes/No), "Loading…", common table headers |
| `StageFright.UI` | `DashboardResource` | `Resources/Strings/` | Dashboard page + tiles |
| `StageFright.UI` | `MembersResource` | `Resources/Strings/` | Members module screens (US1 slice) |
| `StageFright.UI` | `RehearsalsResource` | `Resources/Strings/` | Rehearsals module screens |
| `StageFright.UI` | `EventsResource` | `Resources/Strings/` | Events / AGM module screens |
| `StageFright.UI` | `FinanceResource` | `Resources/Strings/` | Finance module screens |
| `StageFright.UI` | `SettingsResource` | `Resources/Strings/` | Settings page + tabs (incl. language picker + restart notice) |
| `StageFright.UI` | `SetupResource` | `Resources/Strings/` | Setup Wizard steps |

Each `.resx` ships: neutral file (= `en-AU` baseline), and `*.qps-ploc.resx` used only by test fixtures. `<NeutralLanguage>en-AU</NeutralLanguage>` is set in each project file. A real added language ships as `<Marker>.<culture>.resx` beside the neutral file and is picked up automatically by the runtime catalog discovery (§3) — no list edit. The `qps-ploc` set is filtered out of the catalog by its `qps-*` name so it never reaches the picker.

---

## 5. Entity field change

`src/StageFright.Core/Entities/Settings.cs` gains:

```csharp
/// <summary>
/// Selected display language as a BCP-47 culture id (e.g. "en-AU"). Null until the
/// user chooses one; null resolves to the default culture (en-AU). Presentation only —
/// changing it never alters any other stored value (FR-016).
/// </summary>
public string? LanguageCode { get; set; }
```

Migration: `AddLanguageCodeToSettings` (see `data-model.md` §1). `ISettingsService` / `ISettingsRepository` signatures are unchanged — the singleton already round-trips the whole entity.

---

## 6. Blazor consumption pattern (the contract each converted component follows)

`.razor.cs` (code-behind — constitution §4.7):

```csharp
[Inject] private IStringLocalizer<MembersResource> L { get; set; } = null!;
```

`.razor` (markup only, no `@code`):

```razor
<h1>@L["Members_List_Title"]</h1>
<button>@Shared["Shared_Action_Save"]</button>      @* second IStringLocalizer<SharedResource> Shared *@
<p>@L["Members_List_CountSummary", Model.Count]</p>   @* named {Count} placeholder *@
<span>@member.Status.LocalizeEnum()</span>            @* enum display text — never @member.Status.ToString() (FR-024) *@
<button aria-label="@L["Members_List_AddButtonAriaLabel"]">+</button>  @* aria-label / alt / title are localized too (FR-001); decorative alt="" / aria-hidden are exempt *@
<td>@Money.Format(fee.Amount)</td>                   @* FR-015 — fixed "$"/"AUD", culture separators; never @fee.Amount.ToString("C") / FormatString="{0:C}" *@
```

Menu/tile providers (`StageFright.Core`) take `IStringLocalizer<NavigationResource>` via constructor and populate `MenuItem.Title` in `GetMenuItems()`. Report providers (`StageFright.Reports`, T039) take the **`ILocalizer` facade** via constructor rather than `IStringLocalizer<ReportsResource>` — several report titles/subtitles use named-placeholder formatting (`{DateFrom} – {DateTo}`, `Status: {Status} — {Date}`) which only `ILocalizer.Get<ReportsResource>(key, args)` substitutes — and resolve any enum-valued cell through `ILocalizer.Enum(value)` (e.g. `m.Status` in the Member List report, `reconciliation.Status` in the Bank Reconciliation header). Report filter definitions keep the invariant token as the option **value** (`ReportFilterDefinition.Options`, unchanged — still the token compared in `GenerateAsync` and persisted) and carry the localised option **label** in the new parallel `ReportFilterDefinition.OptionLabels` list, populated from `EnumsResource` (`Enum_MemberStatus_Active`) or, for a synthetic option, `ReportsResource` (`Reports_Filter_OptionAll`); `ReportViewer` renders `OptionLabels[i]` when present and falls back to `Options[i]` otherwise. Report renderers (`*PdfRenderer`, `CsvReportExporter`) are localised separately in T040.

---

## 7. Test contract

- `bUnit` `TestContext` fixtures call `Services.AddLocalization()` and register the real `.resx`-backed localizers (or a fake `IStringLocalizer<T>` that echoes keys). Component assertions reference **keys or the localizer**, never hardcoded English (FR-018).
- `StageFright.Localization.Tests` exposes and enforces the **resource-completeness guard**, the **enum-coverage** / **no-raw-enum-display** guards, the **residual-literal scan** (which also covers `aria-label` / `alt` / `title` attribute values — FR-001), and the **no-`"C"`-currency-format guard** (FR-015) — see `resource-key-catalog.md` §3.
- Integration: `ILanguageProvider.ResolveStartupCultureAsync` honours a persisted `LanguageCode` (SC-005); a switch leaves DB + GL byte-identical (SC-006); under a non-`en` culture a monetary amount renders with the `"$"` symbol and that culture's separators/grouping (FR-015); the runtime catalog lists only real shipped cultures, never `qps-ploc` (FR-011).

---

## 8. Currency & culture-sensitive number formatting (`MoneyFormatter` — FR-015, resolved 2026-08-27)

`src/StageFright.Core/Localization/MoneyFormatter.cs`

```csharp
namespace StageFright.Core.Localization;

/// <summary>
/// Formats monetary amounts for display. The amount is ALWAYS Australian dollars:
/// the currency symbol/code is fixed ("$" or an explicit "AUD ") regardless of the
/// active culture (FR-015 / FR-016). Only the decimal separator, digit grouping and
/// symbol placement follow CultureInfo.CurrentCulture. Never use decimal.ToString("C")
/// / "{0:C}" at a display site — that substitutes the culture's own currency symbol.
/// </summary>
public static class MoneyFormatter   // or ICurrencyFormatter if a seam is wanted for tests
{
    static string Format(decimal amount);              // "$1,234.50" (en-AU) · "$1 234,50" (fr-FR)
    static string FormatWithCode(decimal amount);      // "AUD 1,234.50" — for reports/exports where disambiguation matters
}
```

- Built by cloning `CultureInfo.CurrentCulture.NumberFormat` and overriding `CurrencySymbol` to `"$"` (and leaving `CurrencyDecimalDigits` at 2); `CurrencyGroupSeparator` / `CurrencyDecimalSeparator` / `CurrencyPositivePattern` etc. stay the culture's.
- Consumers: every `.razor` / `.razor.cs` / report provider / PDF-CSV renderer that today calls `decimal.ToString("C")`, `string.Format("{0:C}", …)`, or sets `RadzenDataGridColumn FormatString="{0:C}"` (~34 files) switches to `MoneyFormatter` (Radzen grids use a `Template` or a `FormatString` of `"{0:N2}"` wrapped by the formatter).
- Dates and plain (non-money) numbers are **not** routed through this — they use `CultureInfo.CurrentCulture` directly via the existing Blazor `InputDate` / `InputNumber` binding.
- The **no-`"C"`-currency-format guard** (`resource-key-catalog.md` §3) fails the build if a converted file still formats a display amount with `"C"` / `{0:C}`.
