# Phase 0 Research: Localization Support

**Feature**: `027-localization-support` | **Date**: 2026-08-27

Baseline finding from codebase investigation: there is **no localization infrastructure today** — zero `.resx`/`.resw`, no `IStringLocalizer`, no `AddLocalization`, no `RequestLocalization`, and no culture is ever set (only `CultureInfo.CurrentCulture` is *read*, in `GeneralSettingsTab.razor`, for currency symbol / month names). User-facing text is ~500–700 literals across ~150 files: 65 `.razor` + 64 `.razor.cs` in `StageFright.UI`, the `*MenuItemProvider` / dashboard-tile providers in `StageFright.Core`, the 11 report providers + PDF/CSV renderers in `StageFright.Reports`, and `Message` text on `StageFright.Core/Exceptions/*` + validation services.

Two facts that the 2026-08-27 Clarifications turn into concrete work items:
- **Accessibility attribute text is in scope** (FR-001, resolved 2026-08-27): ~119 `aria-label` / `alt` / `title` occurrences across 39 `.razor` files are user-facing literals to extract, on top of the visible-text count above. Genuinely decorative `alt=""` / `aria-hidden` subtrees are exempt.
- **Currency is displayed via raw `.ToString("C")` / `FormatString="{0:C}"`** in ~34 files (no shared money helper). The `"C"` specifier substitutes `CurrentCulture.NumberFormat.CurrencySymbol`, so under a non-`en-AU` culture it would render e.g. `€`. FR-015 (resolved 2026-08-27) requires those *display* sites to move to a fixed-AUD formatter — see Decision 11.

---

## Decision 1 — Localization mechanism: `Microsoft.Extensions.Localization` (`IStringLocalizer`)

**Decision**: Use the first-party `Microsoft.Extensions.Localization` package. `MauiProgram` calls `builder.Services.AddLocalization()`. Components and services take `IStringLocalizer<TResource>` where `TResource` is an area marker class; `.resx` files sit next to the marker class. The neutral (no-culture-suffix) `.resx` holds the Australian English baseline; additional cultures are `TResource.<culture>.resx` satellite assemblies. `IStringLocalizer` already does parent-culture fallback (`es-ES` → `es` → neutral), which satisfies FR-008.

**Rationale**: It is the framework-standard approach, needs no new UI toolkit or JavaScript (constitution §7.1/§7.3), the `dotnet` build already compiles `.resx` to satellite assemblies, and it works identically in the MAUI Blazor Hybrid `BlazorWebView` (in-process, single culture) as in server Blazor. Translators get plain `.resx` XML — editable in any resx editor or a translation tool — meeting SC-009.

**Alternatives considered**:
- *Strongly-typed `.resx` designer classes only* (`ResXFileCodeGenerator` → `Strings.Designer.cs`, `Strings.Title`). Rejected as the primary mechanism: the generated accessor throws or returns `""`/key on a missing culture entry rather than transparently falling back, and gives no hook to *record* the miss (FR-009). Kept as an optional convenience layer only if the team wants compile-time key checking, generated over the same `.resx`.
- *JSON resource files + custom loader*. Rejected: reinvents `IStringLocalizer`, adds a bespoke framework (against §3.1 "simple over clever"), and needs custom parsing/caching/fallback code that must itself be fully tested.
- *Database-stored strings*. Rejected: makes translation a data-migration problem, complicates backups, and offers no offline translator workflow.

---

## Decision 2 — Resource granularity: a small set of area-scoped marker classes, not per-component `.resx`

**Decision**: ~8 marker classes in `StageFright.UI` (`SharedResource`, `DashboardResource`, `MembersResource`, `RehearsalsResource`, `EventsResource`, `FinanceResource`, `SettingsResource`, `SetupResource`), plus `NavigationResource` and `ValidationResource` in `StageFright.Core`, plus `ReportsResource` in `StageFright.Reports`. Each screen injects the marker for its area; genuinely cross-cutting text (Save, Cancel, Yes/No, "Loading…") lives in `SharedResource`.

**Rationale**: `IStringLocalizer<T>` convention maps `T`'s namespace+name to a `.resx` path. One `.resx` per component would mean ~130 files per culture — unmanageable for translators and for the completeness guard. Area scoping keeps it to ~11 files per culture while still giving each string a clear owner. It also matches the app's existing module-slice organization (§4.1).

**Alternatives considered**:
- *One global `AppResource.resx`*. Rejected: a single 600-entry file is hard to review, invites key collisions, and forces every project to depend on the project that owns it.
- *Per-component `.resx`*. Rejected: file-count explosion; churn on every component rename.

---

## Decision 3 — Key naming: `Area_Context_Meaning`, wording-independent

**Decision**: Keys are `PascalCase` segments joined by `_`: `Nav_Members`, `Members_List_Title`, `Members_Form_FirstNameLabel`, `Shared_Action_Save`, `Validation_Member_FirstNameRequired`, `Reports_MemberList_ColumnJoinDate`, `Setup_General_FinancialYearHelpText`. Placeholders are named (`{OrganisationName}`, `{Count}`), never positional-only. Count-dependent text uses explicit `_One` / `_Other` key variants resolved by a tiny helper (`Plural(count, "Key")`), since `.resx` has no native ICU plural support.

**Rationale**: FR-002 requires the key to survive a wording change. `Area_` prefixes make the completeness guard and "which module owns this" trivial. Named placeholders (FR-010) let a translator reorder arguments. The `_One`/`_Other` convention is the minimal thing that covers the app's actual plural cases (e.g. `"{Count} year"` / `"{Count} years"` in `GeneralSettingsTab`) without pulling in an ICU library.

**Alternatives considered**:
- *Auto-generated keys from source text* (`"Member List" → MemberList`). Rejected: the key changes when the copy changes, defeating FR-002; collisions on short words.
- *Full ICU MessageFormat via a library*. Rejected for v1: heavier dependency than the handful of plural cases justify; can be layered later behind the same helper.

---

## Decision 4 — Missing-key detection: logging decorator + build/test-time completeness guard

**Decision**: Two layers. (a) Runtime: a `MissingKeyLoggingLocalizerFactory` decorator wraps `IStringLocalizerFactory`; when `IStringLocalizer` returns a `LocalizedString` with `ResourceNotFound == true`, it logs a `Warning` through Serilog with the key and requested culture, then returns the neutral (en-AU) value (FR-008/FR-009). (b) Build/test: a new `StageFright.Localization.Tests` project enumerates every key referenced in code/markup (regex/Roslyn scan for `L["…"]`, `localizer["…"]`, `IStringLocalizer` indexer calls) and asserts each has a neutral `.resx` entry (SC-008), and that each shipped non-neutral `.resx` contains only known keys. A companion residual-literal scan asserts no user-facing string literal remains in the files a given phase has converted (SC-001), scoped per phase so it can go green incrementally.

**Rationale**: The runtime layer protects the end user; the test layer stops a gap ever shipping. Scoping the literal scan per phase lets US1 land green without US2 being done.

**Alternatives considered**:
- *Fail hard at runtime on a missing key*. Rejected: FR-008 explicitly wants graceful fallback, not a crash.
- *Only a test-time check*. Rejected: does not satisfy FR-009's "record the fallback" for a locale gap discovered in the field.

---

## Decision 5 — Resolving the startup culture: explicit choice → OS display language → en-AU (MAUI Blazor Hybrid)

**Decision**: In `MauiProgram.CreateMauiApp`, after `builder.Build()` and inside the existing startup scope (alongside `RunStartupSequence`), `LanguageProvider` (Core) resolves the culture to apply in this order, then `MauiProgram` sets `CultureInfo.DefaultThreadCurrentCulture` and `CultureInfo.DefaultThreadCurrentUICulture` to it before the `BlazorWebView` first renders:

1. **Explicit choice** — `settings.LanguageCode` is non-null and names a language in `ISupportedLanguagesCatalog` → use it. Always wins (FR-023, FR-014).
2. **OS display language** — no explicit choice: read the operating-system UI culture (`CultureInfo.InstalledUICulture`, or the MAUI device culture), and if the catalog has a match by exact culture, then by parent language → use that match (FR-023 / SC-010).
3. **Fallback** — no explicit choice and no OS match, or the OS culture string will not construct a `CultureInfo` → `catalog.Default` (`en-AU`).

`LanguageProvider` reads the OS culture through a tiny injectable seam (`ISystemCultureProvider`, implemented in the App layer over the device culture) so the whole ladder is unit-testable without MAUI. While `en-AU` is the only shipped catalog entry, step 2 never matches and the effective default stays `en-AU` — but the ladder is built now so adding a language "just works" (SC-003). A `LanguageCode` that names a language no longer shipped is treated as "no explicit choice" and re-resolved from step 2.

`ISupportedLanguagesCatalog` is **not** a hand-maintained list (FR-011, resolved 2026-08-27). Its implementation builds the language set at runtime by enumerating the resource cultures actually shipped — the neutral set (`en-AU`) plus every `<Marker>.<culture>.resx` satellite culture present in the loaded resource assemblies — with each entry's endonym taken from `CultureInfo.GetCultureInfo(code).NativeName`. Cultures whose name matches the pseudo-locale pattern (`qps-*`, e.g. `qps-ploc`) are filtered out so the test pseudo-locale never appears in the picker or in step 2 matching. Dropping in a new satellite `.resx` is therefore sufficient for it to be offered — no code or list edit (SC-003).

**Rationale**: The Hybrid `BlazorWebView` runs in-process on one culture; there is no per-request pipeline. Setting the default thread cultures at composition time is the documented way to make the whole app (Blazor render + QuestPDF + formatting) honour one culture. It mirrors how `Settings.Theme` is read at startup by `ThemeProvider`. Reading the OS language only when no explicit choice is stored keeps the user's saved preference authoritative (FR-023) and means an unchanged install behaves exactly as before while `en-AU` is the only shipped set.

**Alternatives considered**:
- *Set culture inside a root Blazor component's `OnInitialized`*. Rejected: leaves a first-render flash in the wrong culture and does not cover non-UI code (reports generated from a background path).
- *`RequestLocalization` middleware*. Not applicable — no ASP.NET request pipeline in Hybrid.
- *Always follow the OS language, ignoring the stored choice*. Rejected: FR-023 requires an explicit selection to win; a user who picked a language must not have it silently overridden by an OS change.
- *Auto-adopt (persist) the OS language into `LanguageCode` on first run*. Rejected: that would make a later OS change stop taking effect; keeping `LanguageCode` null until the user actively picks preserves "follow the system" as the default behaviour.

---

## Decision 6 — FR-021: language change applies on next launch, with a restart notice (v1 — resolved)

**Decision**: The Settings/Setup language picker persists `LanguageCode` immediately and shows an inline "Restart the app to finish switching language" notice. No in-session re-render of the whole tree in v1. The design keeps a `LanguageProvider` cascading-component seam (parallel to `ThemeProvider`) so a later story can add live switching by cascading a culture value and re-rendering, without touching the extracted call sites.

**Rationale**: Process-wide culture in Hybrid means a true live switch requires re-initialising every component and re-resolving singletons (menu providers are `AddSingleton`) — real work with real regression surface. The 2026-08-27 Clarifications session **resolved** this: in-session live switching is **out of scope for v1**; only next-launch switching ships (the cascading seam is retained for a future story). Shipping restart-based switching first de-risks Stories 1–2.

**Alternatives considered**:
- *Force an app restart programmatically on change*. Rejected: abrupt, risks losing unsaved work elsewhere; a notice lets the user choose when.
- *Block the picker behind "advanced / requires restart" wording only*. Rejected: still needs the same persistence + notice; no simpler.

---

## Decision 7 — Culture-invariant boundary for stored/financial values (FR-016)

**Decision**: Only *display* formatting follows the selected culture. All persistence, serialization, GL math, backup manifests, and EF value conversions continue to use invariant/explicit formats (they already do — amounts are `decimal`, dates are `DateTime` UTC, `SchemaVersion` is a fixed string). Add an integration test that snapshots the DB + computed member/GL balances, switches `LanguageCode`, re-runs the resolve path, and asserts byte-identical stored state (SC-006). Number/date *input* parsing in forms uses the active culture via the existing Blazor `InputNumber`/`InputDate` binding, with tests that a culture using `,` as the decimal separator still round-trips an entered amount.

**Rationale**: The finance constitution (§3.5/§3.6) and FR-016 require stored values to be untouched. The app already stores culture-invariant; this decision is mostly "don't regress that" plus explicit test coverage.

---

## Decision 8 — Plugin-contributed text is out of scope; host renders it as-provided (FR-020)

**Decision**: No change to `StageFright.Plugins.Contracts`. The shell already renders `MenuItem.Title`, tile captions, settings-tab labels, and plugin `ReportName` as plain strings — it keeps doing that verbatim for plugin-supplied values. Only the *MVP* providers (which live in `StageFright.Core`/`StageFright.UI`, not in a plugin) are converted to resource lookups. A test loads the sample `StageFright.TestPlugin` under a non-en culture and asserts the app renders its English strings without error or blanking.

**Rationale**: Plugins own their own text and localization story; forcing a contract change would break the leaf-assembly rule for `StageFright.Plugins.Contracts` and is explicitly a non-goal in the spec.

---

## Decision 9 — Test-only pseudo-locale to prove switching and fallback

**Decision**: Ship a `*.qps-ploc.resx` (or `en-XA`) pseudo-localized set that is present **only in test fixtures / not registered as a user-selectable language** — the runtime catalog discovery (Decision 5 / FR-011) excludes any culture whose name matches the pseudo pattern `qps-*`, so it is never offered in the picker even when the `.resx` is on disk. Each string is derived by bracketing + accenting the neutral value, with a few keys deliberately omitted. Tests use it to prove: selecting a non-baseline culture re-presents the app (SC-003/SC-005), and omitted keys fall back to en-AU and are logged (SC-004).

**Rationale**: Meets SC-003 ("adding a language needs zero code changes, demonstrated end-to-end") without committing the team to translating a real language, which the spec Assumptions place outside this feature. `qps-ploc` is the Windows-standard pseudo-locale and needs no translator.

**Alternatives considered**:
- *Translate a real second language (e.g. `en-US` or `mi-NZ`)*. Rejected for v1 scope: real translation is a business decision the spec defers; `en-US` differs too little from `en-AU` to prove much.

---

## Decision 10 — Enum display text: `Enum_<Type>_<Member>` keys in a shared `EnumsResource` + a `LocalizeEnum` helper (FR-024)

**Decision**: Every user-facing enum member gets a resource key `Enum_<EnumTypeName>_<MemberName>` — e.g. `Enum_MemberStatus_Active`, `Enum_FeeType_Annual`, `Enum_TaxCode_Standard` — in a new **`EnumsResource`** marker in `StageFright.Core` (not per-area), because the same enums (`MemberStatus`, `FeeType`, `PaymentMethod`, `PaymentType`, `AccountType`, `TaxCode`, `ReconciliationStatus`, `JournalEntryType`, `Theme`) surface in both `StageFright.UI` and `StageFright.Reports` and must read identically. A `LocalizeEnum` extension (`this Enum value` → `IStringLocalizer<EnumsResource>["Enum_" + type.Name + "_" + value]`, routed through the missing-key logging decorator) replaces every `enum.ToString()` / hardcoded switch at a display site. `<select>` option lists that today hardcode enum-like tokens (`["Active","Inactive","Archived","All"]` in the report providers) keep the invariant token as the option **value** and resolve the option **label** through the same keys, with `Reports_Filter_OptionAll` for the synthetic "All". In-scope enums are those actually rendered to a user; `ReportFilterType`, `ReportColumnAlignment`, `DashboardTileSize`, `PlatformThemePreference`, and audit action codes are excluded.

**Rationale**: One predictable key shape the completeness guard enforces (Decision 4). A single shared `EnumsResource` keeps a status label identical on screen and in a printed report and avoids duplicating ~40 enum entries across eight area files. Keeping the enum name/number out of the resource path preserves FR-016 — filter tokens, sort keys, storage and GL are untouched.

**Alternatives considered**:
- *`[Display(Name=...)]` / `[Description]` attributes on enum members.* Rejected: the attribute argument is a compile-time constant literal — it cannot vary by culture without `[Display(ResourceType=...)]`, which needs generated resource-accessor types and still bypasses the missing-key logging decorator.
- *A hand-maintained `Dictionary<Enum,string>` per enum.* Rejected: no fallback or logging, duplicates the resx, drifts when a member is added.
- *Per-area enum keys (e.g. `Members_Status_Active`).* Rejected: the same enum then has two labels that can diverge between the screen and the report.

---

## Decision 11 — Currency display: fixed AUD symbol, culture-driven number format only (FR-015)

**Decision**: Monetary amounts are always presented in Australian dollars. The currency symbol/code (`"$"` / `"AUD"`) is **fixed** regardless of the active UI culture; only the decimal separator, digit grouping and symbol placement of the number follow the selected region. A small shared formatter (`StageFright.Core/Localization/MoneyFormatter` — a static helper or `ICurrencyFormatter`) formats a `decimal` using `CultureInfo.CurrentCulture`'s `NumberFormatInfo` for separators/grouping/placement but overrides the currency symbol to `"$"` (and offers an explicit-`"AUD "` prefix variant for reports/exports where disambiguation matters). Every display site that today calls `decimal.ToString("C")`, `string.Format("{0:C}", …)`, or `RadzenDataGridColumn FormatString="{0:C}"` (~34 files) is converted to this formatter. Dates and plain numbers use the active culture directly via the existing Blazor `InputDate`/`InputNumber` binding.

**Rationale**: The app's money is stored, reconciled and GL-posted in one real currency (AUD). `"C"` formatting substitutes the *culture's* currency symbol — e.g. `€` under `fr-FR` — which would misrepresent an AUD balance and can visually contradict the GL. Keeping the symbol fixed while still localising separators/grouping honours regional number conventions without restating the amount in another currency (consistent with FR-016 and the spec's "financial math and stored values stay culture-invariant" edge case). A single formatter also gives the residual-literal / format guard one thing to enforce.

**Alternatives considered**:
- *Full `"C"` culture formatting (culture's own currency symbol).* Rejected by the 2026-08-27 Clarifications — misrepresents the currency.
- *Always emit the ISO code `"AUD 1,234.50"` in every language.* Rejected as the default (heavier for the common single-language case); offered as the opt-in report/export variant only.
- *Invariant culture for all money (fixed `$` and fixed `.`/`,`).* Rejected: ignores that separators legitimately differ by region, which FR-015 says should follow the selected region.

---

## Resolved unknowns / clarifications

| Spec marker | Resolution |
|---|---|
| FR-021 (immediate vs next-launch switch) | **Resolved** in the 2026-08-27 Clarifications session: applies on **next launch + restart notice** (Decision 6); in-session live switching is **out of scope for v1**. A `LanguageProvider` cascading seam is still included so a later story can add it without touching call sites. |
| Currency display under a non-`en-AU` region (FR-015) | **Resolved** (2026-08-27): amounts stay in Australian dollars — `"$"` / `"AUD"` is fixed; only separators, grouping and symbol placement follow the region. Display sites use a shared fixed-AUD formatter, never `.ToString("C")` / `{0:C}` (Decision 11). |
| How a new language is registered (FR-011) | **Resolved** (2026-08-27): the app auto-discovers shipped resource cultures at runtime and derives endonyms from `CultureInfo.NativeName` — no hand-maintained list; `qps-*` pseudo-locales excluded by name (Decision 5, data-model §2). |
| Accessibility text (aria-label / alt / title) in scope (FR-001 / SC-001) | **Resolved** (2026-08-27): in scope and covered by the residual-literal + completeness guards; decorative/empty `alt=""` / `aria-hidden` exempt (Decision 4, key-catalog §1 rule 8 / §3). |
| Which additional languages ship | None (spec Assumptions). Infrastructure + `qps-ploc` test locale only (Decision 9). |
| RTL / non-Latin | Out of scope (spec Assumptions); key/resx design does not preclude it. |

## New dependency

`Microsoft.Extensions.Localization` — add `<PackageVersion Include="Microsoft.Extensions.Localization" Version="10.0.x" />` to `Directory.Packages.props` (align the patch with the existing `Microsoft.Extensions.*` 10.0.10 entries), then version-less `<PackageReference>` in `StageFright.UI.csproj`, `StageFright.Reports.csproj`, and `StageFright.Core.csproj` (Core may take `Microsoft.Extensions.Localization.Abstractions` only if the concrete factory is wired from the App layer — decide during Phase 1 contracts). No other new packages.
