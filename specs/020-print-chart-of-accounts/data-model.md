# Phase 1 Data Model: Print Chart of Accounts

No entity is introduced or reshaped. This feature is a read-only report over the existing `Account` entity and its already-derived `AccountBalance` projection; no new field, migration, or persisted state is added.

## Existing entity reused (unchanged)

### `Account` (`StageFright.Core/Entities/Account.cs`)

Fields this feature reads:

| Field | Type | Used for |
|---|---|---|
| `AccountNumber` | `string` | Row's "No." cell; within-section ordering (FR-005) |
| `Name` | `string` | Row's "Name" cell |
| `Type` | `AccountType` (enum: `Asset`, `Liability`, `Equity`, `Income`, `Expense`) | Which of the five fixed sections the row prints under (FR-004) |
| `IsSystem` | `bool` | Plain-text "(System)" suffix on the Name cell (FR-006) |
| `IsBankAccount` | `bool` | Plain-text "(Bank)" suffix on the Name cell (FR-006) |
| `IsDeleted` | `bool` | Archived accounts are excluded — the report only ever reads `IAccountBalanceService.GetActiveAccountBalancesAsync()`, which never returns archived rows (FR-011) |

Current balance is **not** a stored field — same as today's Chart of Accounts screen, it is computed at read time by `IAccountBalanceService` from the GL (see [research.md](./research.md)).

## Existing transient projection reused (unchanged)

### `AccountBalance` (`StageFright.Core/Modules/Finance/AccountBalance.cs`)

Already carries every field the report needs per row (`AccountId`, `AccountNumber`, `Name`, `Type`, `IsSystem`, `IsBankAccount`, `Balance` (nullable `decimal`), `HasError`). `ChartOfAccountsReportProvider` consumes this type directly — it does not introduce its own row DTO.

## Report-time shape (not persisted — `ReportData`, existing model)

`ChartOfAccountsReportProvider.GenerateAsync` produces one `ReportData` per call:

- **Sections** (fixed order, always present even when empty per the spec's edge case): `Assets`, `Liabilities`, `Equity`, `Income`, `Expenses` — one `ReportSection` each, `Rows` = that type's `AccountBalance` entries already ordered by `AccountNumber` (the order `IAccountBalanceService` returns them in).
- **Columns**: `["No.", "Name"]` when `includeBalances` is off; `["No.", "Name", "Balance"]` when on (see [research.md](./research.md) — the column is structurally absent, not blank).
- **Row cells**: `[AccountNumber, Name-with-suffix]` or `[AccountNumber, Name-with-suffix, Balance-or-error-text]`.
- **`GrandTotal`**: always `null` (FR-012 — no combined balance figure).
- **`SummaryColumns`**: always `null` — this is a flat report, not master-detail.

No state transitions apply — every value is recomputed fresh on each `GenerateAsync` call.
