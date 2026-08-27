# Phase 1 Data Model: Localization Support

**Feature**: `027-localization-support` | **Date**: 2026-08-27

This feature adds **one persisted field** and a handful of build-time / in-memory model types. Resource sets themselves are compile-time assets (`.resx` → satellite assemblies), not database rows.

---

## 1. `Settings.LanguageCode` (persisted — new column)

Added to the existing singleton entity `src/StageFright.Core/Entities/Settings.cs`, next to `Theme`.

| Attribute | Value |
|---|---|
| Property | `public string? LanguageCode { get; set; }` |
| Meaning | The user's chosen display language as a BCP-47 culture identifier, e.g. `en-AU`, `en-US`. |
| Nullability | Nullable. `null` = never chosen ⇒ the app uses the default culture (`en-AU`). Existing rows and new installs are `null` until the user picks a language (FR-017). |
| Storage | SQLite `TEXT NULL` on the `Settings` table. No `MaxLength` required; a defensive `HasMaxLength(16)` may be added in the EF configuration. |
| Default | No SQL default. When `null`, `LanguageProvider` resolves the effective culture at startup — explicit choice → operating-system display language (when a matching catalog language ships) → `en-AU` (see §4). The database never holds the resolved value. |
| Validation | On save, must be either `null` or a `CultureCode` present in `ISupportedLanguagesCatalog`. An unknown/blank value is coerced to `null` (treated as "use default") rather than rejected, so a downgraded install with an unrecognised code still starts. |
| Audit | `SettingsService.SaveAsync` already writes an audit entry for changed fields; `LanguageCode` is included automatically (constitution §4.3). |
| Immutability impact | Presentation-only. FR-016: changing it MUST NOT alter any other stored value or GL balance — covered by an integration test (SC-006). |
| Display impact | Applying a culture localises **date and number** formatting only. Monetary amounts are always Australian dollars — the `"$"` / `"AUD"` symbol/code is fixed regardless of culture; only the number's separators, grouping and symbol placement follow the region (FR-015, resolved 2026-08-27). Display sites use the shared `MoneyFormatter`, never `decimal.ToString("C")` / `{0:C}` (which would emit the culture's own currency symbol). |

### Migration

`dotnet ef migrations add AddLanguageCodeToSettings --project src/StageFright.Data/ --startup-project src/StageFright.App/`

- `Up`: `AddColumn<string>("LanguageCode", "Settings", nullable: true)`.
- `Down`: `DropColumn("LanguageCode", "Settings")`.
- Regenerates `StageFrightDbContextModelSnapshot.cs`.
- Follows the shape of `20260809083931_AddAuditRetentionYearsToSettings`.
- `Settings.SchemaVersion` bumped (patch) per the existing convention for a settings-shape change (NFR-002).

---

## 2. `SupportedLanguage` (in-memory value object — new)

`src/StageFright.Core/Modules/Localization/SupportedLanguage.cs` — one entry per language the app ships as a user-selectable option.

| Field | Type | Notes |
|---|---|---|
| `CultureCode` | `string` | BCP-47 id, e.g. `en-AU`. Matches a shipped neutral or satellite `.resx` set discovered at runtime. |
| `Endonym` | `string` | The language's name in its own language, shown in the picker (FR-012), e.g. "English (Australia)". **Derived** from `CultureInfo.GetCultureInfo(CultureCode).NativeName` (title-cased) — not authored or stored. |
| `IsDefault` | `bool` | `true` for the neutral / baseline set (`en-AU`). Used to pre-select and to resolve `null`/unknown `LanguageCode`. |

Immutable (`record` or init-only). Equality by `CultureCode`.

### `ISupportedLanguagesCatalog`

Returns the ordered `IReadOnlyList<SupportedLanguage>`, **built at runtime** (FR-011, resolved 2026-08-27) — there is no hand-maintained list. `SupportedLanguagesCatalog` enumerates the resource cultures the app actually ships: the neutral set (`en-AU`, `IsDefault`) plus every satellite `<Marker>.<culture>.resx` culture present in the loaded resource assemblies, de-duplicated, ordered default-first then by endonym. Endonyms come from `CultureInfo.NativeName`. Cultures whose name matches the pseudo-locale pattern (`qps-*`, e.g. `qps-ploc`) are excluded so the test pseudo-locale never appears in the picker or in FR-023 matching. For v1 this yields exactly one entry (`en-AU`) because no satellite ships. Adding a real language = drop its `<Marker>.<culture>.resx` set in; it is discovered and listed automatically by its `NativeName`, with no code or list change (SC-003). The `qps-ploc` pseudo-locale (Decision 9) is a `.resx` on disk for test fixtures only and is filtered out by the `qps-*` rule.

---

## 3. `LanguageResourceSet` / `ResourceKey` (conceptual — enforced by the guard test, no runtime type)

These are the spec's Key Entities. They exist as `.resx` files + the completeness guard, not as C# entities.

| Concept | Realised as | Rules enforced |
|---|---|---|
| **Language Resource Set** | The group of `.resx` files for one culture across all resource-owning projects: the neutral set (= `en-AU` baseline) and any `*.<culture>.resx` satellites. | The neutral set MUST contain every key referenced anywhere in code/markup (FR-003, SC-008). A satellite set MAY be partial; missing keys fall back to neutral (FR-008). A satellite set MUST NOT contain a key absent from neutral (guard test fails). |
| **Resource Key** | A `<data name="...">` entry, named `Area_Context_Meaning` (see `contracts/resource-key-catalog.md`). | Unique within its area `.resx`. Stable across wording changes (FR-002). Named placeholders only (`{Name}`), documented in the entry's `<comment>` (FR-010). Plural pairs use `_One` / `_Other` suffixes. App-authored accessibility text (`aria-label`, image `alt`, `title`/tooltip) is keyed like any visible label with an `…AriaLabel` / `…Alt` / `…Tooltip` role suffix (FR-001, resolved 2026-08-27); decorative/empty `alt=""` and `aria-hidden` content have no key. |
| **Enum display label** | An `Enum_<EnumTypeName>_<MemberName>` entry in the shared `EnumsResource` (`StageFright.Core`), one per member of each user-facing enum (`MemberStatus`, `FeeType`, `PaymentMethod`, `PaymentType`, `AccountType`, `TaxCode`, `ReconciliationStatus`, `JournalEntryType`, `Theme`). | The enum's name/number is the culture-invariant identity used for storage, GL, backups, sorting, comparison and `<option value>` / report-filter tokens — never localized (FR-024, FR-016). Only the rendered label is. A new enum member without a key fails the enum-coverage guard. |
| **Language Preference** | `Settings.LanguageCode` (§1 above) + `LanguageProvider` resolution. | Singleton; read once at startup (Decision 5). Resolves explicit choice → OS display language (matched in the catalog by exact culture, then parent language) → `IsDefault` culture (`en-AU`). |

---

## 4. Resolution flow (no new persisted state)

```
MauiProgram.CreateMauiApp
  └─ (after Build, in startup scope)
       ILanguageProvider.ResolveStartupCultureAsync()
         ├─ settings = ISettingsService.GetAsync()
         ├─ explicit = ISupportedLanguagesCatalog.Find(settings?.LanguageCode)   // null unless a shipped language is named
         │    └─ if explicit != null →  culture = explicit.CultureCode           // (1) explicit choice always wins
         ├─ else osCulture = ISystemCultureProvider.GetUiCulture()               // OS / device display language
         │    system = ISupportedLanguagesCatalog.Find(osCulture)
         │           ?? ISupportedLanguagesCatalog.Find(parentLanguageOf(osCulture))
         │    └─ if system != null →  culture = system.CultureCode               // (2) OS language, resource set exists
         ├─ else culture = catalog.Default.CultureCode                           // (3) fallback → "en-AU"
         └─ CultureInfo.DefaultThreadCurrentCulture   = new CultureInfo(culture)
            CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo(culture)
            // any step whose code will not construct a CultureInfo drops to (3)
```

State transitions for `Settings.LanguageCode`: starts `null` — the app *follows the OS display language* (step 2), or `en-AU` when no matching set ships (step 3). *User picks a language in Settings/Setup* → `"<CultureCode>"` (persisted, restart notice shown) → *applied on next launch*, and from then on the explicit choice wins over the OS language (step 1). The user may set it back to the default entry, storing the explicit `en-AU` code; clearing it (returning to `null`) resumes "follow the OS language". A stored code for a language no longer shipped is treated as `null`.
