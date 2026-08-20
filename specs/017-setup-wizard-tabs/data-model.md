# Phase 1 Data Model: Setup Wizard Tabbed Redesign

No new database entities or migrations. Every persisted record this feature creates (`Account`, `JournalEntry`/`Transaction`, `CommitteeOfficeHolderType`, `Settings`) already exists and is unchanged in shape. What's new is **in-progress wizard state** (never persisted directly) and one **extended request model**.

## In-progress wizard state (not persisted)

Owned by `SetupWizard.razor.cs` for the lifetime of the wizard; discarded if the coordinator never finishes setup (spec Edge Cases: nothing here survives an abandoned setup attempt except an already-first-run app state).

### QueuedCommitteeRole
| Field | Type | Notes |
|---|---|---|
| `Title` | `string` | Trimmed, non-empty (FR-011). Uniqueness enforced case-insensitively against the rest of the queue at add-time. |

Unchanged in substance from today's comma-separated `CommitteeOfficeHolderTitlesText` — only the entry/removal mechanism (FR-009/FR-010) and storage shape (a list the user builds one item at a time, instead of one delimited string) change.

### QueuedAccount
| Field | Type | Notes |
|---|---|---|
| `Name` | `string` | Required, ≤100 chars (matches `AddAccountForm`'s validation, carried over from `ChartOfAccountsPage`'s existing `NewAccountModel`). Unique case-insensitively against both the queue and any already-persisted account (FR-014). |
| `Type` | `AccountType` | Existing enum (`Asset`, `Liability`, `Equity`, `Income`, `Expense`) — no change. |
| `IsBankAccount` | `bool` | Only meaningful, and only offered in the form, when `Type == Asset` (FR-012, matches existing behavior). |

**State transition**: created via the shared `AddAccountForm`'s `OnSubmit` callback → held in the queue, rendered in a `BorderedListBox` → at Finish, each becomes one real `Account` row via `IAccountService.CreateAsync` (assigns `Id`, sequential `AccountNumber`, `IsSystem = false`) inside `SetupService.InitializeAsync`. Removed from the queue directly if the coordinator removes it before Finish (no DB interaction, since nothing was persisted yet).

### QueuedOpeningBalanceEntry
| Field | Type | Notes |
|---|---|---|
| `AccountRef` | `Guid` (existing account) **or** a queued-account local reference | Resolved to a real `AccountId` at Finish, after queued accounts are created. See "Account reference resolution" below. |
| `Amount` | `decimal` | Same sign convention as today's `OpeningBalanceEntry.Amount` — positive posts to the account's normal side, negative to the opposite side (existing `OpeningBalanceService.ToNormalSide` logic, unchanged). Zero entries are simply not queued (mirrors the standalone page filtering `NonZeroRows`). |

**Account reference resolution**: because a coordinator can enter an opening balance for an account they *just queued* on the Chart of Accounts tab (which has no real `Id` yet), the Opening Balances tab's rows are keyed by a stable local reference (e.g. the queued account's position/a client-side `Guid` placeholder) for already-existing accounts this is simply the real `AccountId`. `SetupWizard.razor.cs` resolves every queued-account local reference to its real `AccountId` immediately after `IAccountService.CreateAsync` returns for that account, before building the final `RecordOpeningBalancesRequest`.

**State transition**: entered via the shared `OpeningBalanceEntryForm`'s `OnSubmit` callback → held in the queue → at Finish, posted as one `RecordOpeningBalancesRequest` (all queued entries together, same as the standalone page's "Post" step) via `IOpeningBalanceService.RecordOpeningBalancesAsync`, which creates one `JournalEntry` (type `OpeningBalance`) plus balanced `Transaction` lines (existing behavior, unchanged). Removed automatically if its `QueuedAccount` is removed (FR-020) or directly if the coordinator clears the amount back to zero.

## Extended request model

### SetupRequest (extended)
Existing record, `src/StageFright.Core/Modules/Settings/SetupRequest.cs`. Adds three properties, all optional/defaulted so no existing caller (there are none outside the wizard today) breaks:

| New field | Type | Default | Notes |
|---|---|---|---|
| `QueuedAccounts` | `IReadOnlyList<QueuedAccountRequest>?` | `null` | Mirrors `CommitteeOfficeHolderTitles`'s existing optional-list shape. `QueuedAccountRequest` is a small new record: `(string Name, AccountType Type, bool IsBankAccount)`. |
| `QueuedOpeningBalances` | `IReadOnlyList<OpeningBalanceEntry>?` | `null` | Reuses the **existing** `OpeningBalanceEntry` type (`StageFright.Core.Modules.Finance.OpeningBalanceEntry`) — no new type needed once account references are already resolved to real `AccountId`s by the caller. |
| `OpeningBalanceAsAtDate` | `DateTime` | `DateTime.UtcNow.Date` | The date opening balances are recorded as at (see research.md's as-at-date decision). Ignored when `QueuedOpeningBalances` is empty. |

### SetupService.InitializeAsync (extended orchestration, no new persisted shape)
Sequence after the existing Settings/event-type creation, before the existing committee-office-holder-title loop:
1. For each `QueuedAccounts` entry → `IAccountService.CreateAsync(name, type, isBankAccount)`, capturing the returned `Account.Id` keyed by the queue's local reference.
2. If `QueuedOpeningBalances` is non-empty → resolve each entry's account reference to a real `AccountId` (already-existing accounts pass through unchanged; queued-account references use the map built in step 1) → call `IOpeningBalanceService.RecordOpeningBalancesAsync` once with the full resolved list and `OpeningBalanceAsAtDate`.
3. Existing committee-office-holder-title loop, unchanged.

## Validation rules carried over unchanged

These are not new rules — restated here only to confirm nothing about them changes in the tabbed/queued redesign:
- Account name uniqueness (case-insensitive), 100-char max, bank flag restricted to `Asset` — `AccountService.CreateAsync`.
- Opening balance posting requires ≥1 non-zero entry; each account may appear once; only eligible (non-excluded) accounts accepted — `OpeningBalanceService.RecordOpeningBalancesAsync`.
- `SetupService.Validate` — organisation name required, tax rate > 0 when tax applicable, non-negative fees, renewal month 1–12, audit retention 1–7 years. Unchanged; still runs before any creation.
