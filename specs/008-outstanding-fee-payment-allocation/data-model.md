# Phase 1 Data Model: Outstanding Fee Selection on the Record Member Payment Form

This feature introduces no new persisted entities and no schema changes. It adds one new read-model DTO, extends one existing request DTO, and introduces one new UI-only row view-model. All existing entities (`Fee`, `Payment`, `Transaction`) are referenced as-is and remain immutable per the constitution's financial-data-preservation rules.

## New: `OutstandingFee` (read-model DTO)

**Location**: `src/StageFright.Core/Modules/Finance/OutstandingFee.cs`

Represents one of a member's outstanding fees with its **true remaining amount owed** (not the fee's original full amount — see [research.md](./research.md) Decision 1).

| Field | Type | Notes |
|---|---|---|
| `FeeId` | `Guid` | The underlying `Fee.Id`. |
| `FeeType` | `FeeType` (existing enum) | Copied from `Fee.FeeType`, for grid display (FR-001). |
| `FeeDate` | `DateTime` | Copied from `Fee.FeeDate`, for grid display and to preserve FIFO ordering context. |
| `DueDate` | `DateTime` | Copied from `Fee.DueDate`, for grid display (FR-001). |
| `RemainingAmount` | `decimal` | `Fee.Amount − Σ(CreditAmount` on `MemberReceivable` GL transactions linked to this `FeeId)`. Always `> 0` — fees with zero or negative remaining are excluded from the returned list (FR-011). |

**Validation / invariants**:
- Never constructed with `RemainingAmount <= 0m` — the producing method (`IMemberBalanceService.GetOutstandingFeesAsync`) filters those out before returning.
- Immutable (init-only or record) — this is a read projection, not a tracked entity; there is nothing to persist back.
- Ordering: the method returns the list in the same FIFO order (`FeeDate ASC, CreatedAt ASC, Id ASC`) as `IFeeRepository.GetUnpaidOrderedFifoAsync`, so the grid's default (unsorted) row order already reflects oldest-first, though the grid itself remains user-sortable (`AllowSorting="true"`, consistent with the standard grid convention) without affecting allocation order, since allocation order is always determined server-side, not by display order.

## Modified: `RecordPaymentRequest`

**Location**: `src/StageFright.Core/Modules/Finance/RecordPaymentRequest.cs`

Adds one new property; all existing properties (`MemberId`, `Date`, `Amount`, `PaymentMethod`, `PaymentType`, `Notes`) are unchanged.

| Field | Type | Notes |
|---|---|---|
| `SelectedFeeIds` | `IReadOnlyList<Guid>?` | **New.** `null` = legacy/no-selection behavior (FIFO across full unpaid history, FR-012 — unchanged for every existing caller). Non-null-and-empty = explicit "nothing selected," rejected by `RecordAsync` (FR-007). Non-null-and-non-empty = allocate to exactly these fee IDs, oldest-first (FR-008/FR-009). See [research.md](./research.md) Decision 2. |

**State/validation transitions enforced by `PaymentService.RecordAsync`** (in addition to the pre-existing `Amount <= 0m` check, which still runs first and is unaffected):

1. `SelectedFeeIds == null` → skip all new validation, existing FIFO-all-fees path runs unchanged.
2. `SelectedFeeIds is { Count: 0 }` → throw `ValidationException` ("at least one outstanding fee must be selected") — FR-007.
3. `SelectedFeeIds is { Count: > 0 }`:
   a. Resolve each selected ID's true remaining-owed amount (same formula as `OutstandingFee.RemainingAmount`).
   b. If `Amount > Σ(remaining-owed of selected fees)` → throw `ValidationException` ("amount exceeds the selected fees' remaining total") — FR-006.
   c. Otherwise, allocate `Amount` across the selected fees in oldest-first order, fully settling each before moving to the next, exactly as the existing FIFO loop already does for the unfiltered case — FR-008/FR-009.
   d. Any fee not in `SelectedFeeIds` receives zero allocation from this payment, even if it would otherwise be next in FIFO order — FR-009.

## New (UI-only): `OutstandingFeeRow`

**Location**: `src/StageFright.UI/Pages/Finance/OutstandingFeeSelectionGrid.razor.cs` (private/internal to the component, following `AttendanceGrid`'s `AttendanceRow` precedent)

| Field | Type | Notes |
|---|---|---|
| `Fee` | `OutstandingFee` | The underlying read-model row. |
| `Selected` | `bool` | Mutable, UI-only checkbox state. Not persisted; never round-trips to the service — only the resulting set of checked `FeeId`s is sent, via `RecordPaymentRequest.SelectedFeeIds`. |

**State transitions**:
- All rows start `Selected = false` on initial load.
- Per-row checkbox toggle flips one row's `Selected`.
- Header "select all" checkbox sets every row's `Selected` to the same value (matching `AttendanceGrid.ToggleSelectAll`).
- After a successful save, the grid becomes read-only (`Selected` can no longer be changed — checkboxes disabled) — FR-010, consistent with the rest of `PaymentForm`'s existing post-save disabled state.

## Existing entities referenced (unchanged)

No fields are added to any of these; listed here only to confirm the feature's boundary (per spec.md's "Key Entities" section).

- **`Fee`** (`src/StageFright.Core/Entities/Fee.cs`) — `Id`, `MemberId`, `FeeType`, `Amount`, `FeeDate`, `DueDate`, `PaidAtCreation`, `RehearsalId?`, `GstCode?`, `CreatedAt`. Immutable, no soft-delete (financial exemption). `Amount` is always the original full fee amount — the new `OutstandingFee.RemainingAmount` is a derived view, never written back to `Fee`.
- **`Payment`** (`src/StageFright.Core/Entities/Payment.cs`) — unchanged. `Notes` remains the only mutable field.
- **`Transaction`** (GL) (`src/StageFright.Core/Entities/Transaction.cs`) — unchanged structurally. This feature changes *which* `FeeId` a given payment's transactions reference (the selected fees, oldest-first) when a selection is supplied, but the entity shape and the debit/credit-pair-per-allocation mechanism are identical to today.

## Relationships

```
Member 1───* Fee
Member 1───* Payment
Payment 1───* Transaction (GL rows, always created in balanced debit/credit pairs)
Fee 1───* Transaction (via Transaction.FeeId, nullable — set on allocation rows)

OutstandingFee  ──(derived from)──>  Fee + Transaction (read-only projection, not a stored relationship)
OutstandingFeeRow ──(wraps)──>  OutstandingFee (UI-only, transient)
RecordPaymentRequest.SelectedFeeIds ──(references)──>  Fee.Id[] (validated against the member's outstanding fees inside RecordAsync)
```
