# Tasks: GST Registration in Setup Wizard & GST/BAS Settings Tab

**Input**: Design documents from `/specs/003-gst-registration-setup/` (plan.md, spec.md)

**Tests**: Included — CLAUDE.md mandates exhaustive `Should_X_When_Y` coverage before merge; each phase ends green (`dotnet build` + full `dotnet test`).

**Organization**: Tasks grouped by user story. Foundational (Abn field/validator/shared masked-input component) blocks both stories. User Story 1 (wizard) and User Story 2 (Settings tab split) are independent of each other once Foundational is done.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Setup

**Purpose**: Baseline verification — no scaffolding needed (existing solution).

- [ ] T001 Verify baseline: `dotnet build` and full `dotnet test` green on branch `003-gst-registration-setup` before any change

---

## Phase 2: Foundational

**Purpose**: The `Abn` field, its validation, and the shared masked-input component — required by both the wizard (US1) and the Settings tabs (US2). No user-story work can begin until this phase is complete.

- [ ] T002 Add nullable `Abn` (`string?`) property to `Settings` entity in src/StageFright.Core/Entities/Settings.cs, placed near `OrganizationName`; not touched by GST-toggle logic
- [ ] T003 [P] Implement `AbnValidator` (ATO weighted-modulus-89 checksum; input must be exactly 11 digit characters) in src/StageFright.Core/Modules/Settings/AbnValidator.cs
- [ ] T004 Implement `AbnAttribute : ValidationAttribute` wrapping `AbnValidator` (null/empty passes; non-empty malformed fails) in src/StageFright.Core/Modules/Settings/AbnAttribute.cs (depends on T003); apply `[Abn]` to `Settings.Abn` (T002)
- [ ] T005 Add migration `AddAbnToSettings` (`dotnet ef migrations add AddAbnToSettings --project src/StageFright.Data/ --startup-project src/StageFright.App/`): nullable `Abn` column, no backfill (depends on T002)
- [ ] T006 [P] Create shared `AbnInput` component — `InputText` subclass overriding `FormatValueAsString`/`TryParseValueFromString` to display the "XX XXX XXX XXX" grouping while binding/persisting the plain 11-digit value, no custom JS — in src/StageFright.UI/Shared/AbnInput.razor(+.razor.cs)

### Tests

- [ ] T007 [P] `AbnValidator` unit tests: ATO's published test ABN (51 824 753 556) valid; checksum-broken variant invalid; wrong length invalid; non-digit characters invalid; null/empty invalid — in tests/StageFright.Core.Tests/
- [ ] T008 [P] `AbnAttribute` unit tests: null/empty passes; valid ABN passes; malformed non-empty fails — in tests/StageFright.Core.Tests/
- [ ] T009 [P] `AbnInput` bUnit tests: typed digits render grouped as "XX XXX XXX XXX"; bound value has no spaces; pasting a pre-formatted value parses to the correct 11-digit value; input beyond 11 digits is ignored; `ValidationMessage` still fires through inherited `InputText` wiring — in tests/StageFright.UI.Tests/
- [ ] T010 [P] Migration integration test: existing seeded/migrated `Settings` rows survive the `AddAbnToSettings` migration with `Abn = null` — in tests/StageFright.Data.Tests/
- [ ] T011 Verify checkpoint: `dotnet build` + full `dotnet test` green

**Checkpoint**: Foundation ready — User Story 1 and User Story 2 can now proceed independently (and in parallel).

---

## Phase 3: User Story 1 — GST registration during first-run setup (Priority: P1) 🎯 MVP

**Goal**: The Setup Wizard becomes a 4-step flow (Organisation/ABN → Fees & Renewal → GST Registration → Review & Finish) that captures ABN, GST registration, and per-fee GST codes at first run, and shows a full-screen "please wait" modal only while sample data is actually seeding.

**Independent Test**: Run the wizard end-to-end for a GST-registered org (valid ABN, GST on, both fee GST codes set) and a non-registered org (valid ABN, GST off); verify the resulting `Settings` row matches what was entered in each case, and that an invalid/missing ABN blocks completion.

### Implementation for User Story 1

- [ ] T012 [US1] Add `Abn` (`[Required]` + `[Abn]`), `IsGstRegistered`, `AnnualFeeGstCode`, `AttendanceFeeGstCode` to src/StageFright.UI/Pages/Setup/SetupFormModel.cs (depends on T004)
- [ ] T013 [US1] Add `Abn`, `IsGstRegistered`, `AnnualFeeGstCode`, `AttendanceFeeGstCode` to src/StageFright.Core/Modules/Settings/SetupRequest.cs
- [ ] T014 [US1] Update `SetupService.Validate`/`InitializeAsync` in src/StageFright.Core/Modules/Settings/SetupService.cs: required + checksum ABN check at the service boundary; force GST codes to `null` when `IsGstRegistered` is false; persist `Abn`/`IsGstRegistered`/GST codes onto the new `Settings` row (depends on T003, T013)
- [ ] T015 [US1] Restructure src/StageFright.UI/Pages/Setup/SetupWizard.razor into 4 steps sharing one `EditContext` (Organisation incl. `<AbnInput>`, Fees & Renewal, GST Registration, Review & Finish), with a "Step X of 4" indicator/progress bar (depends on T006, T012)
- [ ] T016 [US1] Update src/StageFright.UI/Pages/Setup/SetupWizard.razor.cs: `_currentStep`/`_editContext` state, `HandleNext`/`HandleBack` (Next validates via `_editContext.Validate()`), extend the `SetupRequest` construction in `HandleValidSubmitAsync` with the new fields, wrap only the `DebugSeeder.SeedAsync` call in `_seedingInProgress` (depends on T014, T015)
- [ ] T017 [US1] Add `.setup-seeding-overlay` full-screen modal styles (fixed position, dimmed backdrop, centered card) to src/StageFright.App/wwwroot/app.css, distinct from `ReportViewer`'s unstyled `.modal-backdrop-light`

### Tests for User Story 1

- [ ] T018 [P] [US1] `SetupService`/`SetupRequest` unit tests: missing/invalid ABN blocks `InitializeAsync` with `ValidationException`; GST codes forced null when `IsGstRegistered` is false regardless of what was passed in — in tests/StageFright.Core.Tests/
- [ ] T019 [US1] Rewrite `SetupWizard` bUnit tests: Next/Back navigation across all 4 steps; Next blocked on missing/invalid ABN or empty org name; GST dropdowns appear only when toggled on (and codes clear when toggled off); Finish composes the full `SetupRequest` including ABN and GST fields; seeding overlay appears only once seeding starts and only when "Load sample data" is checked — in tests/StageFright.UI.Tests/
- [ ] T020 [US1] Verify checkpoint: `dotnet build` + full `dotnet test` green; manual E2E wizard run for both a GST-registered and a non-registered org (`dotnet run --project src/StageFright.App/`)

**Checkpoint**: User Story 1 fully functional and testable independently.

---

## Phase 4: User Story 2 — Changing GST registration after setup, without losing the Settings page (Priority: P2)

**Goal**: The GST toggle/code controls move off the General tab onto a new "GST / BAS" tab; the General tab gains the ABN field; saving from either tab never clobbers a concurrent save made on the other.

**Independent Test**: Open Settings, confirm GST controls are no longer on the General tab; open the new "GST / BAS" tab, toggle registration, confirm the existing confirm-dialog still appears before the change commits; verify a cross-tab save doesn't lose the other tab's already-saved change.

### Implementation for User Story 2

- [ ] T021 [US2] Add malformed-but-not-missing `Abn` rejection to `SettingsService.SaveAsync` in src/StageFright.Core/Modules/Settings/SettingsService.cs (depends on T003)
- [ ] T022 [US2] Create src/StageFright.UI/Pages/Settings/GstSettingsTab.razor(+.razor.cs): move the GST toggle, its confirm-dialog, and the two GST-code dropdowns verbatim from `GeneralSettingsTab`; `HandleSaveAsync` re-fetches the current `Settings` row and merges in every *non*-GST field before saving (depends on T006)
- [ ] T023 [US2] Update src/StageFright.UI/Pages/Settings/GeneralSettingsTab.razor(+.razor.cs): remove the GST block and its handlers; add `<AbnInput>` with a non-blocking "ABN not on file" notice when empty; `HandleSaveAsync` re-fetches the current `Settings` row and merges in `IsGstRegistered`/`AnnualFeeGstCode`/`AttendanceFeeGstCode` before saving (depends on T006, T022)
- [ ] T024 [US2] Update src/StageFright.UI/Pages/Settings/SettingsPage.razor(+.razor.cs): insert a "GST / BAS" tab immediately after General; renumber `DefaultTabIndex`, lazy-render flags, and `?tab=` keys (`general`, `gst`, `event-types`, `backup`) (depends on T022)
- [ ] T025 [US2] Update `specs/001-initial-mvp/spec.md` NFR-010 reserved `?tab=` table to add the `gst` row

### Tests for User Story 2

- [ ] T026 [P] [US2] `SettingsService.SaveAsync` unit tests: empty `Abn` saves successfully; malformed non-empty `Abn` throws `ValidationException`; valid `Abn` saves successfully — in tests/StageFright.Core.Tests/
- [ ] T027 [US2] Move GST-toggle/confirm-dialog bUnit tests from `GeneralSettingsTab`'s test file to a new `GstSettingsTab` test file — in tests/StageFright.UI.Tests/
- [ ] T028 [US2] Update `GeneralSettingsTab` bUnit tests: GST UI is absent; ABN field is present with the "not on file" notice showing/hiding correctly; save succeeds with an empty `Abn` — in tests/StageFright.UI.Tests/
- [ ] T029 [US2] Update `SettingsPage` bUnit tests: tab order/index includes GST/BAS; `?tab=gst` deep-links correctly; existing `?tab=event-types`/`?tab=backup` deep-links updated to the new indices — in tests/StageFright.UI.Tests/
- [ ] T030 [US2] New cross-tab concurrency tests (e.g. `SettingsCrossTabSaveTests`): save GST tab then save General tab from a stale in-memory copy — GST change survives; and the symmetric case for an ABN change — in tests/StageFright.UI.Tests/ or tests/StageFright.Integration.Tests/
- [ ] T031 [US2] Verify checkpoint: `dotnet build` + full `dotnet test` green; manual E2E — General tab no longer overflows, GST/BAS tab preserves the confirm dialog, cross-tab saves in either order never lose data

**Checkpoint**: User Stories 1 and 2 both independently functional.

---

## Phase 5: Polish & Cross-Cutting Concerns

- [ ] T032 Full regression pass: `dotnet build` + full `dotnet test` (no `--no-build`) across all 5 test projects
- [ ] T033 Manual E2E per spec.md's Testing Plan (`dotnet run --project src/StageFright.App/`): exercise both user stories together end-to-end — fresh install via the GST-registered wizard path, then flip GST off via the GST/BAS tab, confirming ABN persists throughout

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies — can start immediately.
- **Foundational (Phase 2)**: depends on Setup — BLOCKS both user stories.
- **User Story 1 (Phase 3)** and **User Story 2 (Phase 4)**: both depend only on Foundational; independent of each other and can proceed in parallel.
- **Polish (Phase 5)**: depends on both user stories being complete.

### Within Each Phase

- T002 before T005 (migration needs the entity property); T003 before T004 (attribute wraps validator); T006 has no dependency on T002-T005.
- Within US1: T012/T013 before T014; T006 + T012 before T015; T014 + T015 before T016.
- Within US2: T003 before T021; T006 before T022; T006 + T022 before T023; T022 before T024.

### Parallel Opportunities

- T003, T006, and T007-T010 (Foundational) can run in parallel once T002 lands.
- Once Foundational (Phase 2) completes, all of Phase 3 (US1) and Phase 4 (US2) can be worked in parallel.
- T018 [P] and T026 [P] can run parallel to their sibling story's UI tasks.

---

## Parallel Example: Foundational Phase

```bash
Task: "Implement AbnValidator in src/StageFright.Core/Modules/Settings/AbnValidator.cs"
Task: "Create shared AbnInput component in src/StageFright.UI/Shared/AbnInput.razor(+.razor.cs)"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL — blocks both stories)
3. Complete Phase 3: User Story 1 (wizard)
4. **STOP and VALIDATE**: run the wizard end-to-end for both a GST-registered and a non-registered org
5. Deploy/demo if ready — a first-run install already captures ABN + GST correctly even before User Story 2 lands

### Incremental Delivery

1. Setup + Foundational → foundation ready
2. User Story 1 → validate independently → demo (MVP)
3. User Story 2 → validate independently → demo
4. Polish

---

## Notes

- `[P]` tasks = different files, no dependencies.
- `[Story]` label maps a task to its user story for traceability.
- CLAUDE.md requires exhaustive `Should_X_When_Y` coverage regardless of TDD ordering; write tests alongside or ahead of implementation as convenient.
- Commit after each task or logical group.
- Stop at either story's checkpoint to validate independently.
- Avoid: vague tasks, same-file conflicts between parallel tasks, cross-story dependencies that break independence.
