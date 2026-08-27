# Implementation Plan: Localization Support (Language Resource Files)

**Branch**: `027-localization-support` | **Date**: 2026-08-27 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/027-localization-support/spec.md`

**Size**: `oversized` (recorded in `.spec-context.json`) — full plan and all design artifacts, no trimming.

## Summary

Introduce first-class localization to the app by moving every piece of app-authored user-facing text out of `.razor` markup, code-behind, menu/tile providers, report providers/renderers, user-facing exception/validation messages, and the display text of user-facing enum values into per-language resource files, and having each surface read its text through `IStringLocalizer` at render time. Australian English (`en-AU`) is the extracted baseline and the fallback for any missing key. A new nullable `Settings.LanguageCode` column (mirroring how `Settings.Theme` is stored) records an *explicit* display-language choice; at startup the composition root resolves the culture — explicit choice → operating-system display language (when the app ships a matching resource set) → `en-AU` (FR-023) — and sets the process culture before the Blazor host renders, so screens, reports, and date/number formatting all follow it. Monetary amounts are an exception: they stay in Australian dollars — the `"$"` / `"AUD"` symbol/code is fixed regardless of culture, and only the number's separators, grouping and symbol placement follow the region (FR-015, resolved 2026-08-27), which means the ~34 files using raw `.ToString("C")` / `{0:C}` move to a shared fixed-AUD formatter. A language picker is added to the Settings General tab and the Setup Wizard, pre-selecting that resolved default; a change applies on next launch with a restart notice — in-session live switching is **out of scope for v1** (resolved 2026-08-27), though the design leaves a `LanguageProvider` cascading seam so it can be added later without rework.

The technical approach uses `Microsoft.Extensions.Localization` (first-party .NET, added through the central `Directory.Packages.props`) with a **small set of area-scoped resource marker classes** (e.g. `SharedResource`, `NavigationResource`, `MembersResource`, `ValidationResource`, `ReportsResource`) rather than one `.resx` per component, keeping the file count manageable across ~130 components. A thin `ILocalizer`/factory decorator records every missing-key fallback (FR-008/FR-009). Extraction is staged: US1 establishes the pattern end-to-end on the navigation shell plus the Members module; US2 rolls the identical pattern across all remaining surfaces; US3 adds the OS-display-language default (FR-023) and the persisted, user-selectable override, plus a pseudo-locale used only by tests to prove switching and fallback. App-authored accessibility strings (`aria-label`, `alt`, `title`/tooltip — ~119 occurrences across 39 `.razor` files) are extracted alongside visible text (FR-001, resolved 2026-08-27); decorative/empty `alt=""` / `aria-hidden` are exempt. The user-selectable language list is **discovered at runtime** from the resource cultures actually shipped (endonym from `CultureInfo.NativeName`, `qps-*` pseudo-locales filtered out) rather than a hand-maintained catalog (FR-011, resolved 2026-08-27).

## Project Structure

### Documentation (this feature)

```
specs/027-localization-support/
├── spec.md
├── plan.md                # this file
├── research.md            # Phase 0 — decisions & rationale
├── data-model.md          # Phase 1 — Settings.LanguageCode, supported-language catalog, resource-set model
├── contracts/
│   ├── localization-contracts.md      # ILocalizer facade, resource marker classes, ILanguageProvider, ISupportedLanguagesCatalog, Settings field
│   └── resource-key-catalog.md        # key-naming scheme + resource-completeness guard contract
└── checklists/
    └── requirements.md
```

### Source code (repository root) — directories/files this feature touches

```
src/
├── StageFright.Core/
│   ├── Entities/Settings.cs                         # + LanguageCode (string?, null ⇒ resolve: OS language → en-AU)
│   ├── Contracts/
│   │   ├── ILanguageProvider.cs                     # NEW — resolves culture: explicit choice → OS language → en-AU (FR-023)
│   │   ├── ISupportedLanguagesCatalog.cs            # NEW — languages discovered at runtime from shipped resource cultures (no hand-maintained list)
│   │   └── ISystemCultureProvider.cs               # NEW — reads the OS/device display language (impl in StageFright.App)
│   ├── Modules/Localization/                        # NEW module slice
│   │   ├── SupportedLanguage.cs                     # NEW — one shipped language (culture id + endonym from CultureInfo.NativeName)
│   │   ├── SupportedLanguagesCatalog.cs             # NEW — enumerates shipped .resx cultures at runtime; excludes qps-* pseudo-locales (FR-011)
│   │   ├── LanguageProvider.cs                      # NEW
│   │   └── Resources/
│   │       ├── ValidationResource.cs / .resx / .<culture>.resx
│   │       ├── NavigationResource.cs / .resx / .<culture>.resx
│   │       └── EnumsResource.cs / .resx / .<culture>.resx        # shared enum display labels (FR-024)
│   ├── Modules/*/           *MenuItemProvider.cs    # read Title/ShortLabel via IStringLocalizer<NavigationResource>
│   ├── Exceptions/*.cs                              # user-facing Message text sourced from ValidationResource (types/boundary rules unchanged)
│   └── Localization/
│       ├── ILocalizer.cs                            # NEW — thin facade over IStringLocalizerFactory
│       ├── MissingKeyLoggingLocalizerFactory.cs     # NEW — decorator logging resourceNotFound (FR-009)
│       ├── EnumLocalizationExtensions.cs            # NEW — LocalizeEnum(this Enum) → EnumsResource (FR-024)
│       └── MoneyFormatter.cs                        # NEW — fixed-AUD symbol + culture-driven separators/grouping (FR-015); replaces raw "C" / {0:C}
├── StageFright.Data/
│   └── Migrations/ 2026XXXXXXXXXX_AddLanguageCodeToSettings.cs (+ .Designer.cs, snapshot update)
├── StageFright.Reports/
│   ├── Resources/ReportsResource.cs / .resx / .<culture>.resx   # NEW
│   ├── Providers/*ReportProvider.cs                 # ReportName / Filters[].Label / fixed section labels via IStringLocalizer<ReportsResource>
│   └── Rendering/*PdfRenderer.cs, CsvReportExporter.cs           # headers / totals labels / fixed annotations via localizer
├── StageFright.UI/
│   ├── _Imports.razor                               # + @using Microsoft.Extensions.Localization
│   ├── Resources/Strings/                           # NEW — SharedResource, MembersResource, FinanceResource, EventsResource, RehearsalsResource, DashboardResource, SettingsResource, SetupResource (each .cs + .resx + .<culture>.resx + .qps-ploc.resx)
│   ├── Layout/{ShellLayout,ThemeProvider}.razor(.cs)            # nav labels + (optional) LanguageProvider cascade seam
│   ├── Pages/**/*.razor + *.razor.cs                # ~65 + ~64 files — text via @L["Key"] / injected localizer, incl. aria-label / alt / title attribute text (FR-001); money via MoneyFormatter not {0:C}
│   ├── Modules/**/*Tile.razor(.cs)                  # dashboard tile captions
│   ├── Shared/*.razor(.cs)                          # shared components
│   └── Pages/Settings/GeneralSettingsTab.razor(.cs) + Pages/Setup/Tabs/*  # language picker + restart notice
└── StageFright.App/
    ├── MauiProgram.cs                               # AddLocalization(); ILanguageProvider resolves explicit choice → OS language → en-AU at startup; set CultureInfo.DefaultThreadCurrentCulture/UICulture
    └── SystemCultureProvider.cs                      # NEW — ISystemCultureProvider over the device/OS UI culture

tests/
├── StageFright.Core.Tests/Localization/             # LanguageProvider resolution ladder (explicit / OS language / en-AU fallback — FR-023, SC-010), runtime catalog discovery + qps-* exclusion + endonym-from-NativeName (FR-011), missing-key logging, exception-message localization, LocalizeEnum for each user-facing enum (FR-024), MoneyFormatter fixed-AUD symbol + culture separators (FR-015)
├── StageFright.Data.Tests/                          # AddLanguageCodeToSettings migration round-trip
├── StageFright.UI.Tests/                            # bUnit TestContext gains AddLocalization(); assertions via keys/localizer (FR-018)
├── StageFright.Reports.Tests/                       # report labels resolve via ReportsResource
├── StageFright.Integration.Tests/                   # startup honours persisted LanguageCode (SC-005) and OS language when none stored (SC-010); language switch leaves DB/GL untouched (SC-006)
└── StageFright.Localization.Tests/  (NEW)           # resource-completeness guard (SC-008) + enum-coverage / no-raw-enum-display guards (FR-024) + residual-literal scan incl. aria-label/alt/title (FR-001, SC-001) + no-"C"-currency-format guard (FR-015), per phase
```

**Structure Decision**: Keep the established layered/module-slice architecture unchanged. Localization infrastructure is added as a new `Modules/Localization` slice in `StageFright.Core` (contracts, runtime-discovered catalog, provider) plus the existing `StageFright.Core/Localization/` folder for the facade, missing-key decorator, enum helper, and the new `MoneyFormatter` (fixed-AUD display, FR-015), plus a per-project `Resources/` folder in each assembly that owns user-facing text (`StageFright.Core`, `StageFright.Reports`, `StageFright.UI`); the DAL stays centralized (only a new `Settings` column + migration). Resource files are area-scoped marker classes, not per-component, to keep the count near a dozen per culture. App-authored `aria-label` / `alt` / `title` text is extracted like visible text (FR-001).

## Constitution Check

*Constitution `.specify/memory/constitution.md` v2.6.0. Gate before Phase 0; re-checked after Phase 1.*

| Principle | Assessment |
|---|---|
| §3.1 Clean Code / simple over clever | **PASS** — one shared mechanism (`IStringLocalizer` + area resx), applied uniformly; no bespoke string framework. |
| §3.2.1 One class per file (NON-NEGOTIABLE) | **PASS** — each resource marker class, contract, catalog, provider, and decorator is its own file; `.Designer.cs` is tool-generated and exempt by the same rule that exempts EF migration designers. |
| §3.3 Separation of Concerns | **PASS** — markup calls `@L["Key"]`; resolution/fallback/logging live in the Core localization facade; resource data lives in resx. |
| §3.4–3.6 Soft-delete / financial immutability / corrections | **PASS** — adds one nullable presentation-only `Settings` column; no financial entity, GL posting, or delete semantics touched. FR-016 forbids any stored-value change on language switch and is covered by an integration test. FR-015 (resolved 2026-08-27) keeps the displayed currency symbol/code fixed at AUD via `MoneyFormatter`, so a region change never restates an amount in another currency — only number separators/placement localise. |
| §4.1 Layered architecture with module slices | **PASS** — new `Modules/Localization` slice in Core; UI/Reports keep their roles; repositories stay central (only `Settings` migration added). |
| §4.3 Settings System | **PASS** — display language is an **Application Setting** on the built-in General tab (Core-owned), not an `ISettingsTabProvider` plugin tab. |
| §4.6 Navigation Menu System | **PASS** — `MenuItem` contract unchanged (no new field); `Title` is still produced when the shell calls `GetMenuItems()` at render time, now from `IStringLocalizer<NavigationResource>`. |
| §4.7 Blazor Component Patterns (MANDATORY) | **PASS** — localizer is injected in the `.razor.cs` code-behind; `.razor` keeps markup only (`@L["…"]` expressions); no `@code` blocks, no inline `<style>`. |
| §5 Error Handling & Custom Exceptions | **PASS** — only the *user-facing message text* of exceptions/validation moves to resources; exception types, `CorrelationId`/timestamp properties, and boundary-translation rules (§5.3) are untouched; raw framework exceptions are still wrapped. |
| §6 Logging & Observability | **PASS** — log/diagnostic text stays English (spec Assumptions); missing-key fallbacks are logged through the existing Serilog pipeline; no new OTel surface. |
| §7.1 Technology Stack | **PASS** — `Microsoft.Extensions.Localization` is first-party .NET, version-pinned in `Directory.Packages.props`; no Moq, no new JS, no new UI toolkit. |
| §7.3 Prohibited / §9.2 Prohibited in specs | **PASS** — no custom JavaScript (localization is pure C#); spec carries no code/implementation leakage beyond the pinned baseline culture. |
| §10 Planning & Implementation Rules | **PASS** — this plan + Phase 0/1 artifacts precede implementation; work is staged by user story. |
| §11 Testing Standards (§11.0 Non-Negotiable Coverage) | **PASS** — every new/changed path gets tests: provider/catalog/decorator unit tests, migration round-trip, bUnit assertions via keys, report-label tests, startup + no-stored-value integration tests, and a resource-completeness guard that fails the run on a missing baseline key (FR-018 / SC-008). |

No violations → no Complexity Tracking table.

## Phase 0 — Research

See [research.md](./research.md). Resolves: localization mechanism (`IStringLocalizer` + area-scoped resx vs strongly-typed designer vs JSON), resource granularity, key-naming scheme, enum display-text strategy (`Enum_<Type>_<Member>` in a shared `EnumsResource` + a `LocalizeEnum` helper — FR-024), missing-key detection strategy, the startup culture-resolution ladder in MAUI Blazor Hybrid (explicit `Settings.LanguageCode` → OS display language when a matching resource set ships → `en-AU` — FR-023, via an `ISystemCultureProvider` seam), the FR-021 decision (next launch + restart notice; in-session switch out of scope for v1), fixed-AUD currency display via `MoneyFormatter` (FR-015 — Decision 11), runtime discovery of the shipped-language list with endonyms from `CultureInfo.NativeName` and `qps-*` exclusion (FR-011 — Decision 5), accessibility-attribute text in scope (FR-001), culture-invariant handling of GL/stored values, plugin-text boundary, and the pseudo-locale used for test-only proof of switching.

## Phase 1 — Design & Contracts

- [data-model.md](./data-model.md) — `Settings.LanguageCode` field + migration, `SupportedLanguage` catalog entry, and the conceptual `LanguageResourceSet` / `ResourceKey` model the guard test enforces.
- [contracts/localization-contracts.md](./contracts/localization-contracts.md) — `ILocalizer` facade (incl. `Enum` / the `LocalizeEnum` helper, FR-024), the area resource marker classes (incl. shared `EnumsResource`), `ILanguageProvider` (explicit → OS language → en-AU ladder), `ISupportedLanguagesCatalog`, `ISystemCultureProvider`, and the `Settings` API-surface change.
- [contracts/resource-key-catalog.md](./contracts/resource-key-catalog.md) — the `Area_Context_Meaning` key-naming scheme (incl. `…AriaLabel` / `…Alt` / `…Tooltip` roles, FR-001) and the resource-completeness / residual-literal / no-`"C"`-currency guard contract.
- [quickstart.md](./quickstart.md) — runnable validation scenarios: restore/build, run the completeness + residual-literal + guard suite, `en-AU` render parity, pseudo-locale switch + per-key fallback logging, monetary amount shows `$` with culture separators under a non-`en` culture (FR-015), DB/GL byte-identical after a language switch (SC-006), OS-language default when no explicit choice (SC-010).

Post-design constitution re-check (incl. the 2026-08-27 clarifications: FR-021 next-launch-only, FR-015 fixed-AUD display, FR-011 runtime catalog discovery, FR-001 accessibility text): unchanged — all **PASS**, no violations. `MoneyFormatter` is one shared helper (§3.1), its own file (§3.2.1), and keeps financial display culture-invariant in amount while localising only format (§3.4–3.6).
