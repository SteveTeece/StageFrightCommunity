# Data Model: AGM Workflow

**Feature**: [spec.md](./spec.md) | **Research**: [research.md](./research.md)

All entities below follow the project-wide conventions confirmed by research: `Guid` PK, `CreatedAt`/`UpdatedAt` audit fields, `IsDeleted`/`DeletedAt`/`DeletedBy` soft-delete fields (per constitution §3.4), one `IEntityTypeConfiguration<T>` file per entity under `src/StageFright.Data/Configurations/`, filtered unique indexes written as `HasFilter("[Column] = 0")` (bracketed names, matching the existing SQLite convention).

---

## New entities

### `AnnualGeneralMeeting`

Replaces "AGM recorded as a generic Event" (FR-002). One row per meeting sitting.

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK |
| `Date` | `DateTime` | Required. The meeting date. |
| `Notes` | `string?` | Optional free text. |
| `GeneralCommitteeSeatCountTarget` | `int?` | Snapshotted from `Settings` at save time (FR-014, clarified) — the target that was in effect *when this AGM was recorded*, never recomputed later. |
| `IsDeleted`, `DeletedAt`, `DeletedBy` | soft-delete | Archiving a past AGM (FR-017) sets these; cascades to its `AgmAttendanceRecord`s and the `CommitteeTerm` it started stays intact (archiving history must not corrupt the committee-term chain). |
| `CreatedAt`, `UpdatedAt` | audit | |

Relationships: owns many `AgmAttendanceRecord` (one per active member at save time); owns exactly one `CommitteeTerm` (the term it starts, via `CommitteeTerm.StartedByAgmId`).

### `AgmAttendanceRecord`

One row per (AGM, member) pair (FR-004).

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK |
| `AnnualGeneralMeetingId` | `Guid` (FK) | Required. |
| `MemberId` | `Guid` (FK) | Required. |
| `Attended` | `bool` | Default `false`. |
| soft-delete + audit fields | | Never independently set by any workflow — immutable once saved (same convention as `AttendanceRecord` for rehearsals, per CLAUDE.md). |

Constraint: `unique index (AnnualGeneralMeetingId, MemberId)`.

### `CommitteeOfficeHolderType`

A configurable position title (FR-012/FR-013).

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK |
| `Name` | `string` | Required, max 100. Seeded rows: "President", "Secretary", "Treasurer". |
| `DisplayOrder` | `int` | Built-ins fixed at `0`, `1`, `2`. Custom titles start at `3+` and may only be reordered among themselves — never given a `DisplayOrder` below `2`, so they can never sort ahead of a built-in (User Story 2 AC4). |
| `IsBuiltIn` | `bool` | `true` for the three seeded rows. Drives "cannot be renamed, reordered ahead of custom titles, or archived" (FR-013) at the service layer. |
| soft-delete + audit fields | | Archiving a custom title (FR-012) sets these; built-in rows can never be archived (service-level guard, not just UI). |

Constraint: `unique index (Name) WHERE IsDeleted = 0` (case-insensitive collation).

### `CommitteeTerm`

The AGM-to-AGM cycle (User Story 3; see research.md D1 for how this auto-supersedes prior terms).

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK |
| `StartedByAgmId` | `Guid` (FK, required) | The AGM that began this term. Every term produced by this feature has exactly one starting AGM — 1:1. |
| `StartDate` | `DateTime` | = `StartedByAgm.Date`. Denormalized for query convenience (avoids a join for every term-boundary check). |
| `EndDate` | `DateTime?` | Null while current/open. Set to the next AGM's date the instant that next AGM is saved (this *is* the "supersede" mechanism — see research D1). |
| `LabelYear` | `int` | Computed once at creation per FR-024 ("majority of days" rule — equivalently, the year following the AGM when it falls July–December, the AGM's own year when January–June) and never recomputed. |
| audit fields | | No soft-delete fields needed beyond the standard ones — a term is never independently archived; it's archived only as a side effect of archiving its starting AGM. |

Relationships: owns many `CommitteePositionRecord`s. Historical (pre-feature) committee data has **no** `CommitteeTerm` row at all — it stays as legacy-shaped `CommitteePositionRecord` rows with `Year`/`Position` populated and `CommitteeTermId` null (research D2).

**State transition**: `Open` (`EndDate == null`) → `Closed` (`EndDate` set), a one-way transition triggered exclusively by the next AGM being saved. No other code path may close or reopen a term.

### `CommitteePositionRecord` (renamed + extended from `CommitteeMembership`)

The elected outcome for one member in one position (or general committee) for one term.

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK (unchanged) |
| `MemberId` | `Guid` (FK) | Unchanged. |
| `Year` | `int?` | **Legacy only.** Populated on pre-feature rows; always `null` on rows this feature creates. |
| `Position` | `string?` | **Legacy only.** Same rule as `Year`. Max 100 (unchanged from today). |
| `CommitteeTermId` | `Guid?` (FK) | **New.** Null on legacy rows; required on every row this feature creates. |
| `OfficeHolderTypeId` | `Guid?` (FK) | **New.** Null = general committee member. Non-null = a specific office-holder title (built-in or custom). |
| `StartDate` | `DateTime?` | **New.** The date this member's service in this slot began (the owning AGM's date, or a special election's replacement date). |
| `EndDate` | `DateTime?` | **New.** Null while this holder is current for the slot. Set only by a special election closing out a departing holder (FR-027) — never set merely because a new AGM/term started (that's handled at the `CommitteeTerm` level, per D1). |
| `IsDeleted`, `DeletedAt`, `DeletedBy` | soft-delete | Unchanged fields, now also used when an AGM is archived (cascades to its own position records) — never used to "remove" a superseded-by-rerun record (those stay fully intact, per the clarified answer). |
| `CreatedAt`, `UpdatedAt` | audit | Unchanged. |

Constraints (new, replacing the old `(MemberId, Year)` unique index — see research D9):
- `unique index (CommitteeTermId, OfficeHolderTypeId) WHERE EndDate IS NULL AND OfficeHolderTypeId IS NOT NULL AND IsDeleted = 0` — one open holder per named position per term.
- `unique index (CommitteeTermId, MemberId) WHERE EndDate IS NULL AND IsDeleted = 0` — one open slot per member per term (FR-008 backstop).

**Display rule (FR-029)**: when more than one (non-deleted) `CommitteePositionRecord` shares a `(CommitteeTermId, OfficeHolderTypeId)` (i.e., a special election occurred), show each holder's name with `StartDate`–`EndDate` (or "–present" if open). When exactly one exists for that slot, show the name alone, no dates.

---

## Entities removed

| Entity/member | Reason |
|---|---|
| `CommitteeAnnualResetService`, `ICommitteeAnnualResetService` | Superseded by atomic AGM-save (FR-018, research D3). |
| `ICommitteeMembershipRepository.SoftDeleteCurrentYearAsync`, `ICommitteeService.SoftDeleteCurrentYearAsync` | Only caller was the removed reset service. |
| `Settings.LastCommitteeResetYear` | Only consumer was `CheckAgmBannerAsync`. Drop the column; remove from `SettingsBackupDto`/`BackupService` mapping. |
| `(MemberId, Year)` unique index on `CommitteeMembershipConfiguration` | Replaced by the two filtered indexes above — the old index enforces the wrong invariant for a term/position-slot model. |

---

## Settings field changes

| Field | Change |
|---|---|
| `CommitteeRenewalMonth` | Repurposed in place as "AGM month" (research D7) — same column, same default (`1`), updated doc comment and UI label only. No migration needed for this field itself. |

---

## Validation rules (cross-entity)

1. **FR-008** — within one AGM save, no `MemberId` may appear in more than one office-holder assignment or as a general-committee selection. Validated in application code before the transaction opens (mirrors `PaymentService.RecordAsync`'s pre-transaction validation), backstopped by the `(CommitteeTermId, MemberId)` filtered unique index.
2. **One holder per office-holder title** — backstopped by the `(CommitteeTermId, OfficeHolderTypeId)` filtered unique index (built-in and custom titles alike, per clarification).
3. **FR-013** — service-layer guard rejects rename/reorder-ahead-of-custom/archive attempts on any `CommitteeOfficeHolderType` where `IsBuiltIn == true`.
4. **FR-026/Edge case** — a special election is rejected with `DataIntegrityException` if the target `CommitteeTerm.EndDate` is not null (term already closed by a later AGM).
5. **FR-009** — AGM save (meeting + attendance + position records) is one `ExecuteInTransactionAsync` call; any failure rolls back all of it.
