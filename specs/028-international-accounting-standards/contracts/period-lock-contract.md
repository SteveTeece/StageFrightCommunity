# Contract: Closed-period lock

Covers US6 (FR-016, FR-017, FR-018). Consumers: `GLRepository`, every finance posting service (via
the GL choke point), and every finance posting form in `StageFright.UI`.

---

## `Settings.ClosedThroughDate`

`DateTime?`, default `null`. `null` = no period is closed. When set, all financial periods up to **and
including** that date are closed (FR-016).

## `ClosedPeriodException` (new)

`src/StageFright.Core/Exceptions/ClosedPeriodException.cs` — `sealed class ClosedPeriodException :
Exception`, Constitution §5.2 shape:

```csharp
public ClosedPeriodException(
    string message,
    string entityType,
    string operationContext,
    Guid? entityId = null,
    Exception? innerException = null)
```

with `EntityType`, `EntityId` (nullable `Guid`), `OperationContext`, `Timestamp` (`DateTime.UtcNow`
at construction), `CorrelationId` (new `Guid`). Not a subclass of `GLBalanceException` — it is a
distinct signal.

## `IClosedPeriodGuard` (new)

`src/StageFright.Core/Contracts/IClosedPeriodGuard.cs`

```csharp
public interface IClosedPeriodGuard
{
    Task EnsureOpen(DateTime postingDate, CancellationToken ct = default);
}
```

Implementation `src/StageFright.Core/Modules/Finance/ClosedPeriodGuard.cs` (depends only on
`ISettingsRepository`):

| Condition | Result |
|-----------|--------|
| `Settings` is null (pre-setup) | returns (no-op) |
| `Settings.ClosedThroughDate` is `null` | returns (no-op) |
| `postingDate.Date <= Settings.ClosedThroughDate.Value.Date` | throws `ClosedPeriodException` (`Validation_ClosedPeriod_PostingRejected`) |
| otherwise | returns |

A posting dated **exactly on** the closed-through date is inside the closed period and is rejected
(spec Edge Cases).

## Enforcement point — `GLRepository`

`src/StageFright.Data/Repositories/GLRepository.cs` (injects `IClosedPeriodGuard`):

* `AddBalancedSetAsync(lines, ct)` — before the existing balance validation and before
  `SaveChangesAsync`, call `await _closedPeriodGuard.EnsureOpen(line.Date, ct)` for every line
  (or once for the earliest line's date).
* `AddPairAsync(debit, credit, ct)` — same, before delegating to `AddBalancedSetAsync`.

Because every financial mutation funnels through these two methods, and they run inside
`UnitOfWork.ExecuteInTransactionAsync`, a rejection rolls the whole operation back: **no business row
(Fee / Payment / JournalEntry) and no ledger line is persisted** (FR-017).

`ClosedPeriodException` propagates unwrapped through `UnitOfWork.ExecuteInTransactionAsync` (added to
the same pass-through list as `GLBalanceException`), so the UI receives the typed exception rather
than a `DataAccessException` wrapper.

## Setup-time opening balances (FR-018)

No carve-out is implemented. First-run setup always completes before any period can be closed, so
`ClosedThroughDate` is `null` while the setup wizard runs and opening balances post normally. This is
documented in `ClosedPeriodGuard`'s XML doc. If a future story needs a genuine bypass, the ambient
`AuditTrailSuppressionScope` pattern (`StageFright.Core/Modules/AuditTrail`) is the template — not a
new parameter on the guard.

## UI (FR-016)

* A "close all financial periods through <date>" date control plus an explicit confirmation step on
  the General settings tab (`src/StageFright.UI/Pages/Settings/GeneralSettingsTab.razor`), element id
  `settings-close-through-date`. Saving sets `Settings.ClosedThroughDate` via `SettingsService.SaveAsync`.
* Every finance posting form (journal, payment, expense, income, bank deposit, opening balances,
  reactivation forgiveness) catches `ClosedPeriodException` and shows the friendly
  `Validation_ClosedPeriod_PostingRejected` message; the form is left re-submittable after the user
  changes the date.

## Test surface

* Unit: `ClosedPeriodGuard` — null settings, null date, date before / equal / after the closed-through
  date.
* Integration: for each posting path, a transaction dated on or before `ClosedThroughDate` leaves no
  `Fee` / `Payment` / `Transaction` / `JournalEntry` row (FR-017, SC-009); a transaction dated after
  it posts normally; opening balances during first-run setup are accepted (FR-018).
