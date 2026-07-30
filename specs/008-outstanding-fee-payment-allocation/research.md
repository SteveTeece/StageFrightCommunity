# Phase 0 Research: Outstanding Fee Selection on the Record Member Payment Form

No items in Technical Context were marked `NEEDS CLARIFICATION` — the spec's Clarifications session (2026-07-17) already resolved every open behavioral question (allocation semantics, Amount editability, overpayment blocking, partial-payment ordering, checkbox placement, empty-selection blocking, UI+service dual validation). The research below instead resolves the *implementation-approach* unknowns needed to design `data-model.md` and `contracts/`, based on direct inspection of the current codebase (`PaymentForm`, `PaymentService`, `MemberBalanceService`, `AttendanceGrid`).

## Decision 1: Where "remaining amount owed per fee" lives

**Decision**: Add `IMemberBalanceService.GetOutstandingFeesAsync(Guid memberId, CancellationToken ct)` returning `IReadOnlyList<OutstandingFee>`, where `OutstandingFee.RemainingAmount` is computed per-fee as `fee.Amount − Σ(CreditAmount on MemberReceivable GL rows for that fee)`, and fees with `RemainingAmount <= 0m` are excluded.

**Rationale**: This exact formula already exists, inline, inside `PaymentService.RecordAsync`'s FIFO loop (`fee.Amount - alreadySettled` via `_glRepo.GetByFeeAsync(fee.Id, ct)`). A second, *different* approximation exists in `MemberBalanceService.SelectOutstandingFees`, which decides fee inclusion via an aggregate FIFO-prefix trim against the member's total GL balance but returns each included fee at its full original `Amount` — this is wrong for a partially-paid fee and cannot be reused as-is for the grid (FR-011 explicitly requires the true remaining amount). Introducing one new method that reuses the correct, already-proven formula avoids a third divergent implementation and keeps `MemberBalanceService` (which owns per-member fee/balance projections) as the natural home, alongside its existing `GetAllMemberBalancesAsync`.

**Alternatives considered**:
- *Reuse `MemberBalance.Fees` as-is*: Rejected — returns full original `Amount`, not remaining owed; would make the "Amount can't exceed checked total" validation (FR-006) check against the wrong number for any fee with prior partial settlement (edge case explicitly called out in spec.md).
- *Compute remaining-owed inline in the UI code-behind by calling `IGLRepository` directly from `PaymentForm.razor.cs`*: Rejected — violates the module's service-layer boundary (UI should call `IMemberBalanceService`/`IPaymentService`, not repositories directly) and would triplicate the formula instead of centralizing it.
- *Add the method to `IFeeRepository` instead of `IMemberBalanceService`*: Rejected — the computation spans two repositories (`IFeeRepository` for the fee list, `IGLRepository` for settlement history) and existing per-member aggregation methods (`GetBalanceAsync`, `GetAllMemberBalancesAsync`) already live on `IMemberBalanceService`, so this keeps aggregation logic in one place per CLAUDE.md's "repositories are not module-owned, live centrally" split (repos stay dumb data-access, services own the business projection).

## Decision 2: How `RecordAsync` distinguishes "no selection" vs "explicitly empty selection"

**Decision**: `RecordPaymentRequest.SelectedFeeIds` is `IReadOnlyList<Guid>?`:
- `null` → legacy/default behavior, unchanged: FIFO allocation across the member's full unpaid fee history (every existing caller — seeding, other future bulk tooling — passes `null` implicitly by never setting the property).
- Non-null and **empty** → `ValidationException` ("at least one outstanding fee must be selected"), per FR-007 — this is the shape the UI submits if Amount > 0 but no checkbox is checked.
- Non-null and **non-empty** → allocate to exactly those fees, oldest-first (filtering the existing FIFO-ordered fee list down to the selected IDs), per FR-008/FR-009.

**Rationale**: A nullable list is the minimal signal needed to distinguish "caller doesn't know/care about selection" (preserve old behavior, FR-012) from "caller went through the selection UI and selected nothing" (block, FR-007). This avoids adding a second boolean flag or a new request type, keeping `RecordPaymentRequest` backward compatible — existing callers that never touch the new property compile and behave identically.

**Alternatives considered**:
- *Overload `RecordAsync` with a second method signature* (`RecordAsync(request, selectedFeeIds)`): Rejected — spec FR-006/FR-007 require validation to live inside `RecordAsync` itself regardless of entry point ("independently enforces... even if invoked by a caller other than this form"), so a single method with an optional request property is simpler and keeps one code path instead of two.
- *Empty list also means "use default FIFO"*: Rejected — contradicts FR-007's explicit requirement that an empty selection with `Amount > 0` must be blocked at the service layer, not silently treated as "no preference."

## Decision 3: Allocation-loop change is a filter, not a new algorithm

**Decision**: In `PaymentService.RecordAsync`, after `var fees = await _feeRepo.GetUnpaidOrderedFifoAsync(...)`, when `request.SelectedFeeIds` is non-null, do:
```csharp
if (request.SelectedFeeIds is not null)
{
    if (request.SelectedFeeIds.Count == 0)
        throw new ValidationException("At least one outstanding fee must be selected.", nameof(Payment), nameof(RecordAsync));

    var selectedSet = request.SelectedFeeIds.ToHashSet();
    fees = fees.Where(f => selectedSet.Contains(f.Id)).ToList();
}
```
before the existing FIFO `foreach (var fee in fees)` loop, with the pre-loop validation that `request.Amount` does not exceed the sum of the selected fees' remaining-owed (computed the same way the loop already does, via `_glRepo.GetByFeeAsync`), throwing `ValidationException` if it does (FR-006).

**Rationale**: `GetUnpaidOrderedFifoAsync` already returns fees in FIFO order (`FeeDate ASC, CreatedAt ASC, Id ASC`), so filtering the ordered list to the selected-ID set preserves oldest-first ordering automatically — no new sort/ordering logic needed. Everything downstream (per-fee remaining-owed lookup, GL pair creation, overpayment-credit fallthrough for any amount left after the loop) is untouched. This keeps the diff small and auditable against the existing, already-tested FIFO block, minimizing risk to GL correctness.

**Alternatives considered**:
- *Write a separate allocation method for the selected-fee case*: Rejected — would duplicate the GL-pair-creation and remaining-owed-lookup logic that the existing loop already gets right (and is already covered by `RecordAsync_SkipsFeesAlreadySettledByPriorPayments` and related tests), doubling the surface area for GL-balancing bugs.
- *Validate selected-fee-IDs belong to the member inside the UI only*: Rejected — spec's clarification session explicitly requires service-layer enforcement (FR-006/FR-007) "to protect GL integrity even if a future caller bypasses the UI."

## Decision 4: New grid is a separate paired component, not inline in `PaymentForm`

**Decision**: Create `OutstandingFeeSelectionGrid.razor` / `.razor.cs` in `src/StageFright.UI/Pages/Finance/`, parameterized by the loaded `IReadOnlyList<OutstandingFee>` and an `EventCallback<decimal>` (or similar) that fires the new checked-total whenever selection changes. `PaymentForm` owns fetching the outstanding fees (via `IMemberBalanceService.GetOutstandingFeesAsync`) and wires the callback to update `PaymentFormModel.Amount`.

**Rationale**: Matches the existing `AttendanceGrid`/`ParticipationGrid` precedent exactly — both are standalone paired components embedded in a parent form/page rather than inlined, keeping each component's code-behind focused on one concern (§3.2.1 Single Responsibility via file organization). `PaymentForm.razor.cs` is already non-trivial (member load, save, notes-update, validation); folding grid/selection-state logic directly into it would make one file own two responsibilities.

**Alternatives considered**:
- *Inline the grid markup and selection logic directly in `PaymentForm.razor`/`.razor.cs`*: Rejected — breaks the one-component-one-concern precedent set by `AttendanceGrid`/`ParticipationGrid`, and CLAUDE.md's data-grid standards section treats those as *the* reference pattern to follow.

## Decision 5: Selection state uses a row view-model, not mutated `Fee` entities

**Decision**: The new grid binds to a local, UI-only row type (e.g. `OutstandingFeeRow { OutstandingFee Fee; bool Selected; }`) constructed from the `IReadOnlyList<OutstandingFee>` returned by the service, mirroring `AttendanceGrid`'s `AttendanceRow` pattern.

**Rationale**: `Fee` (and the new `OutstandingFee` DTO) are read-only projections; mutating them to carry a `Selected` bool would conflate a domain/read-model type with transient UI state and risk implying `Fee` itself is mutable, which contradicts the financial-immutability principle even though `Selected` isn't persisted. `AttendanceGrid`'s `AttendanceRow` already establishes this exact pattern for a checkbox grid in this codebase.

**Alternatives considered**:
- *Track selection as a separate `HashSet<Guid>` of checked fee IDs alongside the read-only fee list*: Viable alternative, slightly more indirection (checkbox `checked` state requires a `Contains` lookup per row per render instead of a direct property). Row view-model chosen for consistency with `AttendanceGrid`'s established convention and simpler Radzen template binding (`checked="@row.Selected"` vs `checked="@SelectedIds.Contains(row.Fee.FeeId)"`).
