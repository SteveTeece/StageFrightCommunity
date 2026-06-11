# Data Model: StageFright Community — Initial MVP

**Branch**: `001-initial-mvp` | **Date**: 2026-06-11 | **Plan**: [plan.md](./plan.md)

All entities live in `StageFright.Core/Entities` (one type per file) and are persisted by the centralized DAL (`StageFright.Data`, FR-042). Money fields are `decimal` with 2+ place precision (research.md R10). Timestamps are UTC unless noted.

## Conventions

- **Soft-delete fields** (`IsDeleted` bool, `DeletedAt` DateTime?, `DeletedBy` string?): present on every entity **except Fee, Payment, Transaction** (Constitution §3.4 financial exemption — those entities have NO soft-delete fields at all, per spec §6 Pass #3 Q5).
- **Audit fields**: `CreatedAt` (DateTime, set on insert) on all entities; `UpdatedAt` where noted.
- **Keys**: `Id` (GUID) primary key on all entities. Import/restore upserts match on PK only.

---

## Member

| Field | Type | Rules |
|-------|------|-------|
| Id | Guid | PK |
| Name | string (≤255) | Required |
| StreetAddress | string | Required |
| Phone | string? | Optional; format validated when provided |
| Email | string? | Optional; format validated when provided |
| JoinDate | DateTime | Required |
| DateOfBirth | DateTime? | Optional; must be < today (UTC), within Settings.MaxAgeRangeYears (default 150); resulting age ≥ Settings.MinimumMemberAge |
| Status | MemberStatus enum {Active, Inactive} | Default Active on create |
| ActivateDate | DateTime? | System-set (today) on Inactive→Active transition; immutable; set on create (default Active) |
| InactivateDate | DateTime? | System-set (today) on Active→Inactive transition; immutable |
| IsDeleted / DeletedAt / DeletedBy | soft-delete | Set only on explicit archival, never on inactivation |
| CreatedAt / UpdatedAt | DateTime | — |

- **Age** is a calculated property (UTC, leap-year-aware algorithm per FR-002a) — never persisted; only displayed when DateOfBirth present.
- **State transitions**:
  - Create → `Status=Active`, `ActivateDate=today`.
  - Active → Inactive: set `InactivateDate=today`; audit entry. No fee impact.
  - Inactive → Active (reactivation): set `ActivateDate=today`; trigger Reactivation Forgiveness dialog → GL write-off pairs for selected fees (FR-024); committee history unaffected.
  - Archive (either status): `IsDeleted=true` + cascade soft-delete of the member's CommitteeMembership records (spec clarification); Fees/Payments/Transactions referencing the member are untouched.
  - Restore: `IsDeleted=false`.
- **Query patterns**: Active = `Status='Active' AND IsDeleted=0`; Inactive = `Status='Inactive' AND IsDeleted=0`; Archived = `IsDeleted=1`. Historical active-as-of-date D = `Status='Active' AND ActivateDate <= D AND (InactivateDate IS NULL OR InactivateDate > D) AND IsDeleted=0`.
- **Relationships**: 1→N CommitteeMembership, AttendanceRecord, ParticipationRecord, Fee, Payment, Transaction (member reference).

## CommitteeMembership

| Field | Type | Rules |
|-------|------|-------|
| Id | Guid | PK |
| MemberId | Guid | FK → Member, required |
| Year | int | Calendar year |
| Position | string (≤100) | Required when record exists |
| IsDeleted / DeletedAt / DeletedBy | soft-delete | Soft-deleted in cascade when member archived; cleared (soft-deleted) for current year by annual reset |
| CreatedAt / UpdatedAt | DateTime | — |

- **Unique constraint**: (MemberId, Year) — one committee assignment per member per year.
- Annual reset (FR-031, manual button): soft-deletes current-year records for all members, sets `Settings.LastCommitteeResetYear`, writes audit entries. Prior years are read-only history.

## Rehearsal

| Field | Type | Rules |
|-------|------|-------|
| Id | Guid | PK |
| Date | DateTime | Required |
| Time | TimeSpan | Required |
| Notes | string? | Optional |
| StoredAttendanceRate | decimal? | Calculated once at attendance recording (FR-007); immutable thereafter; null until attendance recorded |
| IsDeleted / DeletedAt / DeletedBy | soft-delete | — |
| CreatedAt / UpdatedAt | DateTime | — |

- **Relationships**: 1→N AttendanceRecord.
- Rate formula: members present ÷ members active-as-of `Date` × 100; archived members always excluded from denominator; never recalculated.

## AttendanceRecord *(immutable after batch save)*

| Field | Type | Rules |
|-------|------|-------|
| Id | Guid | PK |
| RehearsalId | Guid | FK → Rehearsal |
| MemberId | Guid | FK → Member |
| Attended | bool | From batch checkbox grid |
| CreatedAt | DateTime | — |
| IsDeleted / DeletedAt / DeletedBy | soft-delete | Present per Constitution §3.4 (non-financial entity) but never set by any MVP workflow — attendance is permanently immutable (§1.1 Q3); corrections via manual GL reversals only |

- **Unique constraint**: (RehearsalId, MemberId).
- Batch creation is atomic: all members' records + auto-created attendance Fees (+ GL pairs) in one transaction.
- Active members with `Attended=true` → Fee created (`PaidAtCreation=true` unless "Mark as unpaid" checked). Inactive members may be recorded but create NO fee.

## Event

| Field | Type | Rules |
|-------|------|-------|
| Id | Guid | PK |
| Date | DateTime | Required |
| EventTypeId | Guid | FK → EventType, required |
| Notes | string? | Optional |
| StoredParticipationRate | decimal? | Calculated once at participation recording; immutable; null until recorded |
| IsDeleted / DeletedAt / DeletedBy | soft-delete | — |
| CreatedAt / UpdatedAt | DateTime | — |

- **Relationships**: 1→N ParticipationRecord; N→1 EventType.
- Events never create fees (FR-006). AGM is an EventType; AGM events drive the committee-reset reminder banner (FR-031).

## EventType

| Field | Type | Rules |
|-------|------|-------|
| Id | Guid | PK |
| Name | string | Required, unique among non-deleted |
| IsSystemDefault | bool | Seeded defaults: Performance, Eisteddfod, Fund raiser, Promotional, Annual General Meeting |
| IsDeleted / DeletedAt / DeletedBy | soft-delete | Archive blocked while referenced by non-deleted Events |
| CreatedAt / UpdatedAt | DateTime | — |

## ParticipationRecord

| Field | Type | Rules |
|-------|------|-------|
| Id | Guid | PK |
| EventId | Guid | FK → Event |
| MemberId | Guid | FK → Member |
| Participated | bool | — |
| CreatedAt | DateTime | — |
| IsDeleted / DeletedAt / DeletedBy | soft-delete | — |

- **Unique constraint**: (EventId, MemberId).

## Fee *(immutable financial record — NO soft-delete fields)*

| Field | Type | Rules |
|-------|------|-------|
| Id | Guid | PK |
| MemberId | Guid | FK → Member, required |
| FeeType | FeeType enum {Annual, Attendance, Other} | Required |
| Amount | decimal(18,2) | Required, immutable |
| FeeDate | DateTime | Annual = Jan 1 of year; Attendance = rehearsal date; immutable |
| DueDate | DateTime | Annual = Dec 31 of year; Attendance = rehearsal date; Other = CreatedAt + 30 days unless specified; immutable |
| PaidAtCreation | bool | Attendance default **true** (override checkbox → false); Annual default **false**; immutable metadata — GL is payment truth |
| RehearsalId | Guid? | FK → Rehearsal for attendance fees (supports idempotency check: one fee per member+rehearsal) |
| CreatedAt | DateTime | FIFO tiebreaker |

- **No updates, no deletes, ever.** All corrections/forgiveness via GL reversing pairs (debit MemberReceivable, credit BadDebtExpense GL#9900 for write-offs).
- Creation always paired with GL transactions in one ACID transaction: Debit MemberReceivable / Credit applicable Income category; if `PaidAtCreation=true`, additionally Debit Cash / Credit MemberReceivable.
- **Outstanding balance is never read from Fee** — derived from GL: `Σ(member debits) − Σ(member credits)`.

## Payment *(immutable except Notes — NO soft-delete fields)*

| Field | Type | Rules |
|-------|------|-------|
| Id | Guid | PK |
| MemberId | Guid | FK → Member, required |
| Date | DateTime | Required, immutable |
| Amount | decimal(18,2) | Required, immutable |
| PaymentMethod | enum {Cash, Check, Card, ElectronicTransfer, Other} | Default Cash; immutable |
| PaymentType | enum {Annual, Attendance, Other} | Required; reporting metadata, distinct from GL Category; immutable |
| Notes | string? | **Only editable field**; edits audited (old/new values) |
| CreatedAt | DateTime | — |
| UpdatedAt | DateTime | Changes ONLY when Notes changes; `UpdatedAt ≠ CreatedAt` ⇒ Notes was edited |

- Repository rejects updates to any locked field with `ValidationException` ("Payment fields are immutable after creation; only Notes may be edited").
- Recording a payment creates GL pairs by FIFO allocation (FR-016): oldest unpaid fee first (FeeDate ASC, CreatedAt ASC, Id ASC); partial allocation supported; overpayment → member credit GL pair.

## Transaction *(General Ledger entry — immutable, NO soft-delete fields)*

| Field | Type | Rules |
|-------|------|-------|
| Id | Guid | PK |
| Date | DateTime | Required |
| CategoryId | Guid | FK → Category, required (implies GL account) |
| DebitAmount | decimal(18,2) | ≥ 0; exactly one of Debit/Credit is non-zero per row |
| CreditAmount | decimal(18,2) | ≥ 0 |
| GLAccount | string | Derived at creation from Category (e.g., "0100" Cash, "0101" MemberReceivable, "10xx" income, "20xx" expense, "9900" write-off); stored denormalized, immutable |
| MemberId | Guid? | FK → Member when applicable |
| PaymentId | Guid? | FK → Payment when created by a payment |
| FeeId | Guid? | FK → Fee when created by fee assignment/write-off |
| Description | string? | Reversals MUST state what was reversed and why (Constitution §3.6) |
| CreatedAt | DateTime | — |

- **Paired-entry invariant**: every financial operation creates exactly two rows with equal amounts (one debit, one credit), committed atomically with GL-balance verification (`Σdebits = Σcredits` within 0.01) — `GLBalanceException` + rollback on failure.
- Source of truth for all balances, Trial Balance, aging, and report data.

## Category

| Field | Type | Rules |
|-------|------|-------|
| Id | Guid | PK |
| Name | string | Required |
| Type | CategoryType enum {Income, Expense} | Set at creation, immutable (drives GL range) |
| GLAccount | string | Auto-assigned by GLAccountAssignmentService: Income → 1000+, Expense → 2000+, sequential by CreatedAt ASC; immutable |
| SortOrder | int | User reorderable |
| IsSystem | bool | True for seeded system categories (see below) — cannot be edited/archived |
| IsDeleted / DeletedAt / DeletedBy | soft-delete (= archive) | Archive **blocked** if ANY Transaction references the category |
| CreatedAt / UpdatedAt | DateTime | — |

- **System-seeded at first-run setup** (non-editable, non-archivable): `Cash` (GL#0100, Asset), `MemberReceivable` (GL#0101, Asset), `BadDebtExpense` (GL#9900, write-off). *(Asset/write-off accounts are modeled as system categories so every Transaction has a CategoryId; their GLAccount values are fixed, outside the 10xx/20xx user ranges.)*

## Settings *(singleton row)*

| Field | Type | Rules |
|-------|------|-------|
| Id | Guid | PK (single row enforced by service) |
| OrganizationName | string | Required (setup wizard) |
| AnnualFee | decimal(18,2) | Required |
| AttendanceFee | decimal(18,2) | Required |
| MembershipRenewalMonth | int (1–12) | Required |
| CommitteeRenewalMonth | int (1–12) | Default 1 (January) |
| MaxAgeRangeYears | int | Default 150 |
| MinimumMemberAge | int | Default 0 (no minimum) |
| Theme | enum {Light, Dark} | Default Light; persisted toggle |
| LastCommitteeResetYear | int? | Set by manual committee reset; drives AGM banner |
| SchemaVersion | string (semver) | Written by migrations/backup |
| IsDeleted / DeletedAt / DeletedBy | soft-delete | Never set (singleton) |
| CreatedAt / UpdatedAt | DateTime | — |

## AuditTrailEntry

| Field | Type | Rules |
|-------|------|-------|
| Id | Guid | PK |
| EntityType | string | e.g. "Member", "Payment" |
| EntityId | Guid | — |
| Action | enum {Create, Update, Delete, Restore, StatusChange, Forgiveness, CommitteeReset, Import, Export} | — |
| OldValue | string? | JSON snapshot of changed fields |
| NewValue | string? | JSON snapshot |
| UserId | string | Fixed "system" in MVP |
| Timestamp | DateTime | — |

- **Retention**: 12 months; purged at startup only (hard delete permitted — log-record exemption, Constitution §3.4); purge failure logs structured error and startup continues.
- No soft-delete fields (it IS the audit log; retention policy governs).

---

## Relationship diagram (logical)

```text
Member 1──N CommitteeMembership
Member 1──N AttendanceRecord N──1 Rehearsal
Member 1──N ParticipationRecord N──1 Event N──1 EventType
Member 1──N Fee 1──N Transaction (FeeId)
Member 1──N Payment 1──N Transaction (PaymentId)
Category 1──N Transaction
Settings (singleton)        AuditTrailEntry (references by EntityType+EntityId)
```

## Validation rules summary (enforced in Core services, surfaced as `ValidationException`)

1. Member: required name/address/join date; email/phone format; DOB past + range + minimum-age (FR-002a messages verbatim).
2. CommitteeMembership: Position required when committee box checked; (Member, Year) uniqueness.
3. Fee/Payment/Transaction: amounts > 0, 2-decimal precision; immutability enforced at repository level; paired GL balance verified pre-commit.
4. Category: archive blocked while referenced (including by any transaction — transactions are never deleted, so any historical reference blocks permanently); system categories locked.
5. Settings: months 1–12; fees ≥ 0; all setup-wizard fields mandatory.
6. Import: schema major-version compatible; all 10 entity types present (Members, Rehearsals, Events, Fees, Payments, Transactions, Categories, Settings, CommitteeMembership, AuditTrail) else `ImportException` "Import file incomplete: missing {entity_type}…".
