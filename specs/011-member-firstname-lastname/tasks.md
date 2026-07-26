---

description: "Task list template for feature implementation"
---

# Tasks: Split Member Name into First Name and Last Name

**Input**: Design documents from `/specs/011-member-firstname-lastname/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/member-service-contract.md](./contracts/member-service-contract.md), [quickstart.md](./quickstart.md)

**Tests**: Included and mandatory — CLAUDE.md's "Exhaustive code-path test coverage" rule is non-negotiable for this repository, and the plan's Constitution Check (§11) requires every consumer touched by this change to keep its test coverage green.

**Organization**: Tasks are grouped by user story (US1/US2/US3, matching spec.md's P1/P2/P3 priorities). Because this feature removes `Member.Name` outright (a breaking rename, not an additive field), the solution will not compile again once Phase 2 lands until **every** consumer across all three stories has been updated — this is the ripple explicitly called out in plan.md's Constitution Check and research.md's inventory. Story grouping below is therefore for **traceability and independent test-verification**, not literal incremental compilability; treat each story's checkpoint as "this slice of behavior is correct," not "this slice alone builds."

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- Paths are relative to the repository root (`c:\SourceCode\StageFrightCommunity`)

---

## Phase 1: Setup

**Purpose**: Establish a clean, known-green baseline before touching a shared entity that ~30 source files and ~30 test files depend on.

- [ ] T001 Run `dotnet restore`, `dotnet build`, and `dotnet test` from the repo root and confirm all five test projects are currently green, so any later failure is attributable to this feature's changes

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Change the shape of `Member` and its create/update request DTOs, and fix the one place that constructs demo data from the old shape. Every user story phase below depends on this shape existing; nothing in Phase 3+ (nor `dotnet run` for manual quickstart validation) will compile until it lands.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [ ] T002 [P] Replace `Name` with `FirstName`, `LastName` (both `string`, required, max 100) and add computed read-only `FullName` (`"{FirstName} {LastName}".Trim()`, entry-order) and `SortableFullName` (`"{LastName}, {FirstName}"`, or just one side when the other is empty) properties on `src/StageFright.Core/Entities/Member.cs`
- [ ] T003 [P] Update `src/StageFright.Data/Configurations/MemberConfiguration.cs`: replace `builder.Property(m => m.Name).IsRequired().HasMaxLength(255)` with `builder.Property(m => m.FirstName).IsRequired().HasMaxLength(100)` and `builder.Property(m => m.LastName).IsRequired().HasMaxLength(100)` (no `builder.Property()` call for `FullName`/`SortableFullName` — they stay unmapped)
- [ ] T004 [P] Replace `Name` with `FirstName` and `LastName` (both `string, init, default ""`) on `src/StageFright.Core/Modules/Members/CreateMemberRequest.cs`
- [ ] T005 [P] Replace `Name` with `FirstName` and `LastName` (both `string, init, default ""`) on `src/StageFright.Core/Modules/Members/UpdateMemberRequest.cs`
- [ ] T006 [P] Update `src/StageFright.App/Seeding/DebugDataSeeder.cs` (opt-in dev/demo data seeded after the Setup Wizard completes, gated behind its "seed debug data" checkbox — see `IDebugDataSeeder`): `CreateMembersAsync`'s literal 51-row tuple array (currently `(string Name, string Address, string Phone, string Email, DateTime JoinDate, DateTime DateOfBirth)[]`, each `Name` a "First Last" pair, lines 190-246) becomes `(string FirstName, string LastName, string Address, string Phone, string Email, DateTime JoinDate, DateTime DateOfBirth)[]` with every row's existing name split into its two words; the `CreateMemberRequest` construction (lines 251-259) changes from `Name = name` to `FirstName = firstName, LastName = lastName`. Not covered by an existing automated test (`SetupWizardTests.cs`/`SetupWizardNoSeederTests.cs` mock `IDebugDataSeeder` and never exercise its data) — verify manually per T060 by running the Setup Wizard with the seed-data option enabled and confirming all 51 members show correct, distinct First/Last names on Member List

**Checkpoint**: `Member.Name` no longer exists. Every file below that referenced it is now a compile error until its task is done — the phases below can be done in any order relative to each other, but the solution stays red until all of them are complete.

---

## Phase 3: User Story 1 - Enter first and last name separately (Priority: P1) 🎯 MVP

**Goal**: Add/Edit Member captures First Name and Last Name as two independent, independently-validated fields; Member Detail shows the entry-order full name; edits are captured in the audit trail.

**Independent Test**: Open Add Member, enter distinct First Name/Last Name values, save, and confirm both persist and redisplay correctly (including on Edit Member pre-population and the Member Detail header); confirm saving with either field blank is rejected with a validation message.

### Tests for User Story 1

- [ ] T007 [P] [US1] Update `tests/StageFright.Core.Tests/Modules/Members/MemberValidationServiceTests.cs`: replace the single required-`Name` case with four independent cases — `FirstName` required, `LastName` required, `FirstName` > 100 chars rejected, `LastName` > 100 chars rejected — for both `CreateMemberRequest` and `UpdateMemberRequest`
- [ ] T008 [P] [US1] Update `tests/StageFright.Core.Tests/Modules/Members/MemberServiceTests.cs`: `CreateAsync`/`UpdateAsync` cases now assert `FirstName`/`LastName` are trimmed and mapped independently, and add a case asserting `UpdateAsync` calls `_audit.LogAsync` with `oldValue`/`newValue` capturing the pre-update `FirstName`/`LastName` (new behavior — today's call passes neither, per research.md Decision 8)
- [ ] T009 [P] [US1] Update `tests/StageFright.UI.Tests/Pages/Members/MemberFormTests.cs`: assert two separate First Name/Last Name inputs exist (no single "Name" input), each shows its own validation message when blank, and Edit Member pre-populates both fields independently
- [ ] T010 [P] [US1] Update `tests/StageFright.UI.Tests/Pages/Members/MemberDetailTests.cs`: assert the page title and `<h1>` render `_member.FullName` (`"{FirstName} {LastName}"` order)
- [ ] T011 [P] [US1] Update `tests/StageFright.Integration.Tests/Scenarios/V2_MemberManagementTests.cs`: full create/edit journey now goes through separate First Name/Last Name fields end-to-end

### Implementation for User Story 1

- [ ] T012 [US1] Update `src/StageFright.Core/Modules/Members/MemberValidationService.cs`: change `ValidateCommon` to take `firstName`/`lastName` instead of `name`, throwing `ValidationException("First name is required.", "Member", operationContext)` / `"Last name is required."` for blank values and `"First name must be 100 characters or fewer."` / `"Last name must be 100 characters or fewer."` for over-length values (after trim); update both `Validate(CreateMemberRequest, ...)` and `Validate(UpdateMemberRequest, ...)` call sites accordingly
- [ ] T013 [US1] Update `src/StageFright.Core/Modules/Members/MemberService.cs`: `CreateAsync` sets `FirstName = request.FirstName.Trim()`, `LastName = request.LastName.Trim()` in place of `Name = request.Name.Trim()` (line 46); `UpdateAsync` captures `oldFirstName`/`oldLastName` before mutating (replacing line 74), then passes `oldValue: $"{oldFirstName} {oldLastName}"`, `newValue: $"{request.FirstName} {request.LastName}"` to the existing `_audit.LogAsync("Member", id, AuditAction.Update, ...)` call (line 85), following the `oldValue:`/`newValue:` pattern already used in `src/StageFright.Core/Modules/Finance/AccountService.cs:99-105`
- [ ] T014 [US1] Update `src/StageFright.UI/Pages/Members/MemberForm.razor`: replace the single "Full Name" input with two inputs, "First Name" and "Last Name", each with its own label and validation-message slot
- [ ] T015 [US1] Update `src/StageFright.UI/Pages/Members/MemberForm.razor.cs`: `MemberFormModel` gets `FirstName`/`LastName` (replacing `Name`); `OnInitializedAsync` binds from `member.FirstName`/`member.LastName` (line 36); `SaveAsync` builds `CreateMemberRequest`/`UpdateMemberRequest` with both fields (lines 85, 103)
- [ ] T016 [P] [US1] Update `src/StageFright.UI/Pages/Members/MemberDetail.razor`: `PageTitle` (line 3) and `<h1>` (line 16) use `_member.FullName` instead of `_member.Name`

**Checkpoint**: Member create/edit is fully functional on separate First Name/Last Name fields with independent validation and audit capture (verifiable once Phase 2 + this phase compile together).

---

## Phase 4: User Story 2 - Find and browse members by name (Priority: P2)

**Goal**: Every screen, grid, and report that used to show/sort/search a single combined name continues to work, searching and sorting correctly by First Name, Last Name, or full name, and displaying "Last Name, First Name" wherever names are listed/sorted.

**Independent Test**: On Member List, search by last-name-only, first-name-only, and full name and confirm the right member(s) appear; sort the list and confirm Last-Name-then-First-Name order displayed as "Last, First"; open Committee report, Member Account Summary report, Member List report, an Attendance grid, and a Participation grid and confirm names render correctly with consistent sort order.

### Tests for User Story 2

- [ ] T017 [P] [US2] Update `tests/StageFright.UI.Tests/Pages/Members/MemberListTests.cs`: search cases for last-name-only, first-name-only, and full-name input; grid column assertion for `SortableFullName` ("Last, First") display and click-to-sort
- [ ] T018 [P] [US2] Update `tests/StageFright.UI.Tests/Pages/Rehearsals/AttendanceGridTests.cs`: member rows render `SortableFullName` and are ordered Last-Name-then-First-Name
- [ ] T019 [P] [US2] Update `tests/StageFright.UI.Tests/Pages/Events/ParticipationGridTests.cs`: member rows render `SortableFullName` and are ordered Last-Name-then-First-Name
- [ ] T020 [P] [US2] Update `tests/StageFright.UI.Tests/Pages/Finance/MemberBalanceListTests.cs`: balance grid's "Member" column renders `SortableFullName`
- [ ] T021 [P] [US2] Update `tests/StageFright.UI.Tests/Pages/Finance/PaymentFormTests.cs`: member display line renders `FullName` (entry order)
- [ ] T022 [P] [US2] Update `tests/StageFright.Core.Tests/Modules/Finance/MemberBalanceServiceTests.cs`: `GetAllMemberBalancesAsync` populates `MemberBalance.Name` from `member.SortableFullName`
- [ ] T023 [P] [US2] Update `tests/StageFright.Reports.Tests/MemberListReportProviderTests.cs`: rows sorted Last-Name-then-First-Name; Name column cell/value uses `SortableFullName`
- [ ] T024 [P] [US2] Update `tests/StageFright.Reports.Tests/MemberAccountSummaryReportProviderTests.cs`: member ordering, section heading, and summary-row label use `SortableFullName`
- [ ] T025 [P] [US2] Update `tests/StageFright.Reports.Tests/CommitteeReportProviderTests.cs`: per-position member lists are built from `SortableFullName` and still sort alphabetically within a line
- [ ] T026 [P] [US2] Update `tests/StageFright.Data.Tests/Repositories/MemberRepositoryIntegrationTests.cs` (`Name = name` helper at line 153): switch the test member-builder helper to `FirstName`/`LastName` parameters
- [ ] T027 [P] [US2] Update `tests/StageFright.Data.Tests/Repositories/GLRepositoryIntegrationTests.cs` (line 462: `Name = "Test Member"`): switch to `FirstName = "Test", LastName = "Member"`
- [ ] T028 [P] [US2] Update `tests/StageFright.Data.Tests/Repositories/PaymentRepositoryIntegrationTests.cs` (line 182: `Name = "Test Member"`): switch to `FirstName = "Test", LastName = "Member"`
- [ ] T029 [P] [US2] Update `tests/StageFright.Data.Tests/FeeRepositoryIntegrationTests.cs` (line 188: `Name = "Test Member"`): switch to `FirstName = "Test", LastName = "Member"`
- [ ] T030 [P] [US2] Update `tests/StageFright.Data.Tests/Repositories/RepositoryIntegrationTests.cs` (line 31: `Assert.Equal("Test Member", found!.Name)` plus its seeding `Member`): switch to `FirstName`/`LastName` and assert against `FirstName`/`LastName`/`FullName` as appropriate
- [ ] T031 [US2] Add a new test in `tests/StageFright.Data.Tests/Repositories/RepositoryIntegrationTests.cs` that seeds members with distinct last names via `AttendanceRepository`, calls `GetByRehearsalAsync`, and asserts the real EF-translated SQL query returns records ordered by `Member.LastName` then `Member.FirstName` — this closes a coverage gap (no existing test exercises this repository's sort against a real SQLite connection) and guards against using an unmapped computed property (`SortableFullName`) inside an `IQueryable`, which would throw `InvalidOperationException` at runtime instead of translating to SQL
- [ ] T032 [P] [US2] Update `tests/StageFright.Integration.Tests/Scenarios/V3_RehearsalAttendanceTests.cs` for the new name fields
- [ ] T033 [P] [US2] Update `tests/StageFright.Integration.Tests/Scenarios/V5_EventsParticipationTests.cs` for the new name fields
- [ ] T034 [P] [US2] Update `tests/StageFright.Integration.Tests/Scenarios/V5_PaymentsTests.cs` for the new name fields
- [ ] T035 [P] [US2] Update `tests/StageFright.Integration.Tests/Scenarios/V6_AccountingReportsTests.cs` for the new name fields
- [ ] T036 [P] [US2] Update `tests/StageFright.Integration.Tests/Scenarios/V12_ReactivationForgivenessTests.cs` for the new name fields
- [ ] T037 [P] [US2] Update `tests/StageFright.Integration.Tests/Scenarios/V13_CommitteeResetAgmBannerTests.cs` for the new name fields

### Implementation for User Story 2

- [ ] T038 [US2] Update `src/StageFright.UI/Pages/Members/MemberList.razor.cs`: extend `DisplayMembers`'s search predicate (line 24) from `m.Name?.Contains(...)` to check `FirstName`, `LastName`, and `FullName`
- [ ] T039 [US2] Update `src/StageFright.UI/Pages/Members/MemberList.razor`: change the grid's `Property="Name"` column (line 48) to `Property="SortableFullName"`, keeping the existing inactive-suffix `<Template>` and the `@onclick`/`aria-label` bindings (lines 52, 75) switched to `SortableFullName`
- [ ] T040 [P] [US2] Update `src/StageFright.UI/Pages/Rehearsals/AttendanceGrid.razor.cs`: change `.OrderBy(m => m.Name)` (line 61) to `.OrderBy(m => m.LastName).ThenBy(m => m.FirstName)` (in-memory, list already materialized); populate `AttendanceRow.MemberName` (line 67) from `m.SortableFullName`
- [ ] T041 [P] [US2] Update `src/StageFright.UI/Pages/Rehearsals/AttendanceGrid.razor`: replace the three `record.Member.Name` / `@record.Member.Name` reads (lines 45, 53, 59) with `record.Member.SortableFullName`
- [ ] T042 [US2] Update `src/StageFright.Data/Repositories/AttendanceRepository.cs`: change `.OrderBy(a => a.Member.Name)` (line 26) to `.OrderBy(a => a.Member.LastName).ThenBy(a => a.Member.FirstName)` — **must** use the mapped `LastName`/`FirstName` columns here, not `SortableFullName`, because this `OrderBy` runs inside an EF Core `IQueryable` translated to SQL, and `SortableFullName` is an unmapped computed property that cannot be translated (see T031)
- [ ] T043 [P] [US2] Update `src/StageFright.UI/Pages/Events/ParticipationGrid.razor.cs`: change `.OrderBy(m => m.Name)` (line 55) to `.OrderBy(m => m.LastName).ThenBy(m => m.FirstName)` (in-memory); populate `ParticipationRow.MemberName` (line 60) from `m.SortableFullName`
- [ ] T044 [P] [US2] Update `src/StageFright.UI/Pages/Events/EventDetail.razor`: change `.OrderBy(p => p.Member?.Name)` (line 52) and `record.Member?.Name` (line 58) to `SortableFullName` (in-memory collection, already materialized)
- [ ] T045 [P] [US2] Update `src/StageFright.Core/Modules/Finance/MemberBalanceService.cs`: `GetAllMemberBalancesAsync` (line 74) populates `MemberBalance.Name` from `member.SortableFullName` instead of `member.Name`
- [ ] T046 [P] [US2] Update `src/StageFright.Core/Modules/Finance/PaymentService.cs`: `var memberName = member?.Name ?? "Unknown Member";` (line 68) becomes `member?.FullName`
- [ ] T047 [P] [US2] Update `src/StageFright.UI/Pages/Finance/PaymentForm.razor.cs`: `_memberName = member?.Name;` (line 33) becomes `member?.FullName`
- [ ] T048 [P] [US2] Update `src/StageFright.Core/Modules/Finance/ReactivationForgivenessService.cs`: `var memberName = member?.Name ?? "Unknown Member";` (line 63) becomes `member?.FullName`
- [ ] T049 [US2] Update `src/StageFright.Reports/Providers/MemberListReportProvider.cs`: `.OrderBy(m => m.Name)` (line 56) becomes `.OrderBy(m => m.LastName).ThenBy(m => m.FirstName)`; the `m.Name` cell value (line 65) and the "Name" column header become `m.SortableFullName`
- [ ] T050 [US2] Update `src/StageFright.Reports/Providers/MemberAccountSummaryReportProvider.cs`: `.OrderBy(m => m.Name)` (line 52) becomes `.OrderBy(m => m.LastName).ThenBy(m => m.FirstName)`; the `label`/section heading/summary-row member cell (line 127) uses `member.SortableFullName`
- [ ] T051 [US2] Update `src/StageFright.Reports/Providers/CommitteeReportProvider.cs`: `.OrderBy(m => m.Name)` (line 60) becomes `.OrderBy(m => m.LastName).ThenBy(m => m.FirstName)`; the two `member.Name` reads feeding `JoinAlphabetically` (lines 130, 141) use `member.SortableFullName`

**Checkpoint**: Search, sort, and display of member names is correct everywhere it was previously shown (verifiable once Phase 2 + this phase compile together).

---

## Phase 5: User Story 3 - Existing member records are converted automatically (Priority: P3)

**Goal**: On upgrade, every existing member's combined `Name` is split into `FirstName`/`LastName` (trim → collapse whitespace → split on first space → truncate to 100 chars) with zero records lost, duplicated, or corrupted, for active, inactive, and archived members alike; pre-feature backups still restore correctly.

**Independent Test**: Take a database with existing member records (including a mononym and an archived member), run the migration, and confirm the row count is unchanged, every member has non-empty `FirstName`, two-word names split correctly, and single-word names leave `LastName` blank without hiding the member.

### Tests for User Story 3

- [ ] T052 [P] [US3] Create `tests/StageFright.Core.Tests/Modules/Members/MemberNameSplitterTests.cs`: cases for a normal two-word name, leading/trailing whitespace, multiple internal spaces, a mononym (no space → `LastName` blank), a name with more than two words (split on *first* space only), and a side exceeding 100 characters after split (truncated)
- [ ] T053 [US3] Create `tests/StageFright.Data.Tests/Migrations/SplitMemberNameIntoFirstLastNameTests.cs`, following the `ConvertCategoriesToAccountsMigrationTests.cs` precedent (migrate to the prior migration `20260708050050_AddAbnToSettings`, seed legacy `Name` rows via raw SQL, migrate to latest, assert on the resulting `Members` table): cover a two-word name ("Jane Smith" → `FirstName="Jane"`, `LastName="Smith"`), a mononym (→ `FirstName=<value>`, `LastName=""`), irregular whitespace (leading/trailing/multiple internal spaces, collapsed before split), an overlong split side (truncated to 100 chars), and an archived (soft-deleted) member (converted identically to active); assert total row count and each member's `Status`/`IsDeleted` are unchanged
- [ ] T054 [P] [US3] Update `tests/StageFright.Core.Tests/Modules/Settings/BackupServiceTests.cs`: export (`MapMember`) populates `FirstName`/`LastName` and leaves `LegacyName` blank; restore (`MapMemberFromDto`) of a legacy DTO (`FirstName`/`LastName` empty, `LegacyName` populated) derives `FirstName`/`LastName` via `MemberNameSplitter.Split`; restore of a current-format DTO uses `FirstName`/`LastName` directly
- [ ] T055 [P] [US3] Update `tests/StageFright.Data.Tests/BackupImportTests.cs` (lines 62, 85, 181, 209, 225): switch seeded `Name = "..."` values to `FirstName`/`LastName`, and add a case importing a legacy-format backup (pre-feature shape) to confirm it still restores non-empty names
- [ ] T056 [P] [US3] Update `tests/StageFright.Integration.Tests/Scenarios/V9_BackupRestoreTests.cs`: full export/restore round-trip preserves `FirstName`/`LastName` exactly; restoring a synthetic pre-feature backup produces correct split names

### Implementation for User Story 3

- [ ] T057 [US3] Create `src/StageFright.Core/Modules/Members/MemberNameSplitter.cs`: static `Split(string combinedName) -> (string FirstName, string LastName)` implementing trim → collapse internal whitespace to single spaces → split on first remaining space → truncate each side to 100 characters, returning `LastName = ""` for a mononym
- [ ] T058 [US3] Scaffold (`dotnet ef migrations add SplitMemberNameIntoFirstLastName --project src/StageFright.Data/ --startup-project src/StageFright.App/`) then hand-edit `src/StageFright.Data/Migrations/<timestamp>_SplitMemberNameIntoFirstLastName.cs` per data-model.md/research.md Decision 3: add nullable `FirstName`/`LastName` columns, `UPDATE Members SET Name = TRIM(Name)`, ten repeated `REPLACE(Name, '  ', ' ')` passes to collapse internal whitespace, a `CASE`-based split-on-first-space `UPDATE` truncating each side to 100 chars (`SUBSTR(..., 1, 100)`), `AlterColumn` both columns to `NOT NULL`, then `DropColumn` `Name`; write the matching `Down()` (`AddColumn Name` → `UPDATE Members SET Name = TRIM(FirstName || ' ' || LastName)` → `AlterColumn NOT NULL HasMaxLength(255)` → drop `FirstName`/`LastName`); do not change `Settings.SchemaVersion` (research.md Decision 9)
- [ ] T059 [P] [US3] Update `src/StageFright.Core/Modules/Settings/Backup/MemberBackupDto.cs`: rename the C# property on `[ProtoMember(2)]` from `Name` to `LegacyName` (same wire field number — old backups stay compatible), add `[ProtoMember(16)] public string FirstName` and `[ProtoMember(17)] public string LastName`
- [ ] T060 [US3] Update `src/StageFright.Core/Modules/Settings/BackupService.cs`: `MapMember` (line 272) populates `FirstName`/`LastName` from the entity and leaves `LegacyName` blank; `MapMemberFromDto` (line 383) uses `d.FirstName`/`d.LastName` directly when either is non-empty, otherwise calls `MemberNameSplitter.Split(d.LegacyName)` when `LegacyName` is non-empty (depends on T057, T059)

**Checkpoint**: Upgrading an existing database and restoring a pre-feature backup both produce correct, complete `FirstName`/`LastName` data with zero record loss (verifiable once Phase 2 + this phase compile together).

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final whole-solution verification once all three stories are in place.

- [ ] T061 Walk through every manual validation step in [quickstart.md](./quickstart.md) (all three user stories plus the backup/restore compatibility check) against a running `dotnet run --project src/StageFright.App/` instance; while doing so, also opt into the Setup Wizard's seed-debug-data checkbox and confirm the 51 seeded demo members (T006) all show distinct, correctly-split First/Last names on Member List
- [ ] T062 Run `dotnet build` and the full `dotnet test` suite (all five projects) from the repo root and confirm everything is green, per CLAUDE.md's build/test verification rule
- [ ] T063 [P] Tick off the success-criteria checklist (SC-001 through SC-005) at the bottom of quickstart.md based on the verification in T061/T062

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — run first to confirm a green baseline
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories; T002-T006 can run in parallel with each other
- **User Stories (Phase 3-5)**: All depend on Foundational completion. Because `Member.Name` is removed (not just added-to), the solution will not compile until **all three** phases' file-level tasks are done — they can be done in any order relative to each other (US1, US2, US3 touch disjoint file sets), but none can be independently *built* in isolation the way a purely additive feature could
- **Polish (Phase 6)**: Depends on all three user stories being complete and the solution compiling again

### Within Each User Story

- Tests are written first per task list order and should fail (or fail to compile) before the paired implementation task lands
- Within US3: T057 (`MemberNameSplitter`) before T060 (`BackupService`, which calls it); T059 (`MemberBackupDto`) before T060 (which reads its new properties); T058 (migration) has no code dependency on T057/T059 (it's pure SQL) and can proceed in parallel with them

### Parallel Opportunities

- All of Phase 2 (T002-T006) can run in parallel — five independent files
- Within each user story, all tasks marked [P] touch different files and can run in parallel; unmarked tasks either touch a file another unmarked task in the same story also touches, or have an explicit dependency called out above
- US1, US2, and US3 touch entirely disjoint file sets and can be staffed in parallel by different developers once Phase 2 is done, even though the solution won't compile until all three finish

---

## Parallel Example: Phase 2 (Foundational)

```bash
# All five foundational file edits are independent:
Task: "Replace Name with FirstName/LastName/FullName/SortableFullName on Member.cs"
Task: "Update MemberConfiguration.cs mapping"
Task: "Update CreateMemberRequest.cs"
Task: "Update UpdateMemberRequest.cs"
Task: "Update DebugDataSeeder.cs's literal member data to FirstName/LastName"
```

## Parallel Example: User Story 1 tests

```bash
Task: "Update MemberValidationServiceTests.cs for per-field required/max-length"
Task: "Update MemberServiceTests.cs for FirstName/LastName mapping + audit capture"
Task: "Update MemberFormTests.cs for two-field entry"
Task: "Update MemberDetailTests.cs for FullName header"
Task: "Update V2_MemberManagementTests.cs for the full create/edit journey"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (entity/config/DTO shape + seed data) — the solution will not compile again until Phase 3 is also done, since `MemberForm.razor.cs` and every other consumer still reference the now-removed `Name`
3. Complete Phase 3: User Story 1
4. At this point the solution still won't build (US2/US3 consumers still reference `Member.Name`) — for a true standalone MVP checkpoint, the fastest path to a green build is completing Phase 4 and Phase 5's file-level changes too (they're small, mechanical renames in most files), even if you validate US1's *behavior* first

### Incremental Delivery (by verified behavior, not by build)

1. Complete Setup + Foundational
2. Add all of US1, US2, US3's file-level changes (required for the solution to compile at all) — but **verify** behavior story-by-story: US1 first (two-field entry/validation/audit), then US2 (search/sort/reports), then US3 (upgrade conversion/backup)
3. Each story's checkpoint above documents what "verified" means for that slice

### Parallel Team Strategy

With multiple developers, once Phase 2 lands:

- Developer A: User Story 1 (Members module + its tests)
- Developer B: User Story 2 (UI grids, reports, Finance display sites + their tests)
- Developer C: User Story 3 (migration, `MemberNameSplitter`, backup/restore + their tests)

All three must land before `dotnet build`/`dotnet test` (T062) will pass.

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- T006 (`DebugDataSeeder.cs`) is dev/demo seed data, not a user-facing acceptance criterion of any single story — it's grouped in Foundational because it's required for `dotnet build`/`dotnet run` to succeed at all, which every later manual quickstart check depends on
- T042 (`AttendanceRepository.cs`) is the one place in this feature where using the wrong name property (`SortableFullName` instead of the mapped `LastName`/`FirstName`) would compile fine but throw `InvalidOperationException` at runtime, because it's the only `Member.Name` consumer that runs inside a live EF Core `IQueryable` — see T031's regression test
- Commit after each task or logical group, per this repository's CLAUDE.md workflow (stage everything, commit with a descriptive message)
- Run `dotnet build` and `dotnet test` before considering any phase checkpoint met
