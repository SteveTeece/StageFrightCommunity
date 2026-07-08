# Feature Specification: GST Registration in Setup Wizard & GST/BAS Settings Tab

**Feature Branch**: `ExpandFnance`
**Created**: 2026-07-08
**Status**: Approved (design confirmed via brainstorming session)
**Input**: Add GST registration setup to the first-run Setup Wizard, and give organisations a way to change GST registration status later without the Settings page overflowing.

## Overview

GST registration (`Settings.IsGstRegistered`, `AnnualFeeGstCode`, `AttendanceFeeGstCode`) was added in the 002 finance expansion but was only ever wired into the Settings page's General tab — the first-run Setup Wizard has no awareness of GST at all, and the General tab has grown to 9+ stacked setting groups that overflow the MAUI WebView pane. This feature: (1) converts the Setup Wizard from a single long form into a 4-step wizard that collects GST registration during first-run setup, (2) adds an Australian Business Number (ABN) field — required for all organisations regardless of GST status — to the Organisation step and the General settings tab, (3) moves the existing GST toggle/code controls out of the General tab into a new dedicated "GST / BAS" tab so organisations can change registration status later, and (4) replaces the wizard's inline sample-data spinner with a proper progress modal, since seeding can take several minutes.

## User Scenarios & Testing

### User Story 1 — GST registration during first-run setup (Priority: P1)

As the person setting up StageFright Community for a new organisation, I record the organisation's ABN and (if applicable) GST registration and per-fee-type GST treatment as part of the guided setup, instead of having to find it later in Settings.

**Independent test**: Run the wizard end-to-end for a GST-registered org (valid ABN, GST on, both fee GST codes set) and for a non-registered org (valid ABN, GST off); verify the resulting `Settings` row matches what was entered in each case, and that an invalid ABN or GST-registered-without-ABN blocks completion.

**Acceptance scenarios**:
1. **Given** the wizard's Organisation step, **When** I leave ABN blank or enter fewer/more than 11 digits or a value that fails the ABN checksum, **Then** I cannot advance to the next step and see a validation message.
2. **Given** a valid ABN and organisation name, **When** I click Next, **Then** I proceed to the Fees & Renewal step, then GST Registration, then Review & Finish.
3. **Given** the GST Registration step, **When** I toggle "Organisation is registered for GST" on, **Then** Annual Fee / Attendance Fee GST-treatment dropdowns appear (each defaulting to "GST-free"); toggling off hides them again.
4. **Given** the Review & Finish step, **When** I click Finish, **Then** `SetupService.InitializeAsync` persists `OrganizationName`, `Abn`, `IsGstRegistered`, and (only if registered) the two GST codes — codes are forced to `null` if not registered, regardless of what was previously selected before toggling off.
5. **Given** "Load sample data" is checked, **When** I click Finish, **Then** a full-screen progress modal appears once seeding actually starts (not during the fast settings-creation step), showing a spinner, live progress text, and the message "Setting up your sample data — this may take a few minutes. Please don't close the app."

### User Story 2 — Changing GST registration after setup, without losing the Settings page (Priority: P2)

As a treasurer whose organisation newly registers for GST (or deregisters), I change that status from Settings without scrolling through an overflowing General tab, and my change doesn't get silently overwritten by a concurrent edit on another tab.

**Independent test**: Open Settings, confirm GST controls are no longer on the General tab; open the new "GST / BAS" tab, toggle registration, confirm the existing warning-confirm dialog still appears before the change commits; verify a cross-tab save doesn't clobber the other tab's unsaved-then-saved change.

**Acceptance scenarios**:
1. **Given** the Settings page, **When** it loads, **Then** tab order is General → GST / BAS → Event Types → Backup & Restore → plugin tabs, and the General tab no longer contains any GST controls (they moved to the new tab) but does contain the ABN field.
2. **Given** the GST / BAS tab, **When** I toggle registration, **Then** the existing confirm-dialog warning ("future income and expense postings will split out GST…" / "...GST fields will be hidden...") appears exactly as it did on the General tab today, and nothing is persisted until I click Confirm.
3. **Given** I edit the General tab (e.g. change Annual Fee) without saving, then switch to the GST / BAS tab, toggle GST on, and save there, **When** I then go back to the General tab and click Save, **Then** the GST toggle change I just saved is preserved (not overwritten by the General tab's stale in-memory copy).
4. **Given** an existing (pre-upgrade) organisation with no ABN on file, **When** it opens the General tab, **Then** it sees a non-blocking notice that its ABN hasn't been recorded, but can still save unrelated changes (e.g. theme) without being forced to supply one immediately.

## Requirements

### Functional
- **FR-111**: `Settings` gains `Abn` (`string?`, digits only). Not soft-deleted/nulled by GST toggling — it is a standing organisation-identity fact, not a GST-transaction preference. New EF Core migration adds the column (nullable, since existing installs have no value to backfill).
- **FR-112**: New `AbnValidator` (`StageFright.Core/Modules/Settings/AbnValidator.cs`) implements the official ATO weighted-modulus-89 ABN checksum algorithm over an 11-digit input.
- **FR-113**: New `AbnAttribute : ValidationAttribute` (`StageFright.Core/Modules/Settings/AbnAttribute.cs`) wraps `AbnValidator`. Empty/null values are treated as valid by the attribute itself (so it can be reused on both a strictly-required context and an optional-but-format-checked context); a non-empty value that isn't 11 digits or fails the checksum is invalid.
  - `SetupFormModel.Abn` carries both `[Required]` and `[Abn]` — the wizard cannot finish without a valid ABN.
  - `Settings.Abn` carries only `[Abn]` — an empty value passes (existing installs aren't blocked from saving), but a non-empty malformed value is still rejected.
- **FR-114**: Setup Wizard (`SetupWizard.razor`/`.razor.cs`) becomes a single component with 4 steps gated by `_currentStep`, sharing one `EditContext`/`SetupFormModel`:
  1. Organisation — Organisation Name, ABN.
  2. Fees & Renewal — Annual Fee, Attendance Fee, Membership Renewal Month.
  3. GST Registration — `IsGstRegistered` toggle; when on, Annual Fee / Attendance Fee GST-code dropdowns (same options as the Settings page). No confirm dialog (no existing postings to warn about for a brand-new org).
  4. Review & Finish — read-only summary of all entered values, "Load sample data" checkbox, Back/Finish.
  - A "Step X of 4" indicator plus a 4-segment progress bar is shown above the form.
  - **Next** (`type="button"`) calls `_editContext.Validate()` to gate advancement using the existing `DataAnnotationsValidator` infrastructure; **Finish** (`type="submit"`) is wired to the existing `OnValidSubmit="HandleValidSubmitAsync"`.
- **FR-115**: `SetupRequest` gains `Abn`, `IsGstRegistered`, `AnnualFeeGstCode`, `AttendanceFeeGstCode`. `SetupService.InitializeAsync`'s `Validate()` re-checks ABN (required + checksum) at the service boundary independent of UI validation, and forces both GST codes to `null` when `IsGstRegistered` is false before persisting.
- **FR-116**: `SettingsService.SaveAsync` rejects a non-empty-but-malformed `Abn` (`ValidationException`) but does not require `Abn` to be present — satisfies "flag, don't block" for pre-upgrade installs.
- **FR-117**: New `GstSettingsTab.razor`/`.razor.cs` (Settings page). The GST toggle, its confirm-dialog, and the two GST-code dropdowns move here verbatim from `GeneralSettingsTab` (behaviour unchanged — same confirm-before-commit flow). `GeneralSettingsTab` gains the ABN input (with a non-blocking "ABN not on file" notice when empty) and loses the GST section entirely.
- **FR-118**: `SettingsPage.razor`/`.razor.cs` adds a "GST / BAS" tab immediately after General. Tab order/index: General(0) → GST/BAS(1) → Event Types(2) → Backup & Restore(3) → plugin tabs. `DefaultTabIndex` switch, lazy-render flags, and `?tab=` query keys (`general`, `gst`, `event-types`, `backup`) updated accordingly; the reserved-key list in `specs/001-initial-mvp/spec.md` NFR-010 is updated to include `gst`.
- **FR-119**: Cross-tab save safety: both `GeneralSettingsTab.HandleSaveAsync` and `GstSettingsTab.HandleSaveAsync` re-fetch the current `Settings` row from the DB immediately before saving and copy across only the fields owned by *the other* tab (GST fields for General; everything else, including Abn, for GST/BAS) before persisting — preventing a stale in-memory copy in one tab from clobbering a concurrent save made in the other during the same page visit.
- **FR-120**: The wizard's sample-data seeding progress moves from an inline spinner/text at the bottom of the form to a full-screen overlay (fixed position, dimmed backdrop, centered card) shown only once seeding actually starts (not during the preceding settings-creation step). Message: "Setting up your sample data — this may take a few minutes. Please don't close the app," plus the existing live progress text. New CSS class added to `app.css` (distinct from `ReportViewer`'s unstyled `modal-backdrop-light`, so that component's appearance is untouched).

### Non-functional / constraints (CLAUDE.md non-negotiables)
- One class per file; `.razor` + `.razor.cs` pairs, no `@code` blocks; custom exceptions (`ValidationException`) at service boundaries; exhaustive `Should_X_When_Y` test coverage for every new/changed code path; `dotnet build` and full `dotnet test` green before considering the task complete.
- No new soft-delete concerns (`Settings` already exempt from soft-delete as a singleton config row; `Abn` is a plain field, not financial data).

## Testing Plan

- `SetupWizard` bUnit tests rewritten to drive all 4 steps: Next/Back navigation; validation blocking on missing/invalid ABN and empty org name; GST dropdowns appearing only when toggled on; Finish composing the full `SetupRequest` including ABN and GST fields; seeding modal appears only once seeding starts and not during settings creation.
- `AbnValidator` unit tests: known-valid ABN (e.g. the ATO's own published test ABN), known-invalid checksum, wrong length, non-digit characters, null/empty.
- `SetupService`/`SetupRequest` unit tests: ABN required + checksum-validated at the service boundary; GST codes forced null when unregistered.
- GST-toggle/confirm-dialog bUnit tests move from `GeneralSettingsTab`'s test file to a new `GstSettingsTab` test file; `GeneralSettingsTab` tests updated to confirm GST UI is absent and ABN UI is present (required-but-not-blocking).
- New cross-tab concurrency test: save GST tab, then save General tab from a stale in-memory copy, assert the GST change survives (and vice versa for an ABN change).
- `StageFright.Data.Tests`: migration adds the `Abn` column without breaking existing seeded/migrated rows (nullable, no backfill required).

## Success Criteria

- **SC-101**: A brand-new setup cannot complete without a valid 11-digit, checksum-valid ABN; GST registration and per-fee GST codes (when registered) are captured and persisted exactly as entered.
- **SC-102**: The Settings page's General tab no longer overflows/cuts off — GST controls live entirely on the new GST / BAS tab.
- **SC-103**: Switching between General and GST / BAS tabs and saving from either one, in either order, never loses the other tab's already-saved change.
- **SC-104**: Existing (pre-upgrade) organisations with no ABN on file can still save any Settings change; only a non-blocking notice appears until they supply one.
- **SC-105**: The sample-data seeding modal is visibly a blocking, full-screen "please wait" experience distinct from the brief settings-creation step that precedes it.

## Out of Scope
- No changes to `GstCalculator`, `BasSummaryReportProvider`, or GL posting logic — this feature is purely data-entry (wizard + Settings UI) plus the new `Abn` field.
- No ABN formatting/display masking (e.g. "XX XXX XXX XXX" grouping) — stored and entered as a plain digit string.
- No retroactive enforcement that forces existing installs to supply an ABN immediately (see FR-116).
