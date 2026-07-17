# Interface Contracts: Outstanding Fee Selection on the Record Member Payment Form

This app has no external HTTP/CLI surface — the contracts consumed by other layers (UI ↔ Application) are the C# service interfaces in `StageFright.Core/Contracts/`. This document specifies the amended and new contract members this feature introduces, for the `Finance` module.

## `IMemberBalanceService` (amended)

**File**: `src/StageFright.Core/Contracts/IMemberBalanceService.cs`

```csharp
public interface IMemberBalanceService
{
    Task<decimal> GetBalanceAsync(Guid memberId, CancellationToken ct = default);
    Task<IReadOnlyList<MemberBalance>> GetAllMemberBalancesAsync(CancellationToken ct = default);

    // NEW
    Task<IReadOnlyList<OutstandingFee>> GetOutstandingFeesAsync(Guid memberId, CancellationToken ct = default);
}
```

### `GetOutstandingFeesAsync` contract

- **Input**: `memberId` — must be an existing member; `ct` — standard cancellation.
- **Output**: `IReadOnlyList<OutstandingFee>`, ordered oldest-first (`FeeDate ASC, CreatedAt ASC, Id ASC`, matching `IFeeRepository.GetUnpaidOrderedFifoAsync`'s ordering).
- **Contents**: Every fee belonging to `memberId` whose true remaining-owed amount (`Fee.Amount − Σ CreditAmount` of `MemberReceivable` GL rows tagged with that `FeeId`) is `> 0`. Fully-settled fees are excluded.
- **Empty result**: A member with no outstanding fees returns an empty list (not an error) — the caller (`PaymentForm`) is responsible for the FR-013 empty-state UI behavior.
- **Errors**: Does not throw for a member with zero fees or zero balance (empty list, not an exception). Follows the same exception-boundary rules as the rest of the module — any persistence failure is translated to a project custom exception (`DataAccessException`/`PersistenceException`) before crossing out of `MemberBalanceService`, consistent with existing methods on this interface.
- **Side effects**: None — pure read.

## `IPaymentService.RecordAsync` (amended, no signature change)

**File**: `src/StageFright.Core/Contracts/IPaymentService.cs` — interface member signature is unchanged:

```csharp
public interface IPaymentService
{
    Task<Payment> RecordAsync(RecordPaymentRequest request, CancellationToken ct = default);
    Task UpdateNotesAsync(Guid paymentId, string? notes, CancellationToken ct = default);
}
```

Only the request DTO gains a field (see below) and the method's internal behavior branches on it. This preserves source and binary compatibility for every existing caller.

### `RecordPaymentRequest` (amended)

**File**: `src/StageFright.Core/Modules/Finance/RecordPaymentRequest.cs`

```csharp
public class RecordPaymentRequest
{
    public Guid MemberId { get; set; }
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public PaymentType PaymentType { get; set; }
    public string? Notes { get; set; }

    // NEW
    public IReadOnlyList<Guid>? SelectedFeeIds { get; set; }
}
```

### `RecordAsync` contract (amended behavior)

**Preconditions** (in evaluation order, inside the existing `ExecuteInTransactionAsync` boundary):

1. `request.Amount <= 0m` → `ValidationException` (**unchanged**, pre-existing check).
2. `request.SelectedFeeIds is { Count: 0 }` (explicitly empty, not null) → `ValidationException("At least one outstanding fee must be selected.", nameof(Payment), nameof(RecordAsync))` — **new**, FR-007.
3. `request.SelectedFeeIds is { Count: > 0 }` and `request.Amount` exceeds the sum of those fees' true remaining-owed amounts → `ValidationException("Amount exceeds the selected fees' remaining total.", nameof(Payment), nameof(RecordAsync))` — **new**, FR-006.

**Postconditions**:

- `request.SelectedFeeIds == null`: identical to current behavior — FIFO allocation across `IFeeRepository.GetUnpaidOrderedFifoAsync(request.MemberId, ct)`, any excess becomes an overpayment credit. **No behavior change** for any existing caller (FR-012).
- `request.SelectedFeeIds` non-empty: allocation walks only the fees whose `Id` is in `SelectedFeeIds`, in the same oldest-first order, fully settling each before applying any remainder to the next (FR-008), and never allocates to a fee outside the selection (FR-009). Because precondition 3 already guarantees `Amount <= Σ(selected remaining-owed)`, the selected-fee allocation loop always fully consumes `remainingPayment` by the time it exhausts the selected fees — no overpayment-credit branch fires in the selected-fee path (there is nothing left over by construction).
- Every GL transaction pair created is still balanced (Σdebits == Σcredits) and wrapped in the existing single `DbContext` ACID transaction; `GLBalanceException` semantics are unchanged.
- Returns the saved `Payment` exactly as today.

**Error taxonomy** (unchanged set, one new triggering condition each for two of them):

| Exception | New trigger added by this feature |
|---|---|
| `ValidationException` | Empty explicit selection with `Amount > 0`; Amount exceeds selected fees' remaining total. |
| `GLBalanceException` | None new — still only fires on a debit/credit imbalance, which the filtered loop cannot introduce since it reuses the same balanced-pair construction. |
| `EntityNotFoundException` | Not thrown by this method for an unrecognized `FeeId` in `SelectedFeeIds` — an ID that doesn't match any of the member's outstanding fees is simply treated as contributing zero to both the remaining-total sum and the allocation loop (the filter `fees.Where(f => selectedSet.Contains(f.Id))` naturally drops IDs that aren't in the member's fee list), so it can never cause an over-allocation; no dedicated existence check is required for GL-safety purposes. (The UI never sends unrecognized IDs, since it only offers checkboxes for fees it just loaded from `GetOutstandingFeesAsync`.) |

## UI contract: `OutstandingFeeSelectionGrid` component

**File**: `src/StageFright.UI/Pages/Finance/OutstandingFeeSelectionGrid.razor` / `.razor.cs`

Parameters (Blazor component contract, not a service interface, but documented here since it's the other half of this feature's "interface" surface):

| Parameter | Type | Direction | Notes |
|---|---|---|---|
| `Fees` | `IReadOnlyList<OutstandingFee>` | In | The member's outstanding fees, as loaded by the parent (`PaymentForm`) via `IMemberBalanceService.GetOutstandingFeesAsync`. |
| `ReadOnly` | `bool` | In | `true` after the payment is saved — disables all checkboxes (FR-010). |
| `SelectionChanged` | `EventCallback<decimal>` | Out | Fires whenever the checked-fee set changes, carrying the new sum of checked fees' `RemainingAmount` (FR-004). The parent uses this to update `PaymentFormModel.Amount`. |
| `SelectedFeeIds` | Exposed via a public read method/property (e.g. `GetSelectedFeeIds() : IReadOnlyList<Guid>`) | Out | Read by `PaymentForm.SaveAsync()` at submit time to populate `RecordPaymentRequest.SelectedFeeIds`. |

**Empty-state contract**: if `Fees` is empty, the component renders an empty-state message in place of the grid (FR-013); `PaymentForm` is responsible for disabling Save in that case (also FR-013).
