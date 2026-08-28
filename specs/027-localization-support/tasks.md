# Tasks: Localization Support (Language Resource Files)

**Branch**: `027-localization-support` | **Date**: 2026-08-27 | **Size**: `oversized` (full phased list)

**Input**: [spec.md](./spec.md), [plan.md](./plan.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/localization-contracts.md](./contracts/localization-contracts.md), [contracts/resource-key-catalog.md](./contracts/resource-key-catalog.md)

Line format: `- [ ] **T###** [P?] [US#] Description · exact/file/path`
`[P]` = independent of the other tasks in its wave (different file, no incomplete dependency). `[US#]` maps to a user story.

**Story order (priority)**: US1 (P1) → US2 (P2) → US3 (P3). US1 is the MVP slice: it settles the extraction pattern end-to-end on the navigation shell + the Members module. US2 rolls the identical pattern across every remaining surface. US3 adds the persisted, OS-aware language selection.

---

## Phase 1: Setup

Shared tooling prerequisites: the localization package, the new guard-test project, and the per-project resx wiring.

**Wave 1 — independent (different files):**

- [x] **T001** [P] Add `<PackageVersion Include="Microsoft.Extensions.Localization" Version="10.0.x" />` (align the patch with the existing `Microsoft.Extensions.*` 10.0.x entries) · `Directory.Packages.props`
- [x] **T002** [P] Scaffold the new guard-test project `StageFright.Localization.Tests` (xUnit; project refs → `StageFright.Core`, `StageFright.UI`, `StageFright.Reports`) and add it to the solution · `tests/StageFright.Localization.Tests/StageFright.Localization.Tests.csproj`, `StageFrightCommunity.slnx`

**⟶ Wait for Wave 1 to finish, then:**

- [x] **T003** Add the version-less `<PackageReference Include="Microsoft.Extensions.Localization" />` and `<NeutralLanguage>en-AU</NeutralLanguage>` to each resource-owning project · `src/StageFright.Core/StageFright.Core.csproj`, `src/StageFright.Reports/StageFright.Reports.csproj`, `src/StageFright.UI/StageFright.UI.csproj`

---

## Phase 2: Foundational (BLOCKS all user stories)

The one shared localization mechanism every story builds on: area resource marker classes, the missing-key logging decorator (FR-008/FR-009), the `LocalizeEnum` helper (FR-024), `MoneyFormatter` (FR-015), the DI wiring, and the test harness. **No user-story work starts until this phase is done.**

**Wave 1 — independent (different projects), create empty marker + neutral `.resx` per area:**

- [x] **T004** [P] Core area markers `NavigationResource`, `ValidationResource`, `EnumsResource` — one `.cs` file each + an empty neutral `.resx` each · `src/StageFright.Core/Modules/Localization/Resources/`
- [x] **T005** [P] Reports area marker `ReportsResource` — `.cs` + empty neutral `.resx` · `src/StageFright.Reports/Resources/ReportsResource.cs` (+ `.resx`)
- [x] **T006** [P] UI area markers `SharedResource`, `DashboardResource`, `MembersResource`, `RehearsalsResource`, `EventsResource`, `FinanceResource`, `SettingsResource`, `SetupResource` — one `.cs` file each + an empty neutral `.resx` each · `src/StageFright.UI/Resources/Strings/`

**⟶ Wait for Wave 1 (markers must exist for `IStringLocalizer<T>` to bind), then:**

**Wave 2 — independent (different files):**

- [x] **T007** [P] `ILocalizer` facade + `Localizer` implementation (`Get<T>`, `Get<T>(args)`, `Plural<T>`, `Enum`) · `src/StageFright.Core/Localization/ILocalizer.cs`, `src/StageFright.Core/Localization/Localizer.cs`
- [x] **T008** [P] `MissingKeyLoggingLocalizerFactory : IStringLocalizerFactory` — decorator over the default factory; on `LocalizedString.ResourceNotFound` logs a Serilog `Warning` and returns the neutral (en-AU) value (FR-008/FR-009) · `src/StageFright.Core/Localization/MissingKeyLoggingLocalizerFactory.cs`
- [x] **T009** [P] `EnumLocalizationExtensions.LocalizeEnum(this Enum)` → `EnumsResource["Enum_<Type>_<Member>"]` routed through the logging decorator (FR-024) · `src/StageFright.Core/Localization/EnumLocalizationExtensions.cs`
- [x] **T010** [P] `MoneyFormatter` — `Format` / `FormatWithCode`; clones `CultureInfo.CurrentCulture.NumberFormat`, forces `CurrencySymbol` to `"$"` / `"AUD "`, keeps culture separators/grouping/placement (FR-015) · `src/StageFright.Core/Localization/MoneyFormatter.cs`
- [x] **T011** [P] Add `@using Microsoft.Extensions.Localization` and `@using StageFright.Core.Localization` · `src/StageFright.UI/_Imports.razor`
- [x] **T012** [P] Guard-test scan helpers (Roslyn/regex): localizer-key-usage extraction, `.resx` `<data name>` parsing, user-facing-enum member enumeration — infrastructure only, no assertions yet · `tests/StageFright.Localization.Tests/Scanning/`
- [x] **T013** [P] bUnit test base: `Services.AddLocalization()` + register the real `.resx`-backed `IStringLocalizer<T>` (or a key-echo fake) so component tests assert via keys/localizer, not English (FR-018) · `tests/StageFright.UI.Tests/` (shared `TestContext` base)

**⟶ Wait for T007 + T008, then:**

- [x] **T014** DI wiring in the composition root: `services.AddLocalization()`, decorate `IStringLocalizerFactory` with `MissingKeyLoggingLocalizerFactory`, `services.AddScoped<ILocalizer, Localizer>()` · `src/StageFright.App/MauiProgram.cs` (`RegisterCoreServices`)

---

## Phase 3: User Story 1 — App text comes from a language resource file, not hardcoded (P1)

**Goal**: Move every user-facing literal in the navigation shell + the complete Members module into resources, read through `IStringLocalizer` at render time, with the extraction pattern (key scheme, code-behind injection, enum + money handling, guard + bUnit tests) proven and reviewable on this one real slice.

**Independent Test**: No user-facing literal remains in the shell/Members `.razor`, `.razor.cs` and provider files (incl. `aria-label`/`alt`/`title` and enum display text such as member status); every shell + Members screen shows the same Australian English wording as before; editing a value in `MembersResource.resx` changes the screen after a rebuild; a missing key falls back to en-AU and is recorded.

### Tests (write to fail first — FR-018, constitution §11.0)

- [x] **T015** [US1] US1 guard suite in `StageFright.Localization.Tests`: baseline-completeness (`Should_HaveNeutralEntry_When_KeyReferencedInCode`), residual-literal scan **scoped to the US1 slice file list** (incl. `aria-label`/`alt`/`title` — FR-001), enum-coverage for `MemberStatus` + `Theme` + no-raw-enum-display, no-`"C"`-currency scoped to US1 files (FR-015), missing-key logging/fallback (`Should_LogWarningAndFallBack_When_KeyMissingForActiveCulture`, FR-008/FR-009/SC-004) · `tests/StageFright.Localization.Tests/`
- [x] **T016** [US1] bUnit tests for `ShellLayout`, `ThemeProvider` and the Members pages/components asserting rendered text via `IStringLocalizer` keys, not hardcoded English (FR-018) · `tests/StageFright.UI.Tests/`

### Implementation

**Wave 1 — independent (different `.resx` files), author en-AU entries verbatim (FR-003/FR-004):**

- [x] **T017** [P] [US1] Shell chrome + `MemberMenuItemProvider` `Title`/`ShortLabel` keys · `src/StageFright.Core/Modules/Localization/Resources/NavigationResource.resx`
- [x] **T018** [P] [US1] Every Members screen/component string — labels, headings, buttons, placeholders, help text, `aria-label`/`alt`/`title` (FR-001), status/success/error · `src/StageFright.UI/Resources/Strings/MembersResource.resx`
- [x] **T019** [P] [US1] `MemberValidationService` + Members-related user-facing exception `Message` text · `src/StageFright.Core/Modules/Localization/Resources/ValidationResource.resx`
- [x] **T020** [P] [US1] `Enum_MemberStatus_*` and `Enum_Theme_*` entries verbatim (FR-024) · `src/StageFright.Core/Modules/Localization/Resources/EnumsResource.resx`
- [x] **T021** [P] [US1] Cross-cutting actions used by the shell + Members (Save/Cancel/Close/Yes/No/"Loading…"/"Actions"/"Status") · `src/StageFright.UI/Resources/Strings/SharedResource.resx`

**⟶ Wait for the resx entries to exist, then convert the call sites:**

**Wave 2 — independent (different files):**

- [x] **T022** [P] [US1] Inject `IStringLocalizer<NavigationResource>` (+ `SharedResource`) in the code-behind; replace all literals incl. `aria-label`/`title` (FR-001); theme labels via `Theme.LocalizeEnum()` · `src/StageFright.UI/Layout/ShellLayout.razor` + `ShellLayout.razor.cs`
- [x] **T023** [P] [US1] Replace theme/enum display text via `LocalizeEnum` / injected localizer · `src/StageFright.UI/Layout/ThemeProvider.razor` + `ThemeProvider.razor.cs`
- [x] **T024** [P] [US1] Constructor-inject `IStringLocalizer<NavigationResource>`; set `MenuItem.Title`/`ShortLabel` from resources in `GetMenuItems()` · `src/StageFright.Core/Modules/Members/MemberMenuItemProvider.cs`
- [x] **T025** [P] [US1] Source user-facing validation/error messages from `IStringLocalizer<ValidationResource>` (exception types + boundary rules unchanged) · `src/StageFright.Core/Modules/Members/` (`MemberValidationService` + Members exception message text)
- [x] **T026** [P] [US1] Inject `IStringLocalizer<MembersResource>` in each code-behind; `@L["…"]` in markup; `member.Status.LocalizeEnum()` for status; `MoneyFormatter.Format(...)` for balances (FR-015); localize `aria-label`/`alt`/`title` (FR-001) · `src/StageFright.UI/Pages/Members/*.razor` + `*.razor.cs`
- [x] **T027** [P] [US1] Inject localizer; convert captions/tile text · `src/StageFright.UI/Modules/Members/**`

**⟶ Wait for Wave 2, then:**

- [x] **T028** [US1] Confirm the Members provider/service registrations resolve `IStringLocalizer<T>` from DI; update any factory-lambda registration that now needs it · `src/StageFright.App/MauiProgram.cs` (`RegisterCoreServices`)

**Checkpoint US1**: The navigation shell and the Members module are fully localized — same en-AU wording, all from resources; the US1-scoped guard suite and Members/shell bUnit tests are green; the extraction pattern is settled and independently demoable. US2 can now repeat it mechanically.

---

## Phase 4: User Story 2 — Every remaining screen, report and system message is localized (P2)

**Goal**: Apply the US1 pattern to the whole app — Dashboard, Rehearsals, Events/AGM, Finance, Settings, Setup Wizard, shared components, dashboard tiles, all 11 PDF/CSV reports, the remaining menu providers, and the user-facing text of validation/exception messages — plus every remaining user-facing enum and every remaining `"C"`-formatted money display site. After this phase no user-facing hardcoded string remains in the app's own code.

**Independent Test**: A repository-wide residual-literal scan over the US1 + US2 file lists finds only non-user-facing matches (logs, routes, CSS, format tokens, `<option value>`/filter tokens, keys). Open every screen and generate every report: wording is unchanged from today; every enum renders via a lookup while its filter/sort/storage token stays the culture-invariant enum identity.

### Tests (write to fail first)

- [x] **T029** [US2] Extend the guard suite to the full app: residual-literal scan over the US2 file list, enum-coverage for `FeeType`/`PaymentMethod`/`PaymentType`/`AccountType`/`TaxCode`/`ReconciliationStatus`/`JournalEntryType` + no-raw-enum-display, no-orphan-satellite-keys, placeholder-parity, plural-pairing, no-`"C"`-currency repo-wide (FR-024/FR-010/FR-015, SC-001/SC-008) · `tests/StageFright.Localization.Tests/`
- [x] **T030** [US2] Report-label tests (`ReportsResource` resolves report names/headers/section+total labels — FR-006) and a plugin-under-non-`en` test asserting `StageFright.TestPlugin` English strings render without error or blanking (FR-020) · `tests/StageFright.Reports.Tests/`, `tests/StageFright.Integration.Tests/`

### Implementation

**Wave 1 — independent (different files/modules); each task authors its area `.resx` en-AU entries verbatim **and** converts that area's call sites (localizer inject in code-behind, `@L["…"]`, `aria-label`/`alt`/`title` per FR-001, `LocalizeEnum`, `MoneyFormatter`):**

- [x] **T031** [P] [US2] Dashboard page + tiles · `src/StageFright.UI/Resources/Strings/DashboardResource.resx`, `src/StageFright.UI/Pages/Dashboard/**`, `src/StageFright.UI/Modules/*/*Tile.razor(.cs)`
- [x] **T032** [P] [US2] Rehearsals module · `src/StageFright.UI/Resources/Strings/RehearsalsResource.resx`, `src/StageFright.UI/Pages/Rehearsals/**`
- [x] **T033** [P] [US2] Events / AGM module · `src/StageFright.UI/Resources/Strings/EventsResource.resx`, `src/StageFright.UI/Pages/Events/**` (+ AGM pages)
- [x] **T034** [P] [US2] Finance module (heavy `MoneyFormatter` migration + `LocalizeEnum` for fee/payment/account/tax/recon enums) · `src/StageFright.UI/Resources/Strings/FinanceResource.resx`, `src/StageFright.UI/Pages/Finance/**`
- [x] **T035** [P] [US2] Settings page + tabs (excluding the language picker — US3) · `src/StageFright.UI/Resources/Strings/SettingsResource.resx`, `src/StageFright.UI/Pages/Settings/**`
- [x] **T036** [P] [US2] Setup Wizard steps (excluding the language step — US3) · `src/StageFright.UI/Resources/Strings/SetupResource.resx`, `src/StageFright.UI/Pages/Setup/**`
- [x] **T037** [P] [US2] Shared components + remaining cross-cutting text · `src/StageFright.UI/Resources/Strings/SharedResource.resx`, `src/StageFright.UI/Shared/*.razor(.cs)`
- [x] **T038** [P] [US2] Remaining `*MenuItemProvider` `Title`/`ShortLabel` · `src/StageFright.Core/Modules/Localization/Resources/NavigationResource.resx`, `src/StageFright.Core/Modules/*/*MenuItemProvider.cs`
- [x] **T039** [P] [US2] Report providers — `ReportName`, filter labels, section labels; filter **option values** stay invariant tokens, **labels** via `EnumsResource` / `Reports_Filter_OptionAll` · `src/StageFright.Reports/Resources/ReportsResource.resx`, `src/StageFright.Reports/Providers/*ReportProvider.cs`
- [x] **T040** [P] [US2] Report renderers — column headers, subtotal/total labels, fixed annotations via `IStringLocalizer<ReportsResource>`; enum cells via `LocalizeEnum`; amounts via `MoneyFormatter.FormatWithCode` · `src/StageFright.Reports/Rendering/*PdfRenderer.cs`, `src/StageFright.Reports/Rendering/CsvReportExporter.cs`
- [x] **T041** [P] [US2] User-facing exception `Message` text + remaining validation services → `ValidationResource` (types / `CorrelationId` / boundary-wrapping rules unchanged) · `src/StageFright.Core/Modules/Localization/Resources/ValidationResource.resx`, `src/StageFright.Core/Exceptions/*.cs`, remaining `*ValidationService`
- [x] **T042** [P] [US2] Remaining user-facing enum keys `Enum_FeeType_*`, `Enum_PaymentMethod_*`, `Enum_PaymentType_*`, `Enum_AccountType_*`, `Enum_TaxCode_*`, `Enum_ReconciliationStatus_*`, `Enum_JournalEntryType_*` (verbatim) + swap their display sites to `LocalizeEnum` (FR-024) · `src/StageFright.Core/Modules/Localization/Resources/EnumsResource.resx`, display call sites across UI + Reports
- [x] **T043** [P] [US2] Remaining shell/app strings · `src/StageFright.UI/Pages/StartupError.razor`, `src/StageFright.App/MainPage.xaml`, `src/StageFright.App/App.xaml`, any remaining `src/StageFright.UI/Pages/**`
- [x] **T044** [P] [US2] Sweep every remaining `decimal.ToString("C")` / `string.Format("{0:C}", …)` / `FormatString="{0:C}"` display site (~34 files, concentrated in Finance + reports) to `MoneyFormatter` (FR-015) · repo-wide display call sites

**⟶ Wait for Wave 1, then:**

- [x] **T045** [US2] Confirm/adjust all provider + service registrations now taking `IStringLocalizer<T>` resolve from DI · `src/StageFright.App/MauiProgram.cs` (`RegisterCoreServices`)

**Checkpoint US2**: Every screen, dialog, tile, menu, settings tab, setup step, report and user-facing message renders en-AU text from resources; the repo-wide residual-literal + enum + no-`"C"` guards pass; a translator has exactly one set of `.resx` files to work from.

---

## Phase 5: User Story 3 — The app opens in the right language, and a user can change it (P3)

**Goal**: Add the persisted `Settings.LanguageCode`, the startup resolution ladder (explicit choice → OS display language when a matching set ships → en-AU, FR-023), the runtime-discovered supported-language catalog (FR-011), the Settings + Setup Wizard language pickers with a restart notice (FR-012/FR-013/FR-021), and a `qps-ploc` test pseudo-locale that proves add-a-language-with-zero-code + per-key fallback.

**Independent Test**: With `qps-ploc` available to tests — (a) a fresh install with no explicit choice starts in the OS display language when a matching set ships; (b) starts in en-AU when it does not (or the OS culture cannot be resolved); (c) an explicit pick in Settings persists, overrides the OS language on restart, and shows a restart notice at the point of change; (d) switching back to en-AU restores the original presentation; a partial language falls back key-by-key to en-AU.

### Tests (write to fail first)

- [ ] **T046** [US3] `LanguageProvider.ResolveStartupCultureAsync` ladder (explicit → OS exact → OS parent → en-AU, FR-023/SC-010), `SupportedLanguagesCatalog` runtime discovery + `qps-*` exclusion + endonym-from-`NativeName` (FR-011), `Find` null/blank/unknown; `AddLanguageCodeToSettings` migration round-trip; startup honours persisted `LanguageCode` (SC-005) and a switch leaves DB + GL byte-identical (SC-006) · `tests/StageFright.Core.Tests/Localization/`, `tests/StageFright.Data.Tests/`, `tests/StageFright.Integration.Tests/`
- [ ] **T047** [US3] `qps-ploc` end-to-end: catalog never lists it; selecting it re-presents the app; deliberately-omitted keys fall back to en-AU and log a Warning (SC-003/SC-004); bUnit — the language picker lists endonyms, marks the active one, and shows the restart notice (FR-012/FR-021) · `tests/StageFright.Localization.Tests/`, `tests/StageFright.UI.Tests/`

### Implementation

**Wave 1 — independent (different files):**

- [ ] **T048** [P] [US3] Add `public string? LanguageCode { get; set; }` (+ optional `HasMaxLength(16)` in the EF config); bump `Settings.SchemaVersion` patch · `src/StageFright.Core/Entities/Settings.cs` (+ `StageFrightDbContext` config)
- [ ] **T049** [P] [US3] Contracts `ILanguageProvider`, `ISupportedLanguagesCatalog`, `ISystemCultureProvider` · `src/StageFright.Core/Contracts/ILanguageProvider.cs`, `ISupportedLanguagesCatalog.cs`, `ISystemCultureProvider.cs`
- [ ] **T050** [P] [US3] `SupportedLanguage` immutable value object (`CultureCode`, `Endonym` from `CultureInfo.NativeName`, `IsDefault`; equality by `CultureCode`) · `src/StageFright.Core/Modules/Localization/SupportedLanguage.cs`

**⟶ Wait for T048, then:**

- [ ] **T051** [US3] Generate EF migration `AddLanguageCodeToSettings` (`Up`: `AddColumn<string>("LanguageCode","Settings", nullable:true)`; `Down`: `DropColumn`) + regenerate `StageFrightDbContextModelSnapshot.cs` · `src/StageFright.Data/Migrations/`

**⟶ Wait for T049 + T050, then:**

**Wave 2 — independent (different files):**

- [ ] **T052** [P] [US3] `SupportedLanguagesCatalog` — build `All` at runtime by enumerating shipped resource cultures (neutral en-AU + `<Marker>.<culture>.resx` satellites), endonyms from `CultureInfo.NativeName`, exclude `qps-*`; `Default` = the `IsDefault` entry; `Find` resolves null/blank/unknown to `null` (FR-011) · `src/StageFright.Core/Modules/Localization/SupportedLanguagesCatalog.cs`
- [ ] **T053** [P] [US3] `LanguageProvider` — resolution ladder over `ISettingsService` + `ISupportedLanguagesCatalog` + `ISystemCultureProvider`; `DefaultCulture` = en-AU; never throws (FR-023/FR-017) · `src/StageFright.Core/Modules/Localization/LanguageProvider.cs`
- [ ] **T054** [P] [US3] `SystemCultureProvider : ISystemCultureProvider` over the device/OS UI culture (`CultureInfo.InstalledUICulture` / MAUI device culture) · `src/StageFright.App/SystemCultureProvider.cs`

**⟶ Wait for Wave 2, then:**

- [ ] **T055** [US3] Startup culture wiring: register `ILanguageProvider` / `ISupportedLanguagesCatalog` / `ISystemCultureProvider`; in the startup scope call `ResolveStartupCultureAsync()` and set `CultureInfo.DefaultThreadCurrentCulture` / `DefaultThreadCurrentUICulture` **before** the `BlazorWebView` first renders · `src/StageFright.App/MauiProgram.cs`

**⟶ Wait for T055, then:**

**Wave 3 — independent (different files):**

- [ ] **T056** [P] [US3] Language `<select>` bound to `ISupportedLanguagesCatalog.All` by endonym, marks the active one, persists `Settings.LanguageCode`, shows an inline restart notice (FR-012/FR-014/FR-021); keys → `SettingsResource.resx` · `src/StageFright.UI/Pages/Settings/GeneralSettingsTab.razor` + `.razor.cs`
- [ ] **T057** [P] [US3] Setup Wizard first-run language step, pre-selecting the FR-023-resolved default (FR-013); keys → `SetupResource.resx` · `src/StageFright.UI/Pages/Setup/Tabs/*`
- [ ] **T058** [P] [US3] `qps-ploc` pseudo-locale fixtures — `<Marker>.qps-ploc.resx` beside each neutral file (bracketed/accented values, ~3 keys deliberately omitted), wired as test-fixture `Content`, **not** a shipped satellite (Decision 9) · `src/**/Resources/**/*.qps-ploc.resx`, `tests/StageFright.Localization.Tests/`

**⟶ Wait for Wave 3, then:**

- [ ] **T059** [US3] Add the `LanguageProvider` cascading-component seam (parallel to `ThemeProvider`) so a later story can add in-session live switching without touching call sites — no behaviour change in v1 (FR-021) · `src/StageFright.UI/Layout/` (new cascading provider + `ShellLayout` wrap)

**Checkpoint US3**: A fresh install follows the OS display language when a matching set ships and en-AU otherwise (SC-010); an explicit pick persists, wins over the OS language, and applies on next launch with a restart notice (SC-005); `qps-ploc` demonstrates a language added with zero code changes plus per-key fallback and logging (SC-003/SC-004); a language switch changes zero stored data or GL balances (SC-006).

---

## Phase 6: Polish & Cross-Cutting

**Wave 1 — independent (different files):**

- [ ] **T060** [P] Remove the per-phase scoping from the residual-literal guard so it asserts **zero** user-facing literals repo-wide (SC-001) · `tests/StageFright.Localization.Tests/`
- [ ] **T061** [P] Add the "where resource files live / how to add a language" guide for translators and maintainers (FR-022, SC-009) · `docs/` (new localization/adding-a-language doc)
- [ ] **T062** [P] Refresh project docs made stale by the extraction — `CLAUDE.md` (data-grid `FormatString="{0:C}"` guidance now via `MoneyFormatter`; new `Modules/Localization` slice; resource-file locations) and any `specs/**` doc that now reads wrong (FR-022) · `CLAUDE.md`, `specs/**`
- [ ] **T063** [P] Run the [quickstart.md](./quickstart.md) validation scenarios — en-AU render parity (SC-002), pseudo-locale switch + per-key fallback logging (SC-004), monetary amount shows `$` with culture separators under a non-`en` culture (FR-015), DB/GL byte-identical after a switch (SC-006), OS-language default with no explicit choice (SC-010) · `specs/027-localization-support/quickstart.md`

**⟶ Wait for Wave 1, then:**

- [ ] **T064** Full `dotnet build` and full `dotnet test` (no `--no-build`) across every project incl. `StageFright.Localization.Tests`; report build + test results · repo root

---

## Dependencies & Execution Order

**Phase order**: Setup (T001–T003) → Foundational (T004–T014) → US1 (T015–T028) → US2 (T029–T045) → US3 (T046–T059) → Polish (T060–T064). Each user-story phase is an independently testable increment; US1 must merge (pattern settled + reviewed) before US2's mechanical repetition; US3 depends only on the Foundational infra plus US1/US2 having converted the surfaces its pickers and catalog touch.

**Phase 1 — Setup**
- Wave 1 `[P]`: T001 (`Directory.Packages.props`), T002 (new test project + `.slnx`).
- Then: T003 (3 `.csproj` files — needs T001's package version).

**Phase 2 — Foundational**
- Wave 1 `[P]`: T004 (Core markers), T005 (Reports marker), T006 (UI markers) — blocks everything after, since `IStringLocalizer<T>` needs the marker types.
- Wave 2 `[P]`: T007 (`ILocalizer`/`Localizer`), T008 (logging factory), T009 (`LocalizeEnum`), T010 (`MoneyFormatter`), T011 (`_Imports.razor`), T012 (scan helpers), T013 (bUnit base).
- Then: T014 (DI wiring — needs T007 + T008).

**Phase 3 — US1**
- Tests first: T015 (guard suite), T016 (bUnit) — written to fail.
- Impl Wave 1 `[P]`: T017–T021 (five different `.resx` files).
- Impl Wave 2 `[P]`: T022–T027 (six different call-site files/areas — need their resx entries from Wave 1).
- Then: T028 (DI check — after the provider ctors change).

**Phase 4 — US2**
- Tests first: T029 (extended guards), T030 (report + plugin tests).
- Impl Wave 1 `[P]`: T031–T044 (fourteen module/area-scoped tasks, each its own resx + call sites; `EnumsResource`/T042, `NavigationResource`/T038, `ValidationResource`/T041 are single-writer per file).
- Then: T045 (DI check).

**Phase 5 — US3**
- Tests first: T046 (provider/catalog/migration/integration), T047 (`qps-ploc` e2e + picker bUnit).
- Impl Wave 1 `[P]`: T048 (`Settings` field), T049 (contracts), T050 (`SupportedLanguage`).
- Then: T051 (migration — needs T048).
- Impl Wave 2 `[P]`: T052 (`SupportedLanguagesCatalog`), T053 (`LanguageProvider`), T054 (`SystemCultureProvider`) — need T049 + T050.
- Then: T055 (`MauiProgram` culture wiring — needs T052–T054).
- Impl Wave 3 `[P]`: T056 (Settings picker), T057 (Setup step), T058 (`qps-ploc` fixtures) — need T055.
- Then: T059 (cascading seam — needs T056/T057 in place).

**Phase 6 — Polish**
- Wave 1 `[P]`: T060 (unscope guard), T061 (translator guide), T062 (doc refresh), T063 (quickstart run).
- Then: T064 (full build + test, reported).

### Parallel Opportunities

- **Setup**: T001 ∥ T002.
- **Foundational**: all of T004–T006 together; then all of T007–T013 together (seven files, no shared dependency).
- **US1**: the five resx tasks T017–T021 together; then the six conversion tasks T022–T027 together.
- **US2**: the fourteen module tasks T031–T044 are the widest parallel wave in the plan — one per module/area, each writing its own resx + its own call sites.
- **US3**: T048 ∥ T049 ∥ T050; then T052 ∥ T053 ∥ T054; then T056 ∥ T057 ∥ T058.
- **Polish**: T060 ∥ T061 ∥ T062 ∥ T063.
- Every `### Tests` task within a story may be written in parallel with its sibling test task, and both precede that story's implementation waves.
