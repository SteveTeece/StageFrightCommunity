# Contract: Resource Key Catalog & Guard

**Feature**: `027-localization-support` | **Date**: 2026-08-27

Defines the rules every resource key obeys and the automated guard that enforces coverage. This is a *contract* because the guard test, translators, and every converted call site all depend on it.

---

## 1. Key-naming scheme

`Area_Context_Meaning` — PascalCase segments joined by `_`.

| Segment | Rule | Examples |
|---|---|---|
| `Area` | The owning resource marker, minus the `Resource` suffix, or a short module token. One of: `Shared`, `Nav`, `Validation`, `Reports`, `Dashboard`, `Members`, `Rehearsals`, `Events`, `Finance`, `Settings`, `Setup`. | `Nav_`, `Members_`, `Reports_` |
| `Context` | The screen, section, form, dialog, tile, report, or entity the text belongs to. | `List`, `Form`, `Detail`, `Tile`, `MemberList`, `Wizard` |
| `Meaning` | What the text *is*, never the text itself. Suffix by role: `…Label`, `…Title`, `…Heading`, `…Placeholder`, `…HelpText`, `…Button`, `…Error`, `…Success`, `…Column<Name>`, `…Required`, `…AriaLabel`, `…Alt`, `…Tooltip`. | `FirstNameLabel`, `SaveButton`, `JoinDateColumn`, `OrganisationNameRequired`, `AddButtonAriaLabel`, `LogoAlt` |

Rules:

1. **Wording-independent** (FR-002). Renaming the copy from "Members" to "People" must not change `Nav_Members`.
2. **Named placeholders only** (FR-010). Use `{OrganisationName}`, `{Count}`, `{MemberName}` — never bare `{0}`. Each placeholder is listed in the `.resx` entry's `<comment>` with a one-line description.
3. **Plurals**: a count-dependent string is two keys, `…_One` and `…_Other`, resolved by `ILocalizer.Plural<T>(key, count)`. Example: `Settings_General_AuditRetentionYears_One` = `"{Count} year"`, `_Other` = `"{Count} years"`.
4. **No composition from fragments**. Do not build a sentence by concatenating `Shared_Word_Member` + `Shared_Word_List`; author the whole sentence as one key so a translator controls word order.
5. **Shared vs area**: text that is identical and genuinely generic (Save, Cancel, Close, Yes, No, "Loading…", "Actions", "Status") lives once in `Shared_*`. Anything module-specific stays in its area even if today it happens to read the same.
6. **Casing/spelling**: baseline values keep the current Australian English exactly (FR-004) — "Organisation", "Colour", "Financial Year", "AGM".
7. **Enum values** (FR-024): every user-facing enum member is one key `Enum_<EnumTypeName>_<MemberName>` (e.g. `Enum_MemberStatus_Active`, `Enum_TaxCode_Standard`) in the shared `EnumsResource`, resolved by the `LocalizeEnum` helper — never `enum.ToString()` at a display site. The enum name/number stays the culture-invariant identity used for storage, sorting, comparison and report-filter **option values**; only the **option label** is localized. Synthetic filter options that are not enum members (e.g. "All") get an ordinary `Reports_Filter_*` key.
8. **Accessibility text** (FR-001, resolved 2026-08-27): app-authored `aria-label`, image `alt`, and `title`/tooltip attribute values are localized keys like any visible label, with role suffix `…AriaLabel` / `…Alt` / `…Tooltip`. Genuinely decorative or empty attributes — `alt=""`, content inside `aria-hidden`, an `aria-label` that only duplicates adjacent visible (already-localized) text — carry no key and are exempt from the guard.
9. **Currency / number formatting** (FR-015, resolved 2026-08-27): monetary amounts are **not** resource strings — they are produced by the shared `MoneyFormatter` (`localization-contracts.md` §8), which keeps the `"$"` / `"AUD"` symbol fixed while separators, grouping and placement follow `CultureInfo.CurrentCulture`. Never format a display amount with `.ToString("C")` / `"{0:C}"` / `FormatString="{0:C}"` — that substitutes the active culture's currency symbol.

---

## 2. Physical layout

```
StageFright.Core/Modules/Localization/Resources/
    NavigationResource.cs            NavigationResource.resx            NavigationResource.qps-ploc.resx
    ValidationResource.cs            ValidationResource.resx            ValidationResource.qps-ploc.resx
    EnumsResource.cs                 EnumsResource.resx                 EnumsResource.qps-ploc.resx    (shared enum display labels, FR-024)
StageFright.Reports/Resources/
    ReportsResource.cs               ReportsResource.resx               ReportsResource.qps-ploc.resx
StageFright.UI/Resources/Strings/
    SharedResource.cs                SharedResource.resx                SharedResource.qps-ploc.resx
    DashboardResource.cs             DashboardResource.resx             …
    MembersResource.cs               MembersResource.resx              (US1)
    RehearsalsResource.cs / EventsResource.cs / FinanceResource.cs / SettingsResource.cs / SetupResource.cs   (US2)
```

- The **neutral** `.resx` (no culture suffix) **is** the `en-AU` baseline (FR-003). Each project sets `<NeutralLanguage>en-AU</NeutralLanguage>`.
- `*.qps-ploc.resx` is the pseudo-locale (Decision 9). It is **`Content` / test-fixture only** — not embedded as a shipped satellite, and excluded from the runtime-discovered `ISupportedLanguagesCatalog` by its `qps-*` culture name (FR-011). It deliberately omits ~3 keys to exercise fallback.
- A real future language adds `<Marker>.<culture>.resx` beside the neutral file — nothing else. The runtime catalog discovery (see `data-model.md` §2 / `localization-contracts.md` §3) picks it up and lists it by its `CultureInfo.NativeName`; there is **no** `SupportedLanguage` list to edit (SC-003, FR-011 resolved 2026-08-27).

---

## 3. Guard contract — `StageFright.Localization.Tests` (NEW project)

Test names follow `Should_[ExpectedBehavior]_When_[Condition]`. All are ordinary `dotnet test` tests; failures block merge (constitution §11.0).

| Guard | Behaviour |
|---|---|
| **Baseline completeness** (`Should_HaveNeutralEntry_When_KeyReferencedInCode`) | Scan `src/**/*.razor`, `*.razor.cs`, provider/renderer/exception `*.cs` for localizer key usages (`["…"]` on an `IStringLocalizer`, `Get<T>("…")`, `Plural<T>("…")`). For every referenced key, assert the matching neutral `.resx` (by area) contains a `<data name>` entry. Missing ⇒ fail, listing key + file (SC-008 / FR-003). |
| **No orphan satellite keys** (`Should_ContainOnlyKnownKeys_When_SatelliteResxInspected`) | Every key in any `*.<culture>.resx` (incl. `qps-ploc`) must exist in the neutral set for that area. |
| **Placeholder parity** (`Should_MatchNeutralPlaceholders_When_SatelliteEntryHasArguments`) | For each key present in both neutral and a satellite, the set of `{Named}` tokens must match. |
| **Plural pairing** (`Should_HaveBothOneAndOther_When_KeyUsedWithPlural`) | Any key passed to `Plural<T>` must have both `…_One` and `…_Other` in the neutral set. |
| **Enum coverage** (`Should_HaveEnumKey_When_MemberOfUserFacingEnum`) | For every member of each user-facing enum (`MemberStatus`, `FeeType`, `PaymentMethod`, `PaymentType`, `AccountType`, `TaxCode`, `ReconciliationStatus`, `JournalEntryType`, `Theme` — an explicit allow-list in the test), assert `EnumsResource` neutral has `Enum_<Type>_<Member>`. A new enum member with no key ⇒ fail (FR-024 / SC-008). |
| **No raw enum display** (`Should_NotCallToString_When_EnumRenderedToUser`) | In converted files, `.ToString()` / interpolation on an enum-typed expression inside markup, a `ReportRow`/`ReportColumn`/`Cells` value, or a `Text`/`Label`/`Heading` assignment ⇒ fail; the site must use `LocalizeEnum` (FR-024). `<select>` option **values** may still be the invariant token. |
| **Residual-literal scan** (`Should_HaveNoUserFacingLiteral_When_AppSurfaceScanned`) | Assert no user-facing string literal remains — element text nodes, `aria-label` / `alt` / `title` attribute values (FR-001), and `Text`/`Title`/`Label`/`Placeholder`/`Header`/`_errorMessage`/`_successMessage` assignments — plus every user-facing exception `Message` literal in `StageFright.Core`. Allow-list: log-message strings, routes (`"/members"`), CSS classes, format tokens (`"N2"`, `"dd/MM/yyyy"`), enum/filter value **tokens** used as `<option value>` / persisted keys (`"Active"`, `"Active Only"`, `"This FY"`), `data-*` attributes, keys themselves, `nameof(…)` arguments, genuinely decorative `alt=""` / `aria-hidden` subtrees, and DataAnnotations `ErrorMessage` / `IValidatableObject` `ValidationResult` message literals (compile-time constants — a runtime resource lookup with missing-key logging is not possible there; **permanent documented carve-out** per T060, see `docs/localization/adding-a-language.md` §7). Scoped per phase during US1/US2 delivery; **T060 made it repo-wide** — all of `StageFright.UI` (SC-001). |
| **No culture currency symbol** (`Should_NotUseCFormat_When_AmountRenderedToUser`) | In converted files, a money value formatted with `"C"`, `"C2"`, `{0:C}`, `string.Format(..., "{0:C}")`, or `FormatString="{0:C}"` at a display site ⇒ fail; use `MoneyFormatter` so the symbol stays `"$"` / `"AUD"` while separators follow the culture (FR-015). |
| **Missing-key logging** (`Should_LogWarningAndFallBack_When_KeyMissingForActiveCulture`) | With `qps-ploc` active and a deliberately-omitted key, `ILocalizer` returns the neutral value and a `Warning` is logged (FR-008 / FR-009 / SC-004). |

The scan helpers (regex/Roslyn) live in the test project only — no production dependency.

**Implemented layout.** The guards ship as `Us1LocalizationGuardTests` (US1 slice — baseline completeness, `MemberStatus`/`Theme` enum coverage, residual-literal, no-raw-enum, no-`"C"`, missing-key logging), the interim per-slice `RehearsalsLocalizationGuardTests` and `Us2ExceptionMessageGuardTests` (US2 T032 / T041 slices), and the repo-wide `Us2LocalizationGuardTests` (T029 — baseline completeness, all-nine-enum coverage + no-raw-enum, residual-literal, orphan-satellite, plural-pair + placeholder parity, cross-culture token parity, no-`"C"` repo-wide). **T060 removed the per-phase scoping**: `Us2LocalizationGuardTests.Should_HaveNoUserFacingLiteral_When_AppSurfaceScanned` now scans the whole `StageFright.UI` project plus every user-facing exception `Message` literal in `StageFright.Core` (folding in `Us2ExceptionMessageGuardTests`' file list), so a literal reintroduced in any converted file fails the run (SC-001). DataAnnotations `ErrorMessage` attribute arguments are the one deliberate, permanent exemption.

---

## 4. Extraction slice lists (bind the residual-literal scan)

**US1 slice** (pattern-fixing): `src/StageFright.UI/Layout/ShellLayout.razor(.cs)`, `ThemeProvider.razor(.cs)`, `src/StageFright.UI/Pages/Members/**`, `src/StageFright.UI/Modules/Members/**`, `src/StageFright.Core/Modules/Members/MemberMenuItemProvider.cs`, the Members-related entries in `ValidationResource`, `MemberValidationService` user-facing messages, and — establishing the enum pattern (FR-024) — `EnumsResource` + `LocalizeEnum` with `MemberStatus` (and `Theme`, already shown in the shell) as the first enums converted. Also in the US1 slice: the `aria-label` / `alt` / `title` attribute text in those Members + shell files (FR-001), and `MoneyFormatter` (FR-015) applied to any amount rendered in the Members screens (e.g. `MemberDetail` balances) as the first place the fixed-AUD pattern is proven.

**US2 slices** (repeat the pattern, one module per task group): Dashboard (`Pages/Dashboard`, `Modules/*/*Tile`), Rehearsals, Events/AGM, Finance, Settings, Setup Wizard, `Shared/*`, remaining `*MenuItemProvider` + report providers/renderers + `Core/Exceptions` user-facing messages + `StartupError.razor` + `MainPage.xaml`/`App.xaml` shell strings, and the remaining user-facing enums into `EnumsResource` (`FeeType`, `PaymentMethod`, `PaymentType`, `AccountType`, `TaxCode`, `ReconciliationStatus`, `JournalEntryType`) plus report-filter option labels. Each module's `aria-label` / `alt` / `title` text is converted in the same task group (FR-001), and every remaining `decimal.ToString("C")` / `{0:C}` / `FormatString="{0:C}"` display site (~34 files, concentrated in Finance screens + report providers/renderers) moves to `MoneyFormatter` (FR-015).

**US3**: `Settings.LanguageCode` + migration, `GeneralSettingsTab` + Setup Wizard language step (restart notice — in-session switch is out of scope for v1, FR-021), `ILanguageProvider` resolution ladder (explicit → OS language → `en-AU`, FR-023) + `ISystemCultureProvider` / `SystemCultureProvider`, `MauiProgram` culture wiring, `SupportedLanguagesCatalog` (runtime resource-culture discovery, endonym from `CultureInfo.NativeName`, `qps-*` excluded — FR-011), `qps-ploc` fixtures, `/docs` "adding a language" guide, and refresh of any `specs/**` doc made stale (FR-022).
