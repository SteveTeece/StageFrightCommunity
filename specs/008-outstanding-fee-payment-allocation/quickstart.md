# Quickstart: Validating Outstanding Fee Selection on the Record Member Payment Form

This guide validates the feature end-to-end once implemented, covering both automated and manual checks. It maps directly to the spec's acceptance scenarios (User Story 1 & 2) and Success Criteria.

## Prerequisites

- Repo built successfully: `dotnet build` from the repo root.
- A local dev database exists at `<repo-root>/TestData/stagefright.db` (auto-created on first run) with at least one member who has multiple `Fee` records, at least one of which has already been partially paid by a prior `Payment` (needed to exercise FR-011 — remaining-amount-owed display).

## 1. Automated validation

Run the full suite first to confirm no regressions (per CLAUDE.md's Build & Test Verification rule):

```bash
dotnet build
dotnet test
```

Then target the feature's own new/changed tests directly while iterating:

```bash
# Core service logic: allocation filtering, validation, new GetOutstandingFeesAsync
dotnet test --filter "FullyQualifiedName~PaymentServiceTests"
dotnet test --filter "FullyQualifiedName~MemberBalanceServiceTests"

# UI: PaymentForm wiring + new grid component
dotnet test tests/StageFright.UI.Tests/ --filter "FullyQualifiedName~PaymentFormTests"
dotnet test tests/StageFright.UI.Tests/ --filter "FullyQualifiedName~OutstandingFeeSelectionGridTests"

# End-to-end acceptance scenario against a real SQLite-backed DbContext
dotnet test tests/StageFright.Integration.Tests/ --filter "FullyQualifiedName~V5_PaymentsTests"
```

**Expected outcome**: all suites green, including the pre-existing `V5_PaymentsTests` FIFO/overpayment cases (proving `SelectedFeeIds == null` behavior is unchanged — SC-005).

## 2. Manual validation — User Story 1 (selecting fees updates Amount)

1. `dotnet run --project src/StageFright.App/`
2. Navigate to **Finance → Outstanding**.
3. Pick a member with 2+ outstanding fees (ideally one partially paid). Click **Record Member Payment**.
4. **Verify (FR-001, SC-001)**: a grid appears listing that member's outstanding fees — type, fee date, due date, and remaining amount owed. For the partially-paid fee, confirm the amount shown is the *remaining* balance, not the original full fee amount (FR-011).
5. Check one fee's checkbox (FR-002 — checkbox is the leftmost column).
6. **Verify (FR-004, SC-002)**: the Amount field updates to that fee's remaining amount.
7. Check a second fee.
8. **Verify**: Amount updates to the sum of both checked fees.
9. Uncheck the first fee.
10. **Verify (Acceptance Scenario 3)**: Amount drops back to just the second fee's remaining amount.
11. Click the header **Select All** checkbox.
12. **Verify (FR-003, Acceptance Scenario 4)**: every fee becomes checked and Amount equals their combined total.

## 3. Manual validation — User Story 2 (saving allocates to exactly the selected fees)

1. With all fees still checked from step 3.12 above, reduce the Amount field below the checked total.
2. **Verify (FR-005)**: the field accepts the edit (remains editable after being auto-filled).
3. Click **Save**.
4. **Verify (FR-008, Acceptance Scenario 2)**: the oldest checked fee is fully settled first; the next checked fee absorbs the remainder (partially, if insufficient) or is left fully outstanding if the amount didn't reach it; any *unchecked* fee is untouched even if it was chronologically next (FR-009).
5. **Verify (FR-010, Acceptance Scenario 5)**: after save, the grid's checkboxes become disabled (read-only), matching the rest of the form's post-save disabled fields.
6. Return to **Finance → Outstanding**, re-open the same member's Record Member Payment form.
7. **Verify (SC-003)**: the grid now reflects the updated remaining amounts — settled fees no longer appear (or appear with reduced/zero remaining, and thus are excluded per FR-011).
8. Open **Finance → Reports → Account Register** (or equivalent GL view) filtered to this member/payment.
9. **Verify (FR-008, SC-004)**: GL transaction rows for this payment are linked (`FeeId`) only to the fees that were checked, traceable 1:1 to the selection made in step 3.

## 4. Manual validation — blocking rules

1. Open the payment form for a member with outstanding fees. Enter an Amount > 0 without checking any fee. Click Save.
   - **Verify (FR-007, Acceptance Scenario 4 of US2)**: Save is blocked with a validation message.
2. Check one fee, then manually type an Amount greater than that fee's remaining amount (don't check more fees to cover it).
   - **Verify (FR-006, Acceptance Scenario 3 of US2)**: Save is blocked with a validation message; no payment is recorded (confirm via Outstanding tab balance unchanged).
3. Navigate directly (via URL/manual `MemberId`) to the payment form for a member with **zero** outstanding fees.
   - **Verify (FR-013, Edge Case)**: the grid area shows an empty-state message and Save is disabled.

## 5. Regression check — existing callers unaffected

1. Run (or inspect) any data-seeding / bulk payment path that doesn't go through `PaymentForm` (e.g. test fixtures, `V5_PaymentsTests` FIFO cases).
2. **Verify (FR-012, SC-005)**: behavior is identical to pre-feature — automatic FIFO allocation across the member's full unpaid fee history, with existing overpayment-credit handling, since these callers never set `RecordPaymentRequest.SelectedFeeIds` (it stays `null`).
