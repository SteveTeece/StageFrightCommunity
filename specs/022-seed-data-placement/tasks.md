# Tasks: Move Seed Data Checkbox to Organisation Settings

**Input**: Design documents from `specs/022-seed-data-placement/` (plan.md, research.md, data-model.md, contracts/ui-contract.md)
**Tests**: Constitution §11 (Non-Negotiable Coverage Rule) requires exhaustive path coverage before merge — every story below includes a Tests block, written to fail first.

## Phase 1: User Story 1 - Decide on sample data from the first tab (Priority: P1) 🎯 MVP

**Goal**: The "Load sample data" checkbox lives on the Organisation Settings tab; the Review tab shows the choice read-only.

**Independent Test**: Launch the wizard (debug build) with no Settings record present, confirm the checkbox appears on Organisation Settings and not as an interactive control on Review, and confirm Review still shows the choice read-only.

### Tests

**Wave 1 — independent (different files):**
- [x] **T001** [P] [US1] Write `SampleDataTabTests.cs`: renders nothing when `DebugSeederAvailable=false`; renders `#seedData` when `true`; checking it invokes `SeedWithTestDataChanged(true)` · `tests/StageFright.UI.Tests/Pages/Setup/Tabs/SampleDataTabTests.cs`
- [x] **T002** [P] [US1] Update `ReviewTabTests.cs`: remove `SeedDataCheckbox_OnlyShown_WhenDebugSeederAvailable` and `CheckingSeedData_InvokesSeedWithTestDataChanged`; add a test asserting a read-only "Load sample data: Yes/No" row renders when `DebugSeederAvailable` is true, and one asserting no `#seedData` input renders at all · `tests/StageFright.UI.Tests/Pages/Setup/Tabs/ReviewTabTests.cs`

### Implementation

**Wave 2 — independent (different files):**
- [x] **T003** [P] [US1] Create `SampleDataTab.razor` + `SampleDataTab.razor.cs` — move the existing `@if (DebugSeederAvailable) { ... }` checkbox block from `ReviewTab.razor` unchanged, with `DebugSeederAvailable`/`SeedWithTestData`/`SeedWithTestDataChanged` parameters · `src/StageFright.UI/Pages/Setup/Tabs/SampleDataTab.razor` + `.razor.cs`
- [x] **T004** [P] [US1] Remove the interactive checkbox block and `OnSeedWithTestDataChangedAsync` from `ReviewTab`; add a read-only `<dt>Load sample data</dt><dd>Yes|No</dd>` row to the existing `<dl>` summary (guarded by `DebugSeederAvailable`); drop the `SeedWithTestDataChanged` parameter · `src/StageFright.UI/Pages/Setup/Tabs/ReviewTab.razor` + `.razor.cs`

**⟶ Wait for Wave 2 to finish, then:**

**Wave 3 — independent (different files):**
- [x] **T005** [P] [US1] Wire `<SampleDataTab>` into `SetupWizard.razor`'s Organisation Settings tab content (after `SalesTaxTab`), passing `DebugSeederAvailable="_debugSeeder is not null"`, `SeedWithTestData="_seedWithTestData"`, `SeedWithTestDataChanged="HandleSeedWithTestDataChanged"`; remove `SeedWithTestDataChanged` from the Review `<Tab>`'s markup · `src/StageFright.UI/Pages/Setup/SetupWizard.razor`
- [x] **T006** [P] [US1] Add `private void HandleSeedWithTestDataChanged(bool value)` to `SetupWizard.razor.cs`, setting `_seedWithTestData = value` (queue-clearing logic added in Phase 3/T013) · `src/StageFright.UI/Pages/Setup/SetupWizard.razor.cs`

**Checkpoint**: User Story 1 is independently functional and testable — the checkbox is on Organisation Settings, Review is read-only, nothing else in the wizard behaves differently yet.

---

## Phase 2: User Story 2 - Selecting sample data bypasses manual account, balance, and committee entry (Priority: P1)

**Goal**: Checking "Load sample data" disables the Chart of Accounts, Opening Balances, and Committee tabs, and Next skips straight from Organisation Settings to Review.

**Independent Test**: Check "Load sample data", confirm the three tab headers become unavailable, Next from Organisation Settings lands on Review, and Finish succeeds with account/balance/committee data supplied entirely by the seeder.

### Tests

**Wave 1 — independent (different files):**
- [x] **T007** [P] [US2] Add `SetupWizardTests.cs` coverage: the three `<Tab>`s render disabled once `#seedData` is checked; clicking a disabled tab header does not change the visible content; clicking Next from Organisation Settings with the box checked shows the Review tab next, not Chart of Accounts · `tests/StageFright.UI.Tests/Pages/Setup/SetupWizardTests.cs`
- [x] **T008** [P] [US2] Add `SetupWizardNoSeederTests.cs` coverage: with no debug seeder registered, the three tabs are never disabled and every tab behaves exactly as today (FR-009) · `tests/StageFright.UI.Tests/Pages/Setup/SetupWizardNoSeederTests.cs`

### Implementation

**Wave 2 — independent (different files):**
- [x] **T009** [P] [US2] Add `Disabled="@_seedWithTestData"` to the Chart of Accounts, Opening Balances, and Committee `<Tab>` elements · `src/StageFright.UI/Pages/Setup/SetupWizard.razor`
- [x] **T010** [P] [US2] Add `private bool IsTabBypassed(int index) => _seedWithTestData && index is >= 1 and <= 3`; guard `SetActiveTab` to no-op when `IsTabBypassed(index)`; make `HandleNextAsync` advance `nextIndex` past every bypassed index · `src/StageFright.UI/Pages/Setup/SetupWizard.razor.cs`

**⟶ Wait for Wave 2 to finish, then:**

**Wave 3 — single task:**
- [x] **T011** [US2] Verify FR-008 against the running seeder (targeted check or integration test): confirm `DebugDataSeeder` creates non-system chart-of-accounts entries, posts a full opening-balance position, and elects a full committee across its seeded AGMs; extend the seeder only if a genuine gap is found (none expected per research.md) · `src/StageFright.App/Seeding/DebugDataSeeder.cs`

**Checkpoint**: User Story 2 is independently functional and testable — selecting sample data makes the three tabs unusable and Next reaches Review directly, with Finish producing a complete sample setup.

---

## Phase 3: User Story 3 - Changing the sample-data choice keeps the wizard consistent (Priority: P2)

**Goal**: Checking the box discards anything already queued on the three tabs; unchecking it makes them available again, empty; Finish's existing balance-or-sample-data gate still holds.

**Independent Test**: Queue an account, a balance, and a committee title; check "Load sample data" and confirm all three are discarded; uncheck it and confirm the tabs are available again, empty, and Finish is rejected without an opening balance.

### Tests

**Wave 1 — single task:**
- [x] **T012** [US3] Add `SetupWizardTests.cs` coverage: a queued account/balance/committee title is discarded the moment the checkbox is checked; the three tabs become enabled (not `Disabled`) again after unchecking, each starting empty; Finish is rejected without a posted balance after unchecking (same as today) · `tests/StageFright.UI.Tests/Pages/Setup/SetupWizardTests.cs`

### Implementation

**Wave 2 — single task:**
- [x] **T013** [US3] Extend `HandleSeedWithTestDataChanged(bool value)`: when `value` is `true`, clear `_queuedAccounts`, `_queuedOpeningBalances`, and `_queuedCommitteeTitles` · `src/StageFright.UI/Pages/Setup/SetupWizard.razor.cs`

**Checkpoint**: User Story 3 is independently functional and testable — toggling the checkbox in either direction never leaves stale queued data or an unreachable tab.

---

## Final Phase: Polish

**Wave 1 — independent (different files):**
- [x] **T014** [P] Update `specs/017-setup-wizard-tabs/spec.md` — tab descriptions, FR-025, and the "ADDED Requirements" tab-strip description, to reflect the checkbox's new home on Organisation Settings and its new bypass effect · `specs/017-setup-wizard-tabs/spec.md`
- [x] **T015** [P] Update `capabilities/app-host/spec.md`'s "Optional sample-data seeding" section — relocate the checkbox reference and add scenario(s) for the three-tab bypass · `capabilities/app-host/spec.md`

**⟶ Wait for Wave 1 to finish, then:**

**Wave 2 — single task:**
- [x] **T016** Full rebuild and full test suite run (`dotnet build -t:Rebuild`, then `dotnet test` without `--no-build`) per CLAUDE.md's Build & Test Verification rule; confirm no new warnings and every test green (treat the two documented "fee"-substring flaky tests per the known-flake note, not as regressions unless the diff actually touches Events/ParticipationGrid/EventForm) · repo-wide

**⟶ Wait for Wave 2 to finish, then:**

**Wave 3 — single task:**
- [x] **T017** Cross-check the green Phase 1–3 test suite against spec.md's SC-001–SC-004 (checkbox location, tab-open prevention, complete sample setup with zero manual entry, clean toggle-back-and-forth) and record the result · `specs/022-seed-data-placement/spec.md`

## Dependencies & Execution Order

- **Phase 1 (US1)** has no dependency on the other phases and must land first — it creates `SampleDataTab` and `HandleSeedWithTestDataChanged`, which Phases 2 and 3 both extend. Wave 1 (tests) → Wave 2 (`SampleDataTab` + `ReviewTab`, parallel) → Wave 3 (`SetupWizard` wiring, parallel).
- **Phase 2 (US2)** depends on Phase 1's `SetupWizard` wiring (T005/T006) existing. Wave 1 (tests) → Wave 2 (`Disabled` binding + tab-skip guard, parallel) → Wave 3 (FR-008 verification, independent of Wave 2's code but sequenced last since it's a verification step, not a blocker for it).
- **Phase 3 (US3)** depends on Phase 2's `HandleSeedWithTestDataChanged` (T006) existing to extend. Wave 1 (test) → Wave 2 (single-method extension).
- **Final Phase (Polish)** depends on all three story phases being complete. Wave 1 (doc updates, parallel) → Wave 2 (build/test gate) → Wave 3 (success-criteria cross-check).
- Every task within a wave touches a different file from every other task in that wave; tasks across waves are sequenced because a later wave's task edits a file (or depends on a method) a prior wave's task just created.
