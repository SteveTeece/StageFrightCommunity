# Phase 1 — Data Model: External Organisations

This feature adds two EF Core entities (`ExternalOrganisation`, `ExternalOrganisationContact`) and one nullable column plus navigation on the existing `JournalEntry` header. One migration, `AddExternalOrganisations`, creates both tables and adds the column. The ledger lines (`Transaction`), `Fee`, `Payment` and every other entity are unchanged. The `.sfbak` backup schema goes `1.2.0` → `1.3.0` (§5).

Entity count in `StageFright.Core/Entities/` goes from 20 to 22.

---

## 1. `ExternalOrganisation` (new) — `StageFright.Core/Entities/ExternalOrganisation.cs`

An external body the society deals with: a venue, supplier, council, sponsor or other group. Named `ExternalOrganisation` so it cannot be confused with `Settings.OrganizationName`, which is the society itself (research §3).

| Field | Type | Required | Rules |
|---|---|---|---|
| `Id` | `Guid` | yes | PK |
| `Name` | `string` | yes | Trimmed before save. Not blank. Max 200 characters. Unique among active organisations, compared case-insensitively after trimming (FR-003). |
| `Address` | `string?` | no | Multi-line free text, line breaks kept. Max 1000 characters. Blank is stored as `null`. |
| `Email` | `string?` | no | Address for receipts and correspondence, stored for reference only. Trimmed. Max 254 characters. When present, must match the app's existing email pattern (`^[^@\s]+@[^@\s]+\.[^@\s]+$`, as `MemberValidationService` uses). Blank is stored as `null`. |
| `IsDeleted` | `bool` | yes | Soft-delete flag. `true` means archived. |
| `DeletedAt` | `DateTime?` | no | UTC time of archive. Cleared on restore. |
| `DeletedBy` | `string?` | no | Identity that archived it. Cleared on restore. |
| `CreatedAt` | `DateTime` | yes | UTC |
| `UpdatedAt` | `DateTime` | yes | UTC, set on every edit, archive and restore |

**Navigation**

- `Contacts`: `ICollection<ExternalOrganisationContact>`, one-to-many.
- `JournalEntries`: `ICollection<JournalEntry>`, one-to-many, the linked financial entries.

**EF configuration** — `StageFright.Data/Configurations/ExternalOrganisationConfiguration.cs`

- `Name` `IsRequired().HasMaxLength(200)`, `Address` `HasMaxLength(1000)`, `Email` `HasMaxLength(254)`.
- Filtered unique index on `Name` with `NOCASE` collation, filter `[IsDeleted] = 0`. This backs up the service check and mirrors `EventTypeConfiguration` (research §4). SQLite `NOCASE` folds ASCII only, so the C# `OrdinalIgnoreCase` check is the stricter rule and the index never refuses a name the service allowed.
- Global query filter `!IsDeleted`. Archived rows are reached with `IgnoreQueryFilters()` for the "show archived" list, the detail screen, restore and backup.

**Derived, not stored**

- *Contact count* for the list grid (FR-001): the number of the organisation's non-deleted contacts.
- *Total paid* and *Total received* for the detail screen (FR-011, FR-012): see §4.

---

## 2. `ExternalOrganisationContact` (new) — `StageFright.Core/Entities/ExternalOrganisationContact.cs`

A person at an organisation, such as the venue manager or a sponsor's marketing officer.

| Field | Type | Required | Rules |
|---|---|---|---|
| `Id` | `Guid` | yes | PK |
| `ExternalOrganisationId` | `Guid` | yes | FK → `ExternalOrganisation.Id`. Set on create and never changed: a contact belongs to exactly one organisation. |
| `Name` | `string` | yes | Trimmed. Not blank (spec US2 scenario 4). Max 200 characters. Not unique: two contacts may share a name. |
| `Role` | `string?` | no | The person's role at the organisation. Max 100 characters. |
| `Phone` | `string?` | no | Free text, so local and international formats are both accepted. No format check. Max 50 characters. |
| `Mobile` | `string?` | no | Free text, as `Phone`. Max 50 characters. |
| `Email` | `string?` | no | Max 254 characters. Format-checked with the same pattern as `ExternalOrganisation.Email` when present. |
| `IsDeleted` | `bool` | yes | `true` means removed (FR-009). |
| `DeletedAt` | `DateTime?` | no | UTC time of removal. |
| `DeletedBy` | `string?` | no | Identity that removed it. |
| `CreatedAt` | `DateTime` | yes | UTC |
| `UpdatedAt` | `DateTime` | yes | UTC |

Every optional text field is trimmed, and blank is stored as `null`.

**Navigation**: `ExternalOrganisation`, the owning organisation.

**EF configuration** — `StageFright.Data/Configurations/ExternalOrganisationContactConfiguration.cs`

- Max lengths as in the table. `Name` `IsRequired()`.
- `HasOne(c => c.ExternalOrganisation).WithMany(o => o.Contacts).HasForeignKey(c => c.ExternalOrganisationId).OnDelete(DeleteBehavior.Restrict)`. Nothing is ever hard-deleted, so no cascade is needed.
- Index on `ExternalOrganisationId`.
- Global query filter `!IsDeleted`.

---

## 3. `JournalEntry` (reshaped) — `StageFright.Core/Entities/JournalEntry.cs`

The organisation link lives on the journal entry header, because one expense payment or income entry is exactly one journal entry (research §1).

| Field | Type | Required | Rules |
|---|---|---|---|
| `ExternalOrganisationId` | `Guid?` | no | **New.** FK → `ExternalOrganisation.Id`. Set only when the entry is created, inside the same `IUnitOfWork.ExecuteInTransactionAsync` call that posts its ledger lines. Never added, changed or cleared afterwards (clarification 2, FR-010). |
| `ExternalOrganisation` | `ExternalOrganisation?` | — | **New** navigation. |

All existing fields (`Id`, `Type`, `Date`, `Description`, `CreatedAt`, `Transactions`) are unchanged. The entity still carries no soft-delete fields.

**Who may set the link**

| `JournalEntryType` | May carry a link? | Set by |
|---|---|---|
| `ExpensePayment` | yes | `ExpensePaymentService.RecordExpenseAsync` |
| `Income` | yes | `IncomeEntryService` |
| Every other type (bank deposit, bank deposit reversal, cash withdrawal, transfer, general journal, opening balances, fee and payment postings) | no, always `null` | — |

> *Since spec 033 (banking rework):* its `BankDepositReversal` and `CashWithdrawal` entry types, like `BankDeposit`, never carry a link (spec 033 FR-025).

**EF configuration** — `JournalEntryConfiguration.cs`

- `HasOne(j => j.ExternalOrganisation).WithMany(o => o.JournalEntries).HasForeignKey(j => j.ExternalOrganisationId).IsRequired(false).OnDelete(DeleteBehavior.Restrict)`.
- Index on `ExternalOrganisationId` for the history query.
- No global query filter, as today. Because `ExternalOrganisation` has a query filter, the history query loads entries by `ExternalOrganisationId` and never relies on an `Include` of the organisation, so an archived organisation's entries are still returned.

**Migration**: existing `JournalEntries` rows get `ExternalOrganisationId = NULL`. Entries recorded before this feature stay unlinked, because financial records are immutable.

### Request models (Core, Finance module)

| Type | New property | Rules |
|---|---|---|
| `RecordExpenseRequest` | `Guid? ExternalOrganisationId` | Optional. `Payee` is unchanged and stays independent of it. |
| `RecordIncomeRequest` | `Guid? ExternalOrganisationId` | Optional. |

**Posting-time validation** (research §6). The service runs these checks before the transaction opens, beside the existing account checks:

1. `ExternalOrganisationId` is `null`: the posting is byte-identical to today (SC-004).
2. No organisation has that ID, even when query filters are ignored: `EntityNotFoundException`.
3. The organisation is archived: localized `ValidationException`.
4. Otherwise the ID is stamped on the new `JournalEntry`. Amounts, accounts, tax split, ledger lines, description and audit entry are unchanged (FR-010).

The closed-period guard runs exactly as it does today. An organisation choice neither loosens nor tightens it.

### Payee auto-fill (UI state only, not stored)

On `ExpensePaymentModel`, choosing an organisation sets `Payee` to the organisation's `Name` only when `Payee` is null or whitespace. A typed payee is never overwritten. Changing or clearing the organisation afterwards leaves `Payee` as it is. Whatever payee is submitted still forms part of the transaction description, as it does today.

---

## 4. History read models (new, not persisted) — `StageFright.Core/Modules/Organisations/`

### `OrganisationHistoryEntry`

| Field | Type | Source |
|---|---|---|
| `JournalEntryId` | `Guid` | `JournalEntry.Id` |
| `Date` | `DateTime` | `JournalEntry.Date` |
| `Description` | `string?` | `JournalEntry.Description` |
| `AccountName` | `string` | The account of the entry's non-bank line: the expense account for `ExpensePayment`, the income account for `Income`. A tax line is never chosen. |
| `Type` | `JournalEntryType` | `JournalEntry.Type`: `ExpensePayment` is money paid to the organisation, `Income` is money received from it |
| `Amount` | `decimal` | Σ `DebitAmount` over the entry's `Transactions` (research §2) |

### `OrganisationHistory`

| Field | Type | Rule |
|---|---|---|
| `Entries` | `IReadOnlyList<OrganisationHistoryEntry>` | Newest first: `Date` descending, then `CreatedAt` descending |
| `TotalPaid` | `decimal` | Σ `Amount` where `Type = ExpensePayment` |
| `TotalReceived` | `decimal` | Σ `Amount` where `Type = Income` |

**Why Σ debits is correct (FR-012, SC-003).** Every entry balances. An expense debits the expense account (net) and tax receivable `2320` (tax), and credits the bank with the gross. An income entry debits the deposit account with the gross, and credits income (net) and tax collected `2310` (tax). Either way the debit total equals the money that crossed the bank account, counted once. Summing debits and credits together would double-count.

> *Since spec 033 (banking rework):* an income entry debits Petty Cash (cash), Undeposited Funds (cheque or "other") or the chosen bank account (card or electronic transfer), not a free-choice deposit account. The debit total is still the gross received, so Σ debits stays correct. `AccountName` still skips those lines, because Petty Cash is a bank account and Undeposited Funds is a system account (spec 033 FR-001).

Every amount and total is displayed through `MoneyFormatter.Format`, so it uses the society's configured currency symbol and precision (FR-013).

---

## 5. Backup DTOs — `StageFright.Core/Modules/Settings/Backup/`

`BackupSchema.CurrentSchemaVersion` goes `1.2.0` → `1.3.0`, and `BackupService` reads the constant instead of its `"1.2.0"` literal (research §8). `ProtoMember` numbers are append-only.

### `ExternalOrganisationBackupDto` (new), mirrors `ExternalOrganisation` 1:1

| ProtoMember | Field | Type |
|---|---|---|
| 1 | `Id` | `Guid` |
| 2 | `Name` | `string` |
| 3 | `Address` | `string?` |
| 4 | `Email` | `string?` |
| 5 | `IsDeleted` | `bool` |
| 6 | `DeletedAt` | `DateTime?` |
| 7 | `DeletedBy` | `string?` |
| 8 | `CreatedAt` | `DateTime` |
| 9 | `UpdatedAt` | `DateTime` |

### `ExternalOrganisationContactBackupDto` (new), mirrors `ExternalOrganisationContact` 1:1

| ProtoMember | Field | Type |
|---|---|---|
| 1 | `Id` | `Guid` |
| 2 | `ExternalOrganisationId` | `Guid` |
| 3 | `Name` | `string` |
| 4 | `Role` | `string?` |
| 5 | `Phone` | `string?` |
| 6 | `Mobile` | `string?` |
| 7 | `Email` | `string?` |
| 8 | `IsDeleted` | `bool` |
| 9 | `DeletedAt` | `DateTime?` |
| 10 | `DeletedBy` | `string?` |
| 11 | `CreatedAt` | `DateTime` |
| 12 | `UpdatedAt` | `DateTime` |

### Changed DTOs

| Type | ProtoMember | Field | Type | Default when absent from a 1.2.0 file |
|---|---|---|---|---|
| `JournalEntryBackupDto` | 6 | `ExternalOrganisationId` | `Guid?` | `null` (unlinked) |
| `BackupEnvelope` | 27 | `ExternalOrganisations` | `List<ExternalOrganisationBackupDto>?` | `null` → `[]` |
| `BackupEnvelope` | 28 | `ExternalOrganisationContacts` | `List<ExternalOrganisationContactBackupDto>?` | `null` → `[]` |

`BackupSnapshot` gains the two matching lists. `EntityCounts` gains `ExternalOrganisations` and `ExternalOrganisationContacts` so the verified-backup check covers them.

**Read and restore order.** `BackupRepository` reads both new sets with `IgnoreQueryFilters()`, so archived organisations and removed contacts are kept. On restore it upserts organisations, then contacts, then journal entries, because both contacts and journal entries hold an FK to an organisation. A 1.2.0 file restores with an empty register and every journal entry unlinked (FR-015). A 1.3.0 file is refused by a 1.2.0 app, which `BackupSchema.IsRestorable` already does.

---

## 6. Relationships

```text
ExternalOrganisation 1 ──< 0..* ExternalOrganisationContact   (required FK, Restrict)
ExternalOrganisation 1 ──< 0..* JournalEntry                   (optional FK, Restrict, write-once)
JournalEntry         1 ──< 2..3 Transaction                    (existing, unchanged)
```

---

## 7. Validation summary

Service checks raise a localized `ValidationException` (message text in `ValidationResource`). The UI shows each message next to the offending field (FR-004).

| Rule | Applies to | Source |
|---|---|---|
| Name required (after trim) | Organisation, Contact | FR-002, FR-008 |
| Name ≤ 200 | Organisation, Contact | Edge case "Long text" |
| No other active organisation has the same trimmed name, ignoring case | Organisation create, edit, restore | FR-003, edge cases |
| Address ≤ 1000 | Organisation | Edge case "Long text" |
| Email ≤ 254 and valid format when present | Organisation, Contact | FR-004 |
| Role ≤ 100 | Contact | Edge case "Long text" |
| Phone ≤ 50, Mobile ≤ 50, no format check | Contact | Assumptions |
| Contact's organisation exists (query filters ignored) | Contact create | FR-008 |
| Linked organisation exists and is active | Expense and income posting | FR-010, research §6 |
| Link only on `ExpensePayment` and `Income` entries | Journal entry | FR-010 |
| Link is never edited after posting | Journal entry | Clarification 2, §3.5 |

Editing an archived organisation or contact is not offered. The service refuses it with a `ValidationException`, so an archived record must be restored first.

---

## 8. State transitions

### `ExternalOrganisation`

```text
           create                 archive
  (none) ─────────► Active ───────────────► Archived
                    ▲   │                      │
                    │   └── edit (stays Active) │
                    └──────── restore ──────────┘
                     (refused when another active
                      organisation has the same name)
```

| Transition | Effect | Audit (`EntityType = "ExternalOrganisation"`) |
|---|---|---|
| create | New row, `IsDeleted = false` | `Create` |
| edit | `Name`, `Address`, `Email` updated, `UpdatedAt` set | `Update`, with old and new values |
| archive | `IsDeleted = true`, `DeletedAt` and `DeletedBy` set. Contacts and linked journal entries are untouched (research §5). | `Delete` |
| restore | Name re-checked for duplicates, then `IsDeleted = false` and `DeletedAt`/`DeletedBy` cleared | `Restore` |

An archived organisation is hidden from the default list and from the finance pickers. Its detail screen, contacts and financial history stay viewable. An organisation with financial history may be archived.

### `ExternalOrganisationContact`

```text
           add                  remove
  (none) ──────► Active ─────────────────► Removed
                  │
                  └── edit (stays Active)
```

| Transition | Effect | Audit (`EntityType = "ExternalOrganisationContact"`) |
|---|---|---|
| add | New row under one organisation | `Create` |
| edit | Fields updated, `UpdatedAt` set | `Update` |
| remove | `IsDeleted = true`, `DeletedAt` and `DeletedBy` set | `Delete` |

The UI offers no restore for a removed contact, because the spec asks only for removal. The row stays in the data and in backups. Contacts keep their own state when their organisation is archived or restored.

### `JournalEntry.ExternalOrganisationId`

Write-once: `null` or an active organisation's ID at creation, then immutable. Archiving or restoring the organisation never changes it.
