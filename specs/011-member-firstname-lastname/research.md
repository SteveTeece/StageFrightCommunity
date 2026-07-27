# Research: Split Member Name into First Name and Last Name

**Feature**: `011-member-firstname-lastname` | **Phase**: 0 (Outline & Research)

This document resolves every open technical question for the feature by grounding it in the
existing codebase (see inventory below) rather than external unknowns — there were no
`NEEDS CLARIFICATION` markers left in Technical Context, so this phase focuses on *how* to
implement each functional requirement consistently with established StageFright patterns.

## Codebase Inventory (baseline)

| Area | File | Current `Name` usage |
|---|---|---|
| Entity | `src/StageFright.Core/Entities/Member.cs:15` | `public string Name { get; set; }` |
| EF mapping | `src/StageFright.Data/Configurations/MemberConfiguration.cs:13` | `.IsRequired().HasMaxLength(255)` |
| Repository | `src/StageFright.Data/Repositories/MemberRepository.cs`, `IMemberRepository.cs` | No `Name`-specific query methods (only `GetByStatusAsync`, `GetActiveAsOfAsync`) — no LINQ-to-SQL translation risk |
| Service | `src/StageFright.Core/Modules/Members/MemberService.cs:46,74` | `CreateAsync`/`UpdateAsync` set `Name = request.Name.Trim()` |
| DTOs | `CreateMemberRequest.cs`, `UpdateMemberRequest.cs` | `public string Name { get; init; }` |
| Validation | `src/StageFright.Core/Modules/Members/MemberValidationService.cs:36-37` | Required-only check (`ValidationException`); no max-length check in code today — length is enforced only by the (unenforced-at-runtime) SQLite column mapping |
| Audit | `MemberService.UpdateAsync` line 85 | `LogAsync("Member", id, AuditAction.Update, ct: innerCt)` — **no old/new value captured today** |
| Audit precedent | `src/StageFright.Core/Modules/Finance/AccountService.cs:99-105` | Captures `oldName`/`trimmed` and passes as `oldValue:`/`newValue:` to `LogAsync` |
| UI form | `src/StageFright.UI/Pages/Members/MemberForm.razor` (+`.razor.cs`) | One "Full Name" input bound to `_form.Name` |
| UI grid | `src/StageFright.UI/Pages/Members/MemberList.razor` | `RadzenDataGridColumn Property="Name"`; search textbox filters via `m.Name?.Contains(_searchTerm, ...)` in `MemberList.razor.cs` |
| UI detail | `src/StageFright.UI/Pages/Members/MemberDetail.razor` | Page title / `<h1>` use `_member.Name` |
| Other UI | `EventDetail.razor`, `ParticipationGrid.razor.cs`, `AttendanceGrid.razor(.cs)`, `MemberBalanceList.razor`, `PaymentForm.razor.cs` | `OrderBy(m => m.Name)`, direct `.Name` display |
| Reports | `MemberListReportProvider.cs`, `MemberAccountSummaryReportProvider.cs`, `CommitteeReportProvider.cs` | Sort by `m.Name`; single "Name" column/label; `CommitteeReportProvider.JoinAlphabetically` (line 172) is the only existing "combine into one string" precedent (joins a list, not two fields) |
| Backup | `src/StageFright.Core/Modules/Settings/Backup/MemberBackupDto.cs` | `[ProtoMember(2)] public string Name` — **protobuf-net wire format, field-number-bound** |
| Backup mapping | `BackupService.cs:272` (`MapMember`), `:383` (`MapMemberFromDto`) | Direct `Name = m.Name` / reverse |
| Migrations | `src/StageFright.Data/Migrations/` | Naming: PascalCase verb phrases, no separators (e.g. `ConvertCategoriesToAccounts`, `AddShowParticipationGraphs`). Closest precedent for a data-transforming migration: `20260705071238_ConvertCategoriesToAccounts.cs` — hand-written, pure `migrationBuilder.Sql()` UPDATE statements, no table rebuild for renames, explicit `Down()` |
| SchemaVersion | `BackupService.cs:19,145` | Restore only checks the **major** component (`SupportedMajorVersion = "1"`); only the one structural GL-conversion migration ever bumped `SchemaVersion` (to `1.1.0`) — every other migration since (`AddGst`, `AddAbnToSettings`, `AddJournalEntries`, `AddBankReconciliation`, `AddShowParticipationGraphs`) left it untouched |
| Tests | 29 files across 5 test projects reference `Member.Name` (full list enumerated per-project in tasks.md) |

## Decisions

### 1. Where the FirstName/LastName split lives: entity, not a value object

**Decision**: Add `FirstName` and `LastName` as plain `string` properties directly on `Member`,
plus a computed, read-only `FullName` property (not mapped by EF) that returns
`$"{FirstName} {LastName}".Trim()`.

**Rationale**: The codebase has no precedent for value objects on entities (`Member.cs` today is
flat scalar properties only, consistent with constitution §4.5's file-per-type rule — a value
object would need its own file for marginal benefit). A computed `FullName` property mirrors how
`CommitteeReportProvider` already builds ad-hoc display strings, and keeps `Member` a plain EF
entity — `MemberConfiguration` uses Fluent `Configure()`, not attributes, and EF Core simply
never maps a get-only computed property that has no `Property()` call and no backing column, so
no explicit `[NotMapped]` exclusion is even required.

**Alternatives considered**:
- A `PersonName` value object (owned type) — rejected: no other entity in the codebase uses
  EF owned types; would be the first of its kind for a two-field concept, adding conceptual
  overhead the constitution's "prefer clarity" principle (§3.1) doesn't ask for here.
- Computing `FullName` in each consumer (UI/report) via string interpolation instead of a shared
  property — rejected: would duplicate the `"{First} {Last}"` vs `"{Last}, {First}"` formatting
  logic (FR-005) across ~10 call sites; a single property (plus a second helper for the sorted
  format, see Decision 2) is the DRY choice and matches the existing `PaymentService` pattern of
  building one `memberName` local variable before interpolating it.

### 2. Two display formats, not one

**Decision**: `Member.FullName` returns `"{FirstName} {LastName}"` (entry/detail order, FR-005's
second clause) for the Add/Edit form confirmation and Member Detail header. A second helper —
`Member.SortableFullName` — returns `"{LastName}, {FirstName}"` for every sorted list/report/search
context (Member List grid, all three report providers, dashboard/attendance/participation
displays where they show a name inline in a list).

**Rationale**: FR-005 explicitly requires both orders in different places; giving them distinct,
named properties (rather than a boolean flag parameter) keeps call sites self-documenting and
avoids a stringly-typed formatting flag, consistent with "avoid cleverness; prefer clarity" (§3.1).

**Alternatives considered**: A single `FullName` with a `MemberNameFormat` enum parameter —
rejected as over-engineered for two fixed formats used in well-known, enumerable places.

### 3. Conversion algorithm: pure SQL inside the migration, not a C# runtime backfill

**Decision**: Implement FR-006's algorithm (trim → collapse internal whitespace → split on first
space → truncate to 100 chars per field) entirely as SQLite string-function SQL inside the
migration's `Up()`, following the same `migrationBuilder.Sql(...)` idiom as
`ConvertCategoriesToAccounts`:

```sql
-- 1. Add nullable staging columns
ALTER TABLE Members ADD COLUMN FirstName TEXT NULL;
ALTER TABLE Members ADD COLUMN LastName TEXT NULL;

-- 2. Normalize: trim outer whitespace
UPDATE Members SET Name = TRIM(Name);

-- 3. Collapse runs of internal spaces to one (repeat: each pass ~halves the longest run,
--    so 10 passes safely converges for any realistic name length up to 1024 consecutive spaces)
UPDATE Members SET Name = REPLACE(Name, '  ', ' ') WHERE Name LIKE '%  %';
-- ...repeated 10x total...

-- 4. Split on first space; truncate each side to 100 chars
UPDATE Members SET
  FirstName = CASE WHEN INSTR(Name, ' ') = 0
                    THEN SUBSTR(Name, 1, 100)
                    ELSE SUBSTR(SUBSTR(Name, 1, INSTR(Name, ' ') - 1), 1, 100) END,
  LastName  = CASE WHEN INSTR(Name, ' ') = 0
                    THEN ''
                    ELSE SUBSTR(SUBSTR(Name, INSTR(Name, ' ') + 1), 1, 100) END;

-- 5. Enforce NOT NULL now that every row is populated, then drop the old column
--    (EF Core's SQLite provider performs the required table rebuild automatically
--    for AlterColumn/DropColumn — no manual rebuild needed, matching how
--    ConvertCategoriesToAccounts let native ALTERs handle simpler cases)
```

**Rationale**: SQLite's `TRIM`, `REPLACE`, `INSTR`, and `SUBSTR` are sufficient to express the
entire FR-006 rule declaratively, matching this codebase's established convention (pure-SQL data
migrations, e.g. the `printf`-based account renumbering in `ConvertCategoriesToAccounts`) instead
of introducing a new pattern. EF Core's base `Migration` class only *accumulates*
`MigrationOperation`s in `Up()` — it has no live connection to run row-by-row C# logic against
existing data mid-migration — so a pure-SQL script is the only idiomatic way to do this **inside**
a single migration; anything else would require a separate runtime backfill step, which the next
decision explicitly rejects.

**Alternatives considered**:
- **Row-by-row C# loop via a raw ADO.NET connection inside `Up()`** — not supported by the
  standard `Migration` base class's execution model (see rationale above); would require bypassing
  `MigrationBuilder` entirely with custom `IMigrationsSqlGenerator` machinery, a much larger and
  riskier change with no precedent in this codebase.
- **Application-startup backfill service** (add nullable columns via migration, then a
  one-time startup step reads every `Member`, computes the split in C#, writes back, and a
  *second* migration later drops `Name`) — rejected: splits an atomic, testable data transform
  across two deploy artifacts (migration + runtime code), risks the app starting up with a
  partially-converted database if interrupted, and contradicts FR-006's "as part of the upgrade"
  requirement. The pure-SQL migration keeps the whole conversion inside one atomic,
  automatically-transacted EF Core migration.
- **Regex-based collapse in SQL** — SQLite has no built-in regex function without loading an
  extension; rejected as an unnecessary new runtime dependency when repeated `REPLACE` achieves
  the same normalized result deterministically.

### 4. A C#-side `MemberNameSplitter` utility is still needed — for tests and backup restore, not the migration

**Decision**: Add a small, pure, static utility (`src/StageFright.Core/Modules/Members/MemberNameSplitter.cs`)
implementing the *identical* FR-006 rule in C# (`Split(string combinedName) -> (string FirstName,
string LastName)`), even though the migration itself uses SQL, not this utility.

**Rationale**: Two independent consumers need the same rule in C#, not SQL:
1. **Legacy backup restore** (Decision 6 below) — `BackupService` restores protobuf blobs, not
   SQL rows, so it needs a C# implementation to convert any pre-feature backup's lone `Name`
   field on restore.
2. **Fast, isolated unit tests** for FR-006/FR-008's edge cases (mononym, multi-word, irregular
   spacing, >100-char overflow) are far cheaper and clearer as pure-function unit tests
   (`tests/StageFright.Core.Tests/Modules/Members/MemberNameSplitterTests.cs`) than as
   SQLite-integration tests of the migration alone. The migration still gets its own
   `StageFright.Data.Tests` integration test (seed legacy `Name` rows, run the migration, assert
   the resulting columns) to verify the **SQL** implementation matches the **same** expected
   outputs — the two implementations must be kept in lock-step by testing both against the same
   edge-case input/output table.

**Alternatives considered**: Skipping the C# utility and only testing via full-migration
integration tests — rejected: would leave the backup-restore compatibility gap (Decision 6)
unaddressed, and makes edge-case coverage (§11.0's exhaustive-path rule) slower and harder to
read than dedicated unit tests.

### 5. Search and sort: extend the existing in-memory patterns, no repository change

**Decision**: `MemberList.razor.cs`'s `DisplayMembers` filter extends from
`m.Name?.Contains(_searchTerm, ...)` to check `FirstName`, `LastName`, and the computed
`FullName`. `RadzenDataGridColumn` sorting and all `OrderBy(m => m.Name)` call sites (report
providers, `ParticipationGrid`, `AttendanceGrid`) switch to
`OrderBy(m => m.LastName).ThenBy(m => m.FirstName)` (FR-005). The Member List grid's single
"Name" column becomes a `Property="SortableFullName"` (via its existing custom `<Template>` that
already handles the inactive-suffix) so native grid click-to-sort still works without introducing
a repository-level sort.

**Rationale**: `IMemberRepository`/`MemberRepository` have **no** `Name`-specific query methods
today (confirmed in inventory) — all name search/sort already happens in-memory after
`GetByStatusAsync`/`GetActiveAsOfAsync` load members into a list. This means there is no
LINQ-to-SQL translation risk to worry about for `FirstName`/`LastName`/computed properties; the
existing in-memory-filter architecture is preserved unchanged in shape, just extended to two
source fields.

**Alternatives considered**: Pushing search/sort into the repository via new
`SearchByNameAsync`/`IOrderedQueryable` methods — rejected as unnecessary scope expansion; no
existing requirement calls for server-side (SQL) filtering, and the dataset size (tens–low
hundreds of members, per Technical Context) doesn't need it.

### 6. Backup/restore compatibility: repurpose the protobuf slot, add two new ones, convert on restore

**Decision**: In `MemberBackupDto`, keep `[ProtoMember(2)]` but rename the C# property from
`Name` to `LegacyName` (protobuf-net binds by field **number**, not property name, so this is
wire-compatible with every backup ever taken). Add `[ProtoMember(16)] public string FirstName`
and `[ProtoMember(17)] public string LastName` (next free numbers after the existing 1–15).
`BackupService.MapMember` (export) populates `FirstName`/`LastName` only, leaving `LegacyName`
blank. `BackupService.MapMemberFromDto` (restore) checks: if `FirstName`/`LastName` are both
empty and `LegacyName` is non-empty (i.e., this is a pre-feature backup), apply
`MemberNameSplitter.Split(LegacyName)` before constructing the `Member`; otherwise use
`FirstName`/`LastName` directly.

**Rationale**: `BackupService` only gates restore compatibility on the backup's **major** schema
version (`SupportedMajorVersion = "1"`, `BackupService.cs:19,145`) — this feature doesn't need to
bump that major version, so old-format backups (`Name`-only) remain restorable and must still
produce correct, non-empty `FirstName`/`LastName` data (consistent with FR-007's "no data lost"
guarantee extending to the backup/restore path, not just the live-upgrade path). Reusing the
already-researched split rule via the shared `MemberNameSplitter` (Decision 4) avoids a second,
divergent implementation.

**Alternatives considered**:
- **Bump `SupportedMajorVersion` to "2" and refuse old backups** — rejected: far more disruptive
  than necessary (blocks restoring any pre-upgrade backup at all) for what is a purely additive,
  non-breaking backup-schema change; no functional requirement calls for backup incompatibility.
- **Leave `MemberBackupDto.Name` as-is and add FirstName/LastName as derived, unmapped
  properties** — rejected: protobuf-net requires `[ProtoMember]` on every field that must
  round-trip; an unmapped property wouldn't serialize on export, breaking new backups.

### 7. Validation: extend `MemberValidationService`'s existing manual-check style

**Decision**: Replace the single `Name` required check with required + max-100-length checks for
`FirstName` and `LastName` independently (FR-002, FR-009), following the exact
`ValidationException("...", "Member", operationContext)` shape already used for `Name` and
`StreetAddress` — no new validation framework introduced (the codebase has no FluentValidation
dependency; all validation in `MemberValidationService` is hand-written `if` checks).

**Rationale**: Matches existing style exactly; §3.1 "consistency across the codebase is
mandatory."

**Alternatives considered**: Adding a `[MaxLength(100)]` data-annotation and relying on
model-binding validation — rejected: the codebase's validation for `Member` is entirely
service-layer (`MemberValidationService`), not attribute-based; introducing attributes here would
be an inconsistent, one-off pattern. EF's `HasMaxLength(100)` mapping (Decision 8 area, see
data-model.md) does not itself enforce length at the SQLite level (SQLite ignores column length
limits at runtime), so a code-level check is required regardless — the manual
`MemberValidationService` check is both necessary and sufficient, and stays consistent with the
rest of the module.

### 8. Audit trail: add old/new value capture to `MemberService.UpdateAsync` (currently absent)

**Decision**: Before mutating `member.FirstName`/`member.LastName` in `UpdateAsync`, capture
`oldFirstName`/`oldLastName`, then pass a combined descriptive string (e.g.
`oldValue: $"{oldFirstName} {oldLastName}"`, `newValue: $"{request.FirstName} {request.LastName}"`)
to `_audit.LogAsync`, following `AccountService.UpdateAsync`'s `oldValue:`/`newValue:` pattern
(`AccountService.cs:99-105`) — the first time `MemberService` does this for any field (today's
`LogAsync("Member", id, AuditAction.Update, ct: innerCt)` call passes neither).

**Rationale**: FR-011 requires FirstName/LastName edits to be tracked "the same way other member
field edits are tracked today" — but today's `MemberService.UpdateAsync` doesn't capture *any*
field's old/new values, so this is a gap the feature must close, using the codebase's one
existing precedent (`AccountService`) as the template rather than inventing a new logging shape.
`AuditTrailEntry`/`AuditTrailService.LogAsync` store free-text strings with no structured
diff/reflection convention anywhere in the codebase, so a single combined descriptive string
(not JSON) matches every other `LogAsync` call site.

**Alternatives considered**: Two separate `LogAsync` calls (one for FirstName, one for LastName)
— rejected: no precedent for multi-field-per-update splits into multiple audit rows in this
codebase; a single combined-value string matches `AccountService`'s one-call-per-update shape and
keeps the audit history one entry per save operation.

### 9. SchemaVersion: no bump

**Decision**: Do not change `Settings.SchemaVersion` in this migration.

**Rationale**: Of the seven migrations since `InitialCreate`, only the one that restructured
`Categories` into a full chart-of-accounts (`ConvertCategoriesToAccounts`) bumped it; every purely
additive/structural change since (`AddGst`, `AddAbnToSettings`, `AddJournalEntries`,
`AddBankReconciliation`, `AddShowParticipationGraphs`) left it untouched. This feature is the same
shape as those — an additive/structural entity change, not a GL-semantics change — and, per
Decision 6, doesn't need a backup-compatibility major-version bump either.

**Alternatives considered**: Bump to `1.2.0` "to be safe" — rejected: would be inconsistent with
the established precedent that only breaking-schema-semantics migrations bump the version, and
`BackupService` doesn't check the minor component at all today, so it would have no functional
effect beyond deviating from convention.

## Outstanding Risks (carried into tasks.md, not blocking)

- **Migration idempotency under repeated `Up()`/`Down()` cycles during development** — the
  hand-written SQL must be safe to re-run against a dev DB that already has `FirstName`/`LastName`
  populated; `Down()` must restore a single `Name` column via a
  `TRIM(FirstName || ' ' || LastName)`-style concatenation for round-trip testing.
- **`INSTR`/`SUBSTR` are 1-indexed and `SUBSTR(x, n)` with `n` beyond the string's length returns
  `''` safely in SQLite** — matches documented SQLite behavior, but the Data integration test
  (Decision 4) must explicitly assert the mononym case (FR-008) doesn't error.
