---

description: "Task list for Outstanding Fee Selection on the Record Member Payment Form"
---

# Tasks: Outstanding Fee Selection on the Record Member Payment Form

**Input**: Design documents from `/specs/008-outstanding-fee-payment-allocation/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/finance-payment-contracts.md, quickstart.md

**Tests**: Included — CLAUDE.md's "Exhaustive code-path test coverage" rule and plan.md's Constitution Check (§11 Testing Standards) both require unit, bUnit, and integration tests for every new/changed code path in this feature.

**Organization**: Tasks are grouped by user story (both P1) to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2)
- Every task lists its exact file path(s)

## Path Conventions

Single MAUI Blazor solution (see CLAUDE.md Project Layout): `src/StageFright.Core/`, `src/StageFright.UI/`, `tests/StageFright.Core.Tests/`, `tests/StageFright.UI.Tests/`, `tests/StageFright.Integration.Tests/`.

---

## Phase 1: Setup

**Purpose**: Confirm a clean baseline before touching any Finance module code (CLAUDE.md's Build & Test Verification rule).

- [ ] T001 Run `dotnet build` and `dotnet test` from the repo root and confirm both are green before making any changes, per CLAUDE.md's Build & Test Verification rule

**Checkpoint**: Baseline confirmed green — safe to start Foundational work.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The one shared read-model type both user stories depend on.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [ ] T002 Create the `OutstandingFee` read-model DTO (`FeeId: Guid`, `FeeType: FeeType`, `FeeDate: DateTime`, `DueDate: DateTime`, `RemainingAmount: decimal`) in `src/StageFright.Core/Modules/Finance/OutstandingFee.cs`, per data-model.md's "New: `OutstandingFee`" section

**Checkpoint**: Foundation ready — User Story 1 and User Story 2 implementation can now begin.

---

## Phase 3: User Story 1 - Select which outstanding fees a payment covers (Priority: P1) 🎯 MVP

**Goal**: The Record Member Payment form shows a grid of the selected member's outstanding fees (type, fee date, due date, true remaining amount owed), with a leftmost checkbox column and a select-all header control. Checking/unchecking fees recalculates the Amount field to the sum of checked fees' remaining amounts.

**Independent Test**: Open the payment form for a member with multiple outstanding fees (including one partially paid), confirm each fee is listed with its remaining amount owed, check one or more fees, and confirm the Amount field reflects their combined total; verify select-all and uncheck behavior.

### Tests for User Story 1

> Write these tests FIRST, ensure they FAIL before implementation (per CLAUDE.md's exhaustive-coverage rule and the `Should_[ExpectedBehavior]_When_[Condition]` naming convention).

- [ ] T003 [P] [US1] Add `MemberBalanceServiceTests` cases for `GetOutstandingFeesAsync` in `tests/StageFright.Core.Tests/Modules/Finance/MemberBalanceServiceTests.cs`: returns each fee's true remaining amount (`Amount − Σ CreditAmount` of linked `MemberReceivable` GL rows), excludes fully-settled fees (`RemainingAmount <= 0`), returns an empty list for a member with no outstanding fees, and orders results `FeeDate ASC, CreatedAt ASC, Id ASC`
- [ ] T006 [P] [US1] Add `OutstandingFeeSelectionGridTests` in `tests/StageFright.UI.Tests/Pages/Finance/OutstandingFeeSelectionGridTests.cs` covering: initial render with all rows unchecked, per-row checkbox toggle raises `SelectionChanged` with the new checked-sum, header select-all checks/unchecks every row and raises `SelectionChanged` accordingly, `GetSelectedFeeIds()` returns exactly the checked fee IDs, empty-state message renders when `Fees` is empty, and checkboxes are disabled when `ReadOnly="true"`

### Implementation for User Story 1

- [ ] T004 [US1] Add `Task<IReadOnlyList<OutstandingFee>> GetOutstandingFeesAsync(Guid memberId, CancellationToken ct = default)` to `IMemberBalanceService` in `src/StageFright.Core/Contracts/IMemberBalanceService.cs` (depends on T002)
- [ ] T005 [US1] Implement `GetOutstandingFeesAsync` on `MemberBalanceService` in `src/StageFright.Core/Modules/Finance/MemberBalanceService.cs`: reuse the existing per-fee remaining-owed formula from `PaymentService.RecordAsync`'s FIFO loop (`fee.Amount − Σ CreditAmount` via `IGLRepository.GetByFeeAsync`), filter to `RemainingAmount > 0`, order `FeeDate ASC, CreatedAt ASC, Id ASC`, translate any persistence failure to the module's existing custom exception type (depends on T004; makes T003 pass)
- [ ] T007 [P] [US1] Create `OutstandingFeeSelectionGrid.razor` in `src/StageFright.UI/Pages/Finance/OutstandingFeeSelectionGrid.razor`: `RadzenDataGrid<OutstandingFeeRow>` with `AllowSorting="true" AllowPaging="true" PageSize="15" class="rz-shadow-0"`, leftmost checkbox column with a `HeaderTemplate` select-all checkbox (matching `AttendanceGrid`'s convention — see `src/StageFright.UI/Pages/Rehearsals/AttendanceGrid.razor`), columns for Fee Type/Fee Date/Due Date/Remaining Amount, and an empty-state message shown when `Fees` is empty (FR-013)
- [ ] T008 [US1] Create `OutstandingFeeSelectionGrid.razor.cs` in `src/StageFright.UI/Pages/Finance/OutstandingFeeSelectionGrid.razor.cs`: `[Parameter] IReadOnlyList<OutstandingFee> Fees`, `[Parameter] bool ReadOnly`, `[Parameter] EventCallback<decimal> SelectionChanged`, a private nested `OutstandingFeeRow { OutstandingFee Fee; bool Selected; }` view-model list built from `Fees` in `OnParametersSet`, a `ToggleSelectAll(bool)` method (mirrors `AttendanceGrid.ToggleSelectAll`), a per-row toggle handler that both rows report through, and a public `GetSelectedFeeIds() : IReadOnlyList<Guid>` method; every toggle path raises `SelectionChanged` with the current sum of checked rows' `RemainingAmount` (depends on T002, T007; makes T006 pass)
- [ ] T009 [US1] Add `PaymentFormTests` cases in `tests/StageFright.UI.Tests/Pages/Finance/PaymentFormTests.cs`: the outstanding-fees grid renders for a member with outstanding fees, the Amount field updates when the grid's `SelectionChanged` callback fires, and the empty-state/disabled-Save path renders for a member with zero outstanding fees (FR-013)
- [ ] T010 [US1] Embed `<OutstandingFeeSelectionGrid>` in `src/StageFright.UI/Pages/Finance/PaymentForm.razor`, passing the loaded fees and wiring `SelectionChanged` to the Amount field; disable Save when the member has zero outstanding fees (FR-013) (depends on T007)
- [ ] T011 [US1] In `src/StageFright.UI/Pages/Finance/PaymentForm.razor.cs`, load the member's outstanding fees via `IMemberBalanceService.GetOutstandingFeesAsync(MemberId)` inside `OnInitializedAsync` (inject `IMemberBalanceService`), store the result for the grid, and add a `SelectionChanged` handler that sets `_form.Amount` to the callback's value (depends on T005, T008; makes T009 pass)

**Checkpoint**: User Story 1 is fully functional and independently testable — grid displays outstanding fees with correct remaining amounts, checkbox selection (including select-all) drives the Amount field.

---

## Phase 4: User Story 2 - Record a full or partial payment against selected fees (Priority: P1)

**Goal**: Saving the payment allocates the entered Amount to exactly the checked fees, oldest-first, fully settling each before moving to the next; saving is blocked when Amount exceeds the checked total or when no fee is checked; the grid becomes read-only after a successful save; callers that don't supply a selection keep today's FIFO-across-all-fees behavior unchanged.

**Independent Test**: Check two fees, submit with an amount equal to their combined total, and confirm both fees no longer appear as outstanding. Repeat with a smaller amount and confirm only the older fee is (fully or partially) settled. Confirm existing FIFO-all-fees tests (no selection supplied) still pass unmodified.

### Tests for User Story 2

> Write these tests FIRST, ensure they FAIL before implementation.

- [ ] T012 [P] [US2] Add `PaymentServiceTests` cases in `tests/StageFright.Core.Tests/Modules/Finance/PaymentServiceTests.cs` for `RecordAsync` with `SelectedFeeIds`: full allocation across two checked fees when Amount equals their combined remaining total, oldest-first partial allocation when Amount is less than the combined total (older fee fully settled, next fee partially settled, remaining checked fees untouched), unchecked fees receive zero allocation even if chronologically next, `ValidationException` when `SelectedFeeIds` is an empty (non-null) list with `Amount > 0` (FR-007), `ValidationException` when `Amount` exceeds the selected fees' combined remaining total (FR-006), an unrecognized fee ID in `SelectedFeeIds` contributes nothing and does not throw, and `SelectedFeeIds == null` reproduces the existing unfiltered FIFO behavior byte-for-byte (regression case, FR-012)
- [ ] T015 [P] [US2] Add `V5_PaymentsTests` integration scenarios in `tests/StageFright.Integration.Tests/Scenarios/V5_PaymentsTests.cs`, against a real SQLite-backed `DbContext`: end-to-end selected-fee full/partial allocation with GL `Transaction` rows traceable (`FeeId`) only to the checked fees (SC-004), and confirm the pre-existing FIFO/overpayment-credit cases (no `SelectedFeeIds`) remain unmodified and green (SC-005)

### Implementation for User Story 2

- [ ] T013 [US2] Add `public IReadOnlyList<Guid>? SelectedFeeIds { get; set; }` to `RecordPaymentRequest` in `src/StageFright.Core/Modules/Finance/RecordPaymentRequest.cs`, per contracts/finance-payment-contracts.md
- [ ] T014 [US2] Amend `RecordAsync` in `src/StageFright.Core/Modules/Finance/PaymentService.cs`: after the existing `Amount <= 0m` check and after loading `fees` via `IFeeRepository.GetUnpaidOrderedFifoAsync`, when `request.SelectedFeeIds is not null`: throw `ValidationException("At least one outstanding fee must be selected.", nameof(Payment), nameof(RecordAsync))` if `Count == 0`; otherwise compute the selected fees' combined remaining-owed total (same formula as `GetOutstandingFeesAsync`) and throw `ValidationException("Amount exceeds the selected fees' remaining total.", nameof(Payment), nameof(RecordAsync))` if `request.Amount` exceeds it; then filter `fees` to `f => selectedSet.Contains(f.Id)` before the existing FIFO allocation loop runs, unchanged otherwise. Leave the `SelectedFeeIds == null` path byte-for-byte identical to today (depends on T013; makes T012 pass)
- [ ] T016 [US2] Add `PaymentFormTests` cases in `tests/StageFright.UI.Tests/Pages/Finance/PaymentFormTests.cs`: Save is blocked with a validation message when Amount > 0 and no fee is checked (FR-007), Save is blocked with a validation message when Amount typed manually exceeds the checked total (FR-006), and after a successful save the outstanding-fees grid's checkboxes become disabled (FR-010)
- [ ] T017 [US2] In `src/StageFright.UI/Pages/Finance/PaymentForm.razor.cs`, update `SaveAsync` to: read the grid's checked fee IDs via `GetSelectedFeeIds()` and populate `RecordPaymentRequest.SelectedFeeIds`; add pre-save validation mirroring FR-006/FR-007 (empty selection with `Amount > 0`; Amount exceeding the checked total) so the UI blocks before calling the service; and set the grid's `ReadOnly` flag to `true` once `_saved` becomes `true`, consistent with the rest of the form's existing post-save disabled-field behavior (FR-010) (depends on T011, T014; makes T016 pass)
- [ ] T018 [US2] Update `src/StageFright.UI/Pages/Finance/PaymentForm.razor` to bind `OutstandingFeeSelectionGrid`'s `ReadOnly` parameter to the form's post-save state (depends on T010, T017)

**Checkpoint**: User Story 2 is fully functional — saving allocates exactly to the checked fees, oldest-first, with both UI and service-layer validation, and every existing no-selection caller is unaffected.

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Final verification across both stories.

- [ ] T019 Run `dotnet build` and the full `dotnet test` suite (without `--no-build`) from the repo root; confirm all suites are green, including the pre-existing `V5_PaymentsTests` FIFO/overpayment cases (SC-005), per CLAUDE.md's Build & Test Verification rule
- [ ] T020 Execute the manual validation steps in `specs/008-outstanding-fee-payment-allocation/quickstart.md` sections 2–5 (grid display and Amount auto-calc, save allocation, blocking rules, regression check) against a running `dotnet run --project src/StageFright.App/` instance

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — run first.
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS both user stories.
- **User Story 1 (Phase 3)**: Depends on Foundational (T002). No dependency on User Story 2.
- **User Story 2 (Phase 4)**: Depends on Foundational (T002). Its UI wiring tasks (T017, T018) depend on User Story 1's UI tasks (T011, T010) because both stories share `PaymentForm.razor`/`.razor.cs`; its service-layer tasks (T012–T014) have no dependency on User Story 1 and can proceed in parallel with Phase 3.
- **Polish (Phase 5)**: Depends on both user stories being complete.

### User Story Dependencies

- **User Story 1 (P1)**: Independently testable once Foundational is done — grid display and Amount auto-calc need nothing from User Story 2.
- **User Story 2 (P1)**: Independently testable at the service layer (T012–T014) once Foundational is done. Its UI half (T016–T018) builds on User Story 1's UI wiring since both touch `PaymentForm`, but the allocation/validation logic itself (`PaymentService.RecordAsync`) has no code dependency on the grid component.

### Within Each User Story

- Tests written and failing before implementation.
- DTO/contract changes before service implementation.
- Service implementation before UI wiring that calls it.
- Story complete before moving to Polish.

### Parallel Opportunities

- T003 and T006 (different test files, no shared dependency) can run in parallel.
- T007 (new grid markup file) can start in parallel with T003/T006 once T002 is done.
- T012 and T015 (different test files) can run in parallel.
- Phase 4's service-layer tasks (T012–T014) can proceed in parallel with Phase 3's UI tasks (T006–T011) once Foundational is done, since they touch disjoint files (`PaymentService.cs`/`RecordPaymentRequest.cs` vs. `OutstandingFeeSelectionGrid.*`) — only the final UI-wiring tasks (T017, T018) need both halves finished.

---

## Parallel Example: Foundational → both stories

```bash
# After T002 (OutstandingFee DTO) completes:
Task: "Add MemberBalanceServiceTests cases for GetOutstandingFeesAsync in tests/StageFright.Core.Tests/Modules/Finance/MemberBalanceServiceTests.cs"   # T003
Task: "Add OutstandingFeeSelectionGridTests in tests/StageFright.UI.Tests/Pages/Finance/OutstandingFeeSelectionGridTests.cs"                            # T006
Task: "Add PaymentServiceTests cases for SelectedFeeIds allocation in tests/StageFright.Core.Tests/Modules/Finance/PaymentServiceTests.cs"              # T012
Task: "Add V5_PaymentsTests integration scenarios in tests/StageFright.Integration.Tests/Scenarios/V5_PaymentsTests.cs"                                  # T015
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001).
2. Complete Phase 2: Foundational (T002) — blocks everything.
3. Complete Phase 3: User Story 1 (T003–T011).
4. **STOP and VALIDATE**: confirm the grid displays outstanding fees correctly and Amount auto-calculates on selection, per User Story 1's Independent Test.
5. Demo if ready — the grid is visible and interactive even before allocation-on-save (User Story 2) lands, since `PaymentForm.SaveAsync` still works today via the pre-existing Amount-only path.

### Incremental Delivery

1. Setup + Foundational → foundation ready.
2. Add User Story 1 → test independently → demo (grid + Amount auto-calc working).
3. Add User Story 2 → test independently → demo (save now allocates exactly to the checked fees, with validation).
4. Polish (T019–T020) → full regression pass + manual quickstart walkthrough.

### Team Strategy

With two developers: one can take Phase 3 (UI grid + display wiring) while the other takes Phase 4's service-layer half (T012–T014, independent of the grid). The final UI-wiring tasks (T017, T018) need both halves merged first.
