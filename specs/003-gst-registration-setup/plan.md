# GST Registration in Setup Wizard & GST/BAS Settings Tab — Implementation Plan

> **Status:** Planned (not started). Plan only — no code changes yet.
> **Branch:** `003-gst-registration-setup` (branched from `ExpandFnance`).
> **Spec:** `specs/003-gst-registration-setup/spec.md` (FR-111–FR-120, SC-101–SC-105).

## Context

`specs/002-finance-expansion` added `Settings.IsGstRegistered`/`AnnualFeeGstCode`/`AttendanceFeeGstCode` but only wired them into `GeneralSettingsTab.razor` (`src/StageFright.UI/Pages/Settings/GeneralSettingsTab.razor:132-185`). The first-run Setup Wizard (`src/StageFright.UI/Pages/Setup/SetupWizard.razor`) has no GST awareness at all, and the General tab is now 212 lines / 9+ setting groups that overflow the MAUI WebView pane. This plan implements the approved spec: an ABN field (always required, independent of GST status), a real 4-step wizard that captures GST + ABN at first run, a new "GST / BAS" Settings tab carrying the existing GST controls off General, cross-tab save safety, and a full-screen seeding-progress modal.

**Non-negotiables that shape everything (CLAUDE.md):** one class per file; `.razor` + `.razor.cs` pairs, no `@code` blocks; custom exceptions (`ValidationException`) at service boundaries; exhaustive `Should_X_When_Y` tests; `dotnet build` + full `dotnet test` green after each phase; no soft-delete concerns (`Settings` is an exempt singleton; `Abn` is a plain field).

---

## Core design decisions

### 1. ABN validation as a reusable attribute over a pure algorithm class

`AbnValidator` (`src/StageFright.Core/Modules/Settings/AbnValidator.cs`) is a static class implementing the ATO's published weighted-modulus-89 checksum: input must be exactly 11 digit characters (no spaces — FR-111/Out-of-Scope explicitly rules out display masking); subtract 1 from the first digit; multiply the 11 digits by weights `{10,1,3,5,7,9,11,13,15,17,19}`; valid iff the sum is a non-zero multiple of 89.

`AbnAttribute : ValidationAttribute` (`src/StageFright.Core/Modules/Settings/AbnAttribute.cs`) wraps it: `IsValid(value)` returns `true` immediately when the value is null/empty (so the same attribute works whether or not the field is also `[Required]`), otherwise delegates to `AbnValidator.IsValid`.

- `SetupFormModel.Abn` gets `[Required]` + `[Abn]` → wizard cannot finish without a valid ABN.
- `Settings.Abn` gets only `[Abn]` → empty passes (existing installs aren't blocked), malformed non-empty values are rejected. This is also the first real DataAnnotations validator `Settings` will carry — `GeneralSettingsTab.razor` already wraps its form in `<DataAnnotationsValidator />` today with nothing for it to check.

Both `AbnValidator` and the bound model field always operate on the plain 11-digit string — display formatting (below) is a presentation-only concern layered on top, never persisted.

### 1a. ABN display mask is a reusable `InputText` subclass, not custom JS

A new shared component, `src/StageFright.UI/Shared/AbnInput.cs` (a single C# file — it has no markup of its own, so it does not get a paired `.razor`; this is the standard Blazor idiom for subclassing a built-in input component), subclasses `Microsoft.AspNetCore.Components.Forms.InputText` (Blazor's own built-in component — permitted under the "no custom JavaScript" rule since it's framework code, not JS interop) and overrides its two extension points:

- `FormatValueAsString(string? value)` — inserts spaces into the raw digit string to render the standard **"XX XXX XXX XXX"** (2-3-3-3) grouping for display.
- `TryParseValueFromString(string? value, out string? result, out string? validationErrorMessage)` — strips every non-digit character from whatever the user typed or pasted, truncates to 11 digits, and sets `result` to that plain digit string (never spaces). Always succeeds (returns `true`) so `AbnAttribute` — not this component — owns validity.

Because it subclasses `InputText`, it inherits full `EditContext`/`FieldIdentifier`/`ValidationMessage` wiring for free — call sites use it exactly like `InputText` (`<AbnInput @bind-Value="_model.Abn" class="form-control form-control-sm" />`), and both the wizard and `GeneralSettingsTab` reuse the same component instead of duplicating masking logic. No JavaScript, no JS interop.

### 2. `Settings.Abn` is a standing identity fact, not a GST preference

Added as `string? Abn` on `src/StageFright.Core/Entities/Settings.cs`, alongside a new nullable-column migration. It is **not** touched by GST-toggle logic (unlike `AnnualFeeGstCode`/`AttendanceFeeGstCode`, which get force-nulled when unregistered) — placed physically near `OrganizationName` in the entity, not near the GST properties.

### 3. Wizard becomes one component, 4 steps, one shared `EditContext`

`SetupWizard.razor`/`.razor.cs` stay a single file pair (consistent with `GeneralSettingsTab.razor` already being 212 lines as existing precedent) rather than fragmenting into per-step child components, avoiding cross-component `EditContext` cascading. The component owns `EditContext _editContext` explicitly (constructed over `_model` in `OnInitialized`) so `<EditForm EditContext="_editContext">` replaces today's `<EditForm Model="_model">`. Steps:

1. **Organisation** — Organisation Name, ABN.
2. **Fees & Renewal** — Annual Fee, Attendance Fee, Membership Renewal Month.
3. **GST Registration** — `IsGstRegistered` toggle; when on, Annual/Attendance Fee GST-code dropdowns (same three options as Settings). No confirm dialog — there are no prior postings to warn about for a brand-new org.
4. **Review & Finish** — read-only summary, "Load sample data" checkbox, Back/Finish.

`_currentStep` (int, 1–4) gates which step's markup renders. **Next** is `type="button"` calling `_editContext.Validate()` — since every not-yet-visited field already has a DataAnnotations-satisfying default (`MembershipRenewalMonth = 1`, fees default `0`), a full-model validate on each Next only ever surfaces errors for fields the user has actually reached, so no custom per-step validation subset is needed. **Finish** stays `type="submit"` wired to the existing `OnValidSubmit="HandleValidSubmitAsync"`. A "Step X of 4" label plus a 4-segment progress bar renders above the form.

### 4. Cross-tab save safety (lost-update fix)

Splitting the single General-tab `Settings` form into General + GST/BAS tabs, each independently loaded/saved over the same singleton row, creates a lost-update race: edit General (unsaved) → switch to GST/BAS → toggle + save → switch back to General → save stale copy → GST change silently reverted. Fix: both tabs' `HandleSaveAsync` re-fetch the current DB row immediately before persisting and copy across only the fields the *other* tab owns:

- `GeneralSettingsTab.HandleSaveAsync`: fetch fresh row, copy `IsGstRegistered`/`AnnualFeeGstCode`/`AttendanceFeeGstCode` from it onto `_settings` before calling `SettingsService.SaveAsync(_settings)`.
- `GstSettingsTab.HandleSaveAsync`: fetch fresh row, copy every *other* field (`OrganizationName`, `Abn`, fees, months, ages, `Theme`, `ShowParticipationGraphs`, `LastCommitteeResetYear`) from it onto `_settings` before saving.

### 5. Seeding modal is a new, distinct CSS class

`ReportViewer.razor:11` already references `.modal-backdrop-light`, which has no CSS definition anywhere (confirmed via repo-wide search) — a pre-existing gap. Rather than fix/reuse it (which would change `ReportViewer`'s shipped appearance as a side effect), the wizard gets its own class (`.setup-seeding-overlay`) in `src/StageFright.App/wwwroot/app.css`, scoped to a fixed-position, full-viewport, dimmed backdrop with a centered card.

---

## Phases (each ends green: `dotnet build` + full `dotnet test`)

```
Phase 1: Abn foundation ──┬── Phase 2: Setup Wizard (4 steps + seeding modal)
 (entity/migration/        │
  validator/attribute/      └── Phase 3: Settings GST/BAS tab split + cross-tab safety
  SetupRequest/Service)
```

Phase 2 and Phase 3 both depend on Phase 1 (the `Abn` field/validator) but not on each other; either order works after Phase 1 lands.

### Phase 1 — ABN foundation

**New files:**
- `src/StageFright.Core/Modules/Settings/AbnValidator.cs` — static `IsValid(string? abn)`, weighted-modulus-89 checksum, exactly-11-digits requirement.
- `src/StageFright.Core/Modules/Settings/AbnAttribute.cs` — `ValidationAttribute` subclass wrapping `AbnValidator`; null/empty passes.
- `src/StageFright.UI/Shared/AbnInput.cs` — `InputText` subclass implementing the "XX XXX XXX XXX" display mask (Core design decision 1a). Built here, in Phase 1, since both Phase 2 (wizard) and Phase 3 (Settings) consume it.

**Changed files:**
- `src/StageFright.Core/Entities/Settings.cs` — add `public string? Abn { get; set; }` with `[Abn]`, placed near `OrganizationName`.
- `src/StageFright.Core/Modules/Settings/SetupRequest.cs` — record gains `Abn`, `IsGstRegistered`, `AnnualFeeGstCode`, `AttendanceFeeGstCode`.
- `src/StageFright.Core/Modules/Settings/SetupService.cs`:
  - `Validate(SetupRequest request)` (line 93) — add ABN required + `AbnValidator.IsValid` check (service-layer re-validation, independent of UI).
  - `InitializeAsync` (line 40) — force `AnnualFeeGstCode`/`AttendanceFeeGstCode` to `null` when `request.IsGstRegistered` is false, before constructing the `SettingsEntity`; set `settings.Abn = request.Abn.Trim()`, `settings.IsGstRegistered`, and the (possibly-nulled) GST codes on the entity at line 48-62.
- `src/StageFright.Core/Modules/Settings/SettingsService.cs` — `SaveAsync` (line 25) gains a check: if `settings.Abn` is non-empty and fails `AbnValidator.IsValid`, throw `ValidationException` before persisting. No requirement that `Abn` be present.

**Migration:**
- `dotnet ef migrations add AddAbnToSettings --project src/StageFright.Data/ --startup-project src/StageFright.App/` — adds nullable `Abn` column to `Settings`. No backfill (existing rows get `null`).

**Tests:**
- `AbnValidator` unit tests: ATO's published test ABN (51 824 753 556) valid; checksum-broken variant invalid; wrong length invalid; non-digit characters invalid; null/empty invalid.
- `AbnAttribute` unit tests: null/empty passes; valid ABN passes; malformed non-empty fails.
- `AbnInput` bUnit tests: typing digits renders "XX XXX XXX XXX" grouping; `@bind-Value` yields a plain digit string with no spaces; pasting a pre-formatted value (with spaces/hyphens) parses to the correct 11-digit value; typing an 12th+ digit is ignored; `EditContext` wiring (field marked modified) still fires through the inherited `InputText` plumbing.
- `SetupService`/`SetupRequest` unit tests: missing/invalid ABN blocks `InitializeAsync` with `ValidationException`; GST codes forced null when `IsGstRegistered` is false regardless of what was passed in.
- `SettingsService.SaveAsync` unit tests: empty `Abn` saves successfully; malformed non-empty `Abn` throws `ValidationException`; valid `Abn` saves successfully.
- `StageFright.Data.Tests`: migration test — existing seeded/migrated rows survive with `Abn = null`, no exceptions.

### Phase 2 — Setup Wizard: 4 steps + seeding modal

**Changed files:**
- `src/StageFright.UI/Pages/Setup/SetupFormModel.cs` — add `Abn` (`[Required]` + `[Abn]`), `IsGstRegistered` (bool, default false), `AnnualFeeGstCode`/`AttendanceFeeGstCode` (`GstCode?`, no `[Required]`).
- `src/StageFright.UI/Pages/Setup/SetupWizard.razor` — restructure into 4 conditionally-rendered step blocks inside one `<EditForm EditContext="_editContext">`, step indicator, Back/Next/Finish buttons; the Organisation step's ABN field uses `<AbnInput @bind-Value="_model.Abn" class="form-control form-control-sm" />` (Core design decision 1a) instead of a plain `InputText`; replace the inline spinner block (lines 72-78) with a full-screen overlay (`_seedingInProgress` flag) using the new `.setup-seeding-overlay` class, message "Setting up your sample data — this may take a few minutes. Please don't close the app," plus the existing live progress text.
- `src/StageFright.UI/Pages/Setup/SetupWizard.razor.cs`:
  - Add `_currentStep` (int, default 1), `_editContext` (constructed in `OnInitialized` over `_model`), `_seedingInProgress` (bool).
  - `HandleNext()` / `HandleBack()` — `_currentStep` +/- 1 clamped to [1,4]; `HandleNext` calls `_editContext.Validate()` and only advances if it returns `true`.
  - `HandleValidSubmitAsync` (line 17) — `SetupRequest` construction (line 24) gains `Abn: _model.Abn!.Trim()`, `IsGstRegistered: _model.IsGstRegistered`, `AnnualFeeGstCode: _model.AnnualFeeGstCode`, `AttendanceFeeGstCode: _model.AttendanceFeeGstCode`. Wrap only the `DebugSeeder.SeedAsync` call in `_seedingInProgress = true` / `finally { _seedingInProgress = false; }`, not the whole method — so the modal appears only once seeding actually starts, not during the fast settings-creation step.
- `src/StageFright.App/wwwroot/app.css` — new `.setup-seeding-overlay` rule (fixed position, full viewport, dimmed backdrop, centered card; distinct from `ReportViewer`'s undefined `.modal-backdrop-light`).

**Tests:**
- `SetupWizard` bUnit tests rewritten: Next/Back navigation across all 4 steps; Next blocked on missing/invalid ABN or empty org name; GST dropdowns appear only when `IsGstRegistered` is toggled on and disappear (with codes cleared) when toggled off; Finish composes the full `SetupRequest` including ABN and GST fields; seeding overlay appears only once seeding starts (not during the settings-creation await) and only when "Load sample data" is checked.

### Phase 3 — Settings GST/BAS tab split + cross-tab safety

**New files:**
- `src/StageFright.UI/Pages/Settings/GstSettingsTab.razor` — GST toggle + confirm-dialog + two GST-code dropdowns, moved verbatim from `GeneralSettingsTab.razor:132-185` (identical confirm-before-commit behaviour).
- `src/StageFright.UI/Pages/Settings/GstSettingsTab.razor.cs` — `OnInitializedAsync` loads `_settings` via `SettingsService.GetAsync()`; `_pendingGstToggle`/`HandleGstToggleRequested`/`ConfirmGstToggle`/`CancelGstToggle` moved verbatim from `GeneralSettingsTab.razor.cs:26,113-132`; `HandleSaveAsync` implements the refetch-and-merge-non-owned-fields pattern from Core design decision 4.

**Changed files:**
- `src/StageFright.UI/Pages/Settings/GeneralSettingsTab.razor` — remove the GST block (lines 132-185); add an ABN field near the Organisation Name field (line 36-40) using `<AbnInput @bind-Value="s.Abn" class="form-control form-control-sm" />` (same shared component as the wizard) with a non-blocking small-text notice ("ABN not on file") shown when `s.Abn` is empty — notice only, no blocking of the Save button.
- `src/StageFright.UI/Pages/Settings/GeneralSettingsTab.razor.cs` — remove `_pendingGstToggle`/`HandleGstToggleRequested`/`ConfirmGstToggle`/`CancelGstToggle` (lines 26, 113-132); `HandleSaveAsync` (line 75) implements the refetch-and-merge pattern from Core design decision 4 before calling `SettingsService.SaveAsync`.
- `src/StageFright.UI/Pages/Settings/SettingsPage.razor` — insert `<Tab Title="GST / BAS" OnClick="OnGstClicked">` (lazily rendering `<GstSettingsTab />` behind a `GstShown` flag) immediately after the General tab.
- `src/StageFright.UI/Pages/Settings/SettingsPage.razor.cs`:
  - Add `internal bool GstShown;` and `internal void OnGstClicked() { GstShown = true; NavToTab("gst"); }`.
  - `DefaultTabIndex` switch (line 35) becomes `"gst" => 1, "event-types" => 2, "backup" => 3, _ => 0`.
  - Lazy-render switch (line 44) renumbers accordingly (`case 1: GstShown = true; break; case 2: EventTypesShown = true; break; case 3: BackupShown = true; break;`).
- `specs/001-initial-mvp/spec.md` NFR-010 reserved `?tab=` table — add a row for `gst` (Settings page).

**Tests:**
- GST-toggle/confirm-dialog bUnit tests move from `GeneralSettingsTab`'s test file to a new `GstSettingsTab` test file (same assertions, new host component).
- `GeneralSettingsTab` bUnit tests updated: assert GST UI is absent; assert ABN input is present and the "not on file" notice shows/hides correctly; assert saving with an empty `Abn` still succeeds (no blocking).
- `SettingsPage` bUnit tests: tab order includes GST/BAS at index 1; `?tab=gst` deep-links correctly; existing `?tab=event-types`/`?tab=backup` deep-links updated to the new indices.
- New cross-tab concurrency test (new test class, e.g. `SettingsCrossTabSaveTests`): save GST tab, then save General tab from a stale in-memory copy, assert the GST change survives; and the symmetric case (save an ABN change on General, then save GST/BAS from a stale copy, assert the ABN survives).

---

## Explicitly NOT changing

- `GstCalculator`, `BasSummaryReportProvider`, or any GL posting logic — this is data-entry only (wizard + Settings UI) plus the new `Abn` field.
- The *stored* representation of `Abn` — always a plain 11-digit string with no spaces; only the on-screen display (via `AbnInput`) is masked.
- No retroactive enforcement forcing existing installs to supply an ABN immediately (Phase 1's `SettingsService.SaveAsync` check only rejects *malformed*, not *missing*, values).
- `ReportViewer.razor`'s existing (unstyled) `.modal-backdrop-light` usage — left untouched; the new overlay is a separate class.
- Confirm-dialog behaviour/wording for the GST toggle — moved to `GstSettingsTab` unchanged.

## Risks / watch-outs

- MAUI WebView gotcha (per CLAUDE.md): the new "GST / BAS" tab must follow the existing `Shown`-flag lazy-render pattern to avoid concurrent DbContext access / `OnShown` callback failures — do not use Bootstrap's `OnShown` event for the new tab (`SettingsPage.razor` already avoids this via `OnClick`, per the code comment at `SettingsPage.razor.cs:60-63`).
- `EditContext.Validate()` triggered from a plain `type="button"` Next handler must not accidentally trigger form submission (only the Finish button is `type="submit"`) — verify Enter-key behaviour doesn't advance/submit unexpectedly mid-wizard.
- The cross-tab merge logic (Core design decision 4) must copy fields *by name*, not do a wholesale entity replace — a careless implementation could silently reintroduce the lost-update bug it's meant to fix.
- Migration must be verified as a simple `AddColumn` (nullable, no default-value backfill needed) — confirm EF doesn't emit an unexpected table rebuild for SQLite.
- Format-as-you-type inputs are prone to cursor-jump bugs (the caret resets to the end after each re-render once spaces are inserted mid-string). `AbnInput` must be manually verified for usable mid-string editing (e.g. correcting a digit in the middle of an already-formatted ABN), not just append-only typing — a bUnit test can assert the formatted output but can't fully exercise real caret behaviour, so this needs a manual check too.

## Verification (per phase)

1. `dotnet build` and full `dotnet test` (no `--no-build`) — all 5 test projects green.
2. Manual E2E (via `dotnet run --project src/StageFright.App/`): run the wizard end-to-end for a GST-registered org and a non-registered org, confirming ABN blocks Next when invalid and the seeding modal appears only once seeding starts; open Settings, confirm General no longer overflows, GST/BAS tab shows the moved controls with the confirm dialog intact, and saving from either tab in either order never loses the other's change.
3. Existing scenario regression: confirm no change to `GstCalculator`/`BasSummaryReportProvider`/GL posting output for a pre-existing seeded database (Phase 1 migration only adds a nullable column).
