---
description: "Task list template for feature implementation"
---

# Tasks: AGM Workflow

**Input**: Design documents from `/specs/013-agm-workflow/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/agm-workflow-contract.md](./contracts/agm-workflow-contract.md)

**Tests**: Included and mandatory — CLAUDE.md's "Exhaustive code-path test coverage" rule is non-negotiable, and the plan's Constitution Check (§11) requires full coverage for every new/changed service and component.

**Organization**: Tasks are grouped by user story (US1–US5, matching spec.md's P1/P2/P3 priorities). Phase 2 (Foundational) is **production code only** — schema, DI, backup plumbing, and the old reset-mechanism deletion — because `CommitteeMembership` is renamed to `CommitteePositionRecord` in place (a breaking rename, not additive) and several new entities/services (`AnnualGeneralMeeting`, `CommitteeTerm`, `CommitteeOfficeHolderType`, `AgmService`) are genuinely shared by every story. **All test-file updates, including ones that merely fix a compile break caused by the Foundational rename, are placed in the story that most naturally owns that behavior** — the solution will not compile again until every story's test tasks have landed, exactly as with the equivalent rename in spec 011. Treat each story's Checkpoint as "this slice of behavior is correct," not "this slice alone builds."

One structural note: `AgmService` implements all five `IAgmService` methods in a single class (C# requires a class to implement its whole interface at once), so its production-code task sits in US1 even though `RecordSpecialElectionAsync` is US4's method and parts of `GetPastAsync`/`ArchiveAsync` are US5's. Each story still adds its **own** tests and UI against the relevant subset, so the Independent Test in spec.md holds for each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies) — tasks in the same wave
- **[Story]**: Which user story this task belongs to (US1–US5)
- Paths are relative to the repository root (`c:\SourceCode\StageFrightCommunity`)

---

## Phase 1: Setup

**Purpose**: Establish a known-green baseline before touching a shared entity (`CommitteeMembership`) and shared services (`EventTypeService`, `CommitteeService`, `Settings`).

- [x] **T001** Run `dotnet restore`, `dotnet build`, and `dotnet test` (all five projects) from the repo root and confirm everything is currently green, so any later failure is attributable to this feature

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: New schema (4 new entities + renamed/extended `CommitteePositionRecord`), the services every story calls into, backup/restore support, and removal of the old manual reset mechanism (FR-018). Nothing in Phase 3+ compiles until this lands.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

### Entities and Settings

**Wave 1 — independent (different files):**

- [x] **T002** [P] New entity `Guid Id, DateTime Date, string? Notes, int? GeneralCommitteeSeatCountTarget` + soft-delete/audit fields on `src/StageFright.Core/Entities/AnnualGeneralMeeting.cs`
- [x] **T003** [P] New entity `Guid Id, Guid AnnualGeneralMeetingId, Guid MemberId, bool Attended` + soft-delete/audit fields (never independently set — immutable once saved, same convention as `AttendanceRecord`) on `src/StageFright.Core/Entities/AgmAttendanceRecord.cs`
- [x] **T004** [P] New entity `Guid Id, string Name (max 100), int DisplayOrder, bool IsBuiltIn` + soft-delete/audit fields on `src/StageFright.Core/Entities/CommitteeOfficeHolderType.cs`
- [x] **T005** [P] New entity `Guid Id, Guid StartedByAgmId, DateTime StartDate, DateTime? EndDate, int LabelYear` + audit fields only (no soft-delete — archived only as a side effect of its starting AGM) on `src/StageFright.Core/Entities/CommitteeTerm.cs`
- [x] **T006** [P] Rename `src/StageFright.Core/Entities/CommitteeMembership.cs` → `src/StageFright.Core/Entities/CommitteePositionRecord.cs`: rename the class; make `Year` (`int` → `int?`) and `Position` (`string` → `string?`) legacy-only (populated on pre-feature rows, always null on rows this feature creates); add `Guid? CommitteeTermId`, `Guid? OfficeHolderTypeId`, `DateTime? StartDate`, `DateTime? EndDate`
- [x] **T007** [P] `src/StageFright.Core/Entities/Settings.cs`: add `public int? GeneralCommitteeSeatCountTarget { get; set; }`; remove `LastCommitteeResetYear` (only consumer, `CheckAgmBannerAsync`, is being deleted in T024); update `CommitteeRenewalMonth`'s doc comment to describe it as "the month the AGM is normally held" (repurposed in place, FR-022/FR-030 — no new field)

**⟶ Wait for Wave 1 to finish, then:**

- [x] **T008** [US-shared] `src/StageFright.Core/Entities/Member.cs`: rename navigation property `ICollection<CommitteeMembership> CommitteeMemberships` → `ICollection<CommitteePositionRecord> CommitteePositionRecords`

### EF configurations

**Wave 2 — independent (different files), depends on Wave 1:**

- [ ] **T009** [P] New `src/StageFright.Data/Configurations/AnnualGeneralMeetingConfiguration.cs` (`IEntityTypeConfiguration<AnnualGeneralMeeting>`; `HasQueryFilter` on `!IsDeleted`)
- [ ] **T010** [P] New `src/StageFright.Data/Configurations/AgmAttendanceRecordConfiguration.cs` (unique index `(AnnualGeneralMeetingId, MemberId)`)
- [ ] **T011** [P] New `src/StageFright.Data/Configurations/CommitteeOfficeHolderTypeConfiguration.cs` (unique index `(Name) WHERE IsDeleted = 0`, case-insensitive collation)
- [ ] **T012** [P] New `src/StageFright.Data/Configurations/CommitteeTermConfiguration.cs` (no query filter — no soft-delete fields)
- [ ] **T013** [P] Rename `src/StageFright.Data/Configurations/CommitteeMembershipConfiguration.cs` → `CommitteePositionRecordConfiguration.cs`: drop the old `HasIndex(c => new { c.MemberId, c.Year }).IsUnique().HasFilter("[IsDeleted] = 0")`; add `unique index (CommitteeTermId, OfficeHolderTypeId) WHERE EndDate IS NULL AND OfficeHolderTypeId IS NOT NULL AND IsDeleted = 0` and `unique index (CommitteeTermId, MemberId) WHERE EndDate IS NULL AND IsDeleted = 0`; make `Year`/`Position` property mappings optional
- [ ] **T014** [P] `src/StageFright.Data/Configurations/MemberConfiguration.cs`: `builder.HasMany(m => m.CommitteeMemberships)` → `builder.HasMany(m => m.CommitteePositionRecords)`

### DbContext and migration

**⟶ Wait for Wave 2 to finish, then (sequential — same model):**

- [ ] **T015** `src/StageFright.Data/StageFrightDbContext.cs`: rename `DbSet<CommitteeMembership> CommitteeMemberships` → `DbSet<CommitteePositionRecord> CommitteePositionRecords`; add `DbSet<AnnualGeneralMeeting> AnnualGeneralMeetings`, `DbSet<AgmAttendanceRecord> AgmAttendanceRecords`, `DbSet<CommitteeOfficeHolderType> CommitteeOfficeHolderTypes`, `DbSet<CommitteeTerm> CommitteeTerms`
- [ ] **T016** Generate migration `dotnet ef migrations add AddAgmWorkflow --project src/StageFright.Data/ --startup-project src/StageFright.App/`: confirm it renames the `CommitteeMemberships` table (not drop+recreate), adds the 4 new nullable columns + drops the old unique index + adds the 2 new filtered indexes on the renamed table, creates the 4 new tables, adds `Settings.GeneralCommitteeSeatCountTarget` and drops `Settings.LastCommitteeResetYear`; hand-add a `migrationBuilder.InsertData(...)` for the 3 built-in `CommitteeOfficeHolderType` rows (President/Secretary/Treasurer, `IsBuiltIn=true`, `DisplayOrder` 0/1/2) so both new and upgrading installs get them without a separate seeding step

### Repositories

**Wave 3 — independent (different files), depends on T016:**

- [ ] **T017** [P] New `src/StageFright.Core/Contracts/IAgmRepository.cs` (extends `ISoftDeletableRepository<AnnualGeneralMeeting>`, adds `GetPastOrderedAsync` most-recent-first) + `src/StageFright.Data/Repositories/AgmRepository.cs`
- [ ] **T018** [P] New `src/StageFright.Core/Contracts/IAgmAttendanceRepository.cs` (extends `IRepository<AgmAttendanceRecord>`, adds `AddRangeAsync(IEnumerable<AgmAttendanceRecord>)` and `GetByAgmAsync(Guid agmId)`) + `src/StageFright.Data/Repositories/AgmAttendanceRepository.cs`
- [ ] **T019** [P] New `src/StageFright.Core/Contracts/ICommitteeOfficeHolderTypeRepository.cs` (extends `ISoftDeletableRepository<CommitteeOfficeHolderType>`, adds `GetActiveOrderedAsync` built-ins-first-by-DisplayOrder-then-custom, `GetMaxCustomDisplayOrderAsync`) + `src/StageFright.Data/Repositories/CommitteeOfficeHolderTypeRepository.cs`
- [ ] **T020** [P] New `src/StageFright.Core/Contracts/ICommitteeTermRepository.cs` (extends `IRepository<CommitteeTerm>`, adds `GetOpenAsync` — the term with `EndDate == null`) + `src/StageFright.Data/Repositories/CommitteeTermRepository.cs`
- [ ] **T021** Rename `ICommitteeMembershipRepository.cs` → `src/StageFright.Core/Contracts/ICommitteePositionRecordRepository.cs` (extends `ISoftDeletableRepository<CommitteePositionRecord>`; keep `GetByMemberAsync`/`GetByYearAsync`; remove `SoftDeleteCurrentYearAsync`; add `GetByTermAsync(Guid committeeTermId)`, `GetByAgmAsync(Guid annualGeneralMeetingId)` — joins through `CommitteeTerm.StartedByAgmId`, and `GetOpenByMemberInTermAsync(Guid termId, Guid memberId)`) + rename `CommitteeMembershipRepository.cs` → `src/StageFright.Data/Repositories/CommitteePositionRecordRepository.cs`

### Services

**⟶ Wait for Wave 3 to finish, then:**

**Wave 4 — independent (different files):**

- [ ] **T022** [P] New `src/StageFright.Core/Contracts/ICommitteeOfficeHolderTypeService.cs` + `src/StageFright.Core/Modules/Members/CommitteeOfficeHolderTypeService.cs` per contract: `GetActiveAsync`, `AddAsync`, `RenameAsync` (throws `ValidationException` if `IsBuiltIn`), `ReorderAsync` (custom titles only), `ArchiveAsync` (throws `ValidationException` if `IsBuiltIn`)
- [ ] **T023** [US-shared] Extend `src/StageFright.Core/Contracts/ICommitteeService.cs` + `src/StageFright.Core/Modules/Members/CommitteeService.cs`: remove `SoftDeleteCurrentYearAsync`; add `GetCurrentAsync()` (records under the one open `CommitteeTerm`), `GetByTermAsync(Guid committeeTermId)`, `GetByAgmAsync(Guid annualGeneralMeetingId)`; keep `AddOrUpdateAsync`/`GetHistoryAsync` unchanged (still valid — they write/read legacy-shaped rows)
- [ ] **T024** New `src/StageFright.Core/Contracts/IAgmService.cs`, `src/StageFright.Core/Modules/Agm/RecordAgmRequest.cs`, `src/StageFright.Core/Modules/Agm/RecordSpecialElectionRequest.cs` exactly per [contracts/agm-workflow-contract.md](./contracts/agm-workflow-contract.md) (interface + request DTOs only — `AgmService.cs` implementation lands in US1/T044)

### Delete the old manual reset mechanism (FR-018)

**Wave 5 — independent (different files), no dependency on Waves 1–4:**

- [ ] **T025** [P] Delete `src/StageFright.Core/Contracts/ICommitteeAnnualResetService.cs` and `src/StageFright.Core/Modules/Members/CommitteeAnnualResetService.cs`
- [ ] **T026** [P] `src/StageFright.UI/Pages/Settings/GeneralSettingsTab.razor.cs` + `.razor`: remove the `_committeeResetService` field, `HandleResetCommitteeAsync`, the `OnInitializedAsync` AGM-banner-check block, `_agmBanner`/`_resetting` fields, the `#agm-banner` alert `<div>`, and the "Reset Committee for New Year" button; relabel the "Committee Renewal Month" `InputSelect` (bound to `s.CommitteeRenewalMonth`) as "AGM Month"
- [ ] **T027** [P] `src/StageFright.App/MauiProgram.cs`: remove `services.AddScoped<ICommitteeAnnualResetService, CommitteeAnnualResetService>();`; add registrations for `IAgmRepository`/`AgmRepository`, `IAgmAttendanceRepository`/`AgmAttendanceRepository`, `ICommitteeOfficeHolderTypeRepository`/`CommitteeOfficeHolderTypeRepository`, `ICommitteeTermRepository`/`CommitteeTermRepository`, `ICommitteeOfficeHolderTypeService`/`CommitteeOfficeHolderTypeService`, `IAgmService`/`AgmService`; rename `ICommitteeMembershipRepository`/`CommitteeMembershipRepository` registration to `ICommitteePositionRecordRepository`/`CommitteePositionRecordRepository`
- [ ] **T028** [P] `src/StageFright.Core/Contracts/IEventRepository.cs` + `IEventService.cs` + `src/StageFright.Data/Repositories/EventRepository.cs` + `src/StageFright.Core/Modules/Events/EventService.cs`: remove `AgmExistsInYearAsync` (confirmed dead once `CommitteeAnnualResetService` — its only caller — is deleted; `GetMostRecentPastAsync` stays, it's a generic query also used by `IRehearsalService`'s equivalent)

### EventType: stop offering AGM as a generic event type (FR-003)

**Wave 6 — independent of AGM entities, can run alongside Waves 1–5:**

- [ ] **T029** [P] `src/StageFright.Core/Modules/Events/EventTypeService.cs`: remove `"Annual General Meeting"` from `GetDefaultEventTypeNames()`; add `GetSelectableForNewEventsAsync()` to `IEventTypeService`/`EventTypeService` (excludes `Name == "Annual General Meeting"`, case-insensitive — a no-op filter for fresh installs, a real filter for upgrading installs that still have the row)
- [ ] **T030** [P] `src/StageFright.UI/Pages/Events/EventForm.razor.cs`: `OnInitializedAsync`'s `EventTypeService.GetAllAsync()` → `EventTypeService.GetSelectableForNewEventsAsync()`

### Backup/restore support (research D8 — new entities are never automatically covered)

**Wave 7 — depends on T006 (renamed entity) and T002–T005 (new entities):**

- [ ] **T031** [P] Rename `src/StageFright.Core/Modules/Settings/Backup/CommitteeMembershipBackupDto.cs` → `CommitteePositionRecordBackupDto.cs` (add the 4 new fields, next sequential `[ProtoMember]` numbers); add new `AnnualGeneralMeetingBackupDto.cs`, `AgmAttendanceRecordBackupDto.cs`, `CommitteeOfficeHolderTypeBackupDto.cs`, `CommitteeTermBackupDto.cs` in the same folder, flat `[ProtoContract]`/`[ProtoMember(n)]` classes mirroring `MemberBackupDto`'s pattern; add `GeneralCommitteeSeatCountTarget` to `SettingsBackupDto.cs` (next sequential member number), remove `LastCommitteeResetYear`
- [ ] **T032** [US-shared] `src/StageFright.Core/Modules/Settings/Backup/BackupEnvelope.cs`: rename `List<CommitteeMembershipBackupDto>? CommitteeMemberships` → `List<CommitteePositionRecordBackupDto>? CommitteePositionRecords`; add 4 new `List<...>?` collections (next sequential `[ProtoMember]` numbers, append-only) for the new entities; `src/StageFright.Core/Modules/Settings/Backup/BackupSnapshot.cs`: same rename + 4 new `IReadOnlyList<T>` properties
- [ ] **T033** [US-shared] `src/StageFright.Core/Modules/Settings/BackupService.cs`: rename all `CommitteeMembership`/`MapCommittee`/`MapCommitteeFromDto` references to `CommitteePositionRecord` equivalents (mapping the 4 new fields too); add mapper methods (`MapAgm`/`MapAgmFromDto`, etc.) for the 4 new entities; add their keys to `MapToEnvelope`'s `EntityCounts` dictionary and to `ValidateCompleteness()`'s required-key checks; `DeserializeAndValidate` needs `envelope.CommitteePositionRecords ??= []` plus `??= []` for the 4 new collections; add `GeneralCommitteeSeatCountTarget` to `MapSettings`/`MapSettingsFromDto`
- [ ] **T034** [US-shared] `src/StageFright.Data/Repositories/BackupRepository.cs`: rename `_db.CommitteeMemberships` → `_db.CommitteePositionRecords` in `GetFullSnapshotAsync`/`UpsertSnapshotAsync`; add the 4 new entities' `IgnoreQueryFilters().AsNoTracking().ToListAsync(ct)` reads and `UpsertCollectionAsync(...)` calls, in FK-safe order (AGM before its attendance records; office-holder-types and terms before position records that reference them)

### Foundational cleanup: MemberDetail's committee-history display

**⟶ Wait for Wave 7 (T031) to finish, then:**

- [ ] **T035** [US-shared] `src/StageFright.UI/Pages/Members/MemberDetail.razor.cs` + `.razor`: rename `List<CommitteeMembership> _committeeHistory` → `List<CommitteePositionRecord>`; the committee-history list now shows, per record, an *effective year* (`CommitteeTerm.LabelYear` when `CommitteeTermId` is set, else legacy `Year`) and *effective label* (`OfficeHolderType.Name` when set, else legacy `Position`, else "General Committee Member"); "Current" badge uses `CommitteeTerm.EndDate == null` for new-model rows instead of `Year == _currentYear`

**Checkpoint**: `CommitteeMembership` no longer exists; the schema, services, backup pipeline, and old reset mechanism are all in their final shape. Every user story phase below builds against this.

---

## Phase 3: User Story 1 - Record an AGM's attendance and election results in one place (Priority: P1) 🎯 MVP

**Goal**: A "Record AGM" screen (from the Events menu) captures meeting date, attendance (with independent scroll, no paging), President/Secretary/Treasurer + custom office-holder + general-committee assignments, and saves everything atomically; a saved AGM is read-only.

**Independent Test**: Open the AGM screen, enter a date, mark attendance, assign President/Secretary/Treasurer + 1-2 general committee members, save, and confirm the meeting/attendance/positions all persisted and are visible without touching the Members screens.

### Tests for User Story 1

**Wave 1 — independent (different files):**

- [ ] **T036** [P] [US1] Update `tests/StageFright.Core.Tests/Entities/EntityFieldConstraintsTests.cs`: `typeof(CommitteeMembership)` → `typeof(CommitteePositionRecord)` (4 call sites)
- [ ] **T037** [P] [US1] Update `tests/StageFright.Integration.Tests/Scenarios/V11_ReportsMenuTests.cs`: `_db.CommitteeMemberships`/`CommitteeMembership`/`CommitteeMembershipRepository` → `CommitteePositionRecords`/`CommitteePositionRecord`/`CommitteePositionRecordRepository`
- [ ] **T038** [P] [US1] Update `tests/StageFright.Core.Tests/Modules/Settings/BackupServiceTests.cs`: rename `CommitteeMembership`-shaped assertions to `CommitteePositionRecord`; add round-trip coverage for the 4 new entity collections and `EntityCounts`/`ValidateCompleteness` including them
- [ ] **T039** [P] [US1] Update `tests/StageFright.UI.Tests/Pages/Members/MemberFormTests.cs` and `tests/StageFright.UI.Tests/Pages/Members/MemberDetailTests.cs`: rename `CommitteeMembership` references; `MemberDetailTests` gains a case asserting a new-model position record renders its `CommitteeTerm.LabelYear`/`OfficeHolderType.Name` instead of blank legacy fields
- [ ] **T040** [P] [US1] New `tests/StageFright.Core.Tests/Modules/Members/CommitteeServiceTests.cs`: full coverage of `CommitteeService` (no prior test file exists) — `AddOrUpdateAsync` create/update, `GetHistoryAsync`, `GetCurrentAsync`, `GetByTermAsync`, `GetByAgmAsync`
- [ ] **T041** [P] [US1] New `tests/StageFright.Core.Tests/Modules/Agm/AgmServiceTests.cs`: `RecordAsync` success (meeting + attendance + position records persisted atomically), `RecordAsync` throws `ValidationException` when a member appears in more than one assignment (FR-008), `RecordAsync` rolls back entirely on a mid-transaction failure, `RecordAsync` closes the previously-open `CommitteeTerm` and opens a new one (D1), `GetByIdAsync`, `GetPastAsync` most-recent-first — NSubstitute mocks for every repo dependency, `_unitOfWork.ExecuteInTransactionAsync` stubbed to invoke its delegate directly (research D12 convention)
- [ ] **T042** [P] [US1] New `tests/StageFright.UI.Tests/Pages/Events/AgmAttendanceGridTests.cs`: renders one row per active member, "Select All" toggles every row, no pagination controls present, independent scroll container class applied (`RadzenGridTestContext` base, research D12)
- [ ] **T043** [P] [US1] New `tests/StageFright.UI.Tests/Pages/Events/RecordAgmTests.cs`: attendance + office-holder + general-committee selection flow, FR-008 one-member-one-slot UI validation, save calls `IAgmService.RecordAsync` with the expected request shape, saved AGM redirects to its read-only detail view

### Implementation for User Story 1

**Wave 2 — depends on Foundational + Wave 1 test scaffolding:**

- [ ] **T044** [US1] New `src/StageFright.Core/Modules/Agm/AgmService.cs` implementing `IAgmService` per contract: `RecordAsync` (pre-transaction FR-008 validation mirroring `PaymentService.RecordAsync`; inside `ExecuteInTransactionAsync` — create `AnnualGeneralMeeting` snapshotting `Settings.GeneralCommitteeSeatCountTarget`, write one `AgmAttendanceRecord` per `AllActiveMemberIds`, close the open `CommitteeTerm` (`EndDate = request.Date`) if any, create the new `CommitteeTerm` (`LabelYear` per FR-024's majority-of-days rule), create one `CommitteePositionRecord` per assignment, audit), `GetByIdAsync`, `GetPastAsync` (most-recent-first), `ArchiveAsync`, `RecordSpecialElectionAsync` (full body per contract — US4 adds its own dedicated tests/UI against this method)

**Wave 3 — independent (different files), depends on T044:**

- [ ] **T045** [P] [US1] New `src/StageFright.UI/Pages/Events/AgmAttendanceGrid.razor(.cs)(.css)` — `RadzenDataGrid` with `AllowPaging="false"`, wrapped in the `flex:1; min-height:0; overflow-y:auto` scroll container (research D5); one row per active member with a "select all" `HeaderTemplate` checkbox (matching `AttendanceGrid.razor`'s pattern)
- [ ] **T046** [P] [US1] New `src/StageFright.UI/Pages/Events/RecordAgm.razor(.cs)` at route `/events/agm/new`: meeting date, embeds `AgmAttendanceGrid`, President/Secretary/Treasurer + active `CommitteeOfficeHolderType` assignment dropdowns, general-committee multi-select with seat-count-target progress display, FR-008 client-side one-member-one-slot guard, calls `IAgmService.RecordAsync`
- [ ] **T047** [P] [US1] New `src/StageFright.UI/Pages/Events/AgmDetail.razor(.cs)` at route `/events/agm/{id:guid}` — read-only attendance + elected positions (US1 needs this immediately since a saved AGM must render read-only per FR-011; US5 extends it with the archive action and multi-holder date display)
- [ ] **T048** [P] [US1] `src/StageFright.Core/Modules/Events/EventsMenuItemProvider.cs`: add `SubItems` to the existing "Events" `MenuItem` — "All Events" (`/events`), "Record AGM" (`/events/agm/new`), "Past AGMs" (`/events/agm`) per contract (US5's list page lands in its own phase but the menu entry is added once, here)

**Checkpoint**: A coordinator can record a complete AGM — attendance + every election — in one atomic save, viewable read-only afterwards (SC-001). Verifiable once this phase compiles together with Foundational.

---

## Phase 4: User Story 2 - Define committee positions and office-holder titles ahead of time (Priority: P2)

**Goal**: Coordinators manage custom office-holder titles (add/rename/reorder/archive) and the general-committee seat-count target, from Settings and from the setup wizard.

**Independent Test**: Add, rename, reorder, and archive custom titles and set a seat-count target from Settings; confirm both surfaces (Settings + setup wizard) read/write the same values.

### Tests for User Story 2

**Wave 1 — independent (different files):**

- [ ] **T049** [P] [US2] New `tests/StageFright.Core.Tests/Modules/Members/CommitteeOfficeHolderTypeServiceTests.cs`: `GetActiveAsync` built-ins-first ordering, `AddAsync`, `RenameAsync` success + throws on `IsBuiltIn`, `ReorderAsync` custom-titles-only + built-ins stay pinned at 0-2, `ArchiveAsync` success + throws on `IsBuiltIn`
- [ ] **T050** [P] [US2] New `tests/StageFright.UI.Tests/Pages/Settings/CommitteeSettingsTabTests.cs` (bUnit, following `EventTypesTab.razor`'s structure): add/archive title flow, seat-count-target input persists, built-in rows show "Read-only" instead of an Archive button

### Implementation for User Story 2

**Wave 2 — independent (different files), depends on Wave 1:**

- [ ] **T051** [P] [US2] New `src/StageFright.UI/Pages/Settings/CommitteeSettingsTab.razor(.cs)` as a 5th hardcoded core tab (research D6 — not `ISettingsTabProvider`): office-holder title management mirroring `EventTypesTab.razor`'s add-form + active/archived `RadzenDataGrid` pattern, plus a `GeneralCommitteeSeatCountTarget` numeric input on `Settings`; merge-preserves every field it doesn't own (research D6's cross-tab reminder) and is added to every sibling tab's own merge-preserve list
- [ ] **T052** [US2] `src/StageFright.UI/Pages/Settings/SettingsPage.razor(.cs)`: add the "Committee" `<Tab>` (`CommitteeShown` flag, `OnClick`/lazy-render pattern matching the other 4 tabs), `DefaultTabIndex` switch gains `"committee" => 4`
- [ ] **T053** [P] [US2] `src/StageFright.UI/Pages/Setup/SetupWizard.razor(.cs)` + `SetupFormModel.cs`: new step 4 (office-holder titles + seat-count target + AGM month selection, shared with US3's T057), review becomes step 5, `_currentStep < 4` → `< 5`, "Step @_currentStep of 4" → "of 5"; committee configuration is optional at every default (FR-021)
- [ ] **T054** [US2] `src/StageFright.Core/Modules/Settings/SetupRequest.cs` + `SetupService.cs`: `SetupRequest` gains `IReadOnlyList<string> CommitteeOfficeHolderTitles`, `int? GeneralCommitteeSeatCountTarget`; `InitializeAsync` seeds any coordinator-entered custom titles via `ICommitteeOfficeHolderTypeService.AddAsync` and sets `Settings.GeneralCommitteeSeatCountTarget`, leaving both at defaults (empty/null) when the coordinator skipped the step

**Checkpoint**: Custom office-holder titles and the seat-count target are configurable from both Settings and setup, immediately available on the next AGM screen (SC-003).

---

## Phase 5: User Story 3 - AGM month drives committee terms, not the calendar year (Priority: P2)

**Goal**: The setup wizard's AGM-month selection determines committee term boundaries (AGM-to-AGM, not 1 Jan–31 Dec) and each term's label year (majority-of-days rule).

**Independent Test**: Set AGM month to October, record two AGMs a year apart, confirm each term runs AGM-to-AGM and is labeled with the correct year.

### Tests for User Story 3

**Wave 1 — independent (different files):**

- [ ] **T055** [P] [US3] Extend `tests/StageFright.Core.Tests/Modules/Agm/AgmServiceTests.cs` (from T041): `RecordAsync` computes `CommitteeTerm.LabelYear` correctly for an October AGM (labeled the following year) and a March AGM (labeled its own year) — the "majority of days" rule from FR-024/Assumptions
- [ ] **T056** [P] [US3] Update `tests/StageFright.Reports.Tests/CommitteeReportProviderTests.cs`: sections now group by `CommitteeTerm.LabelYear` (descending) instead of `Membership.Year`; legacy-only historical rows (`CommitteeTermId == null`) still group correctly by their own `Year`

### Implementation for User Story 3

**Wave 2 — independent (different files):**

- [ ] **T057** [P] [US3] `src/StageFright.UI/Pages/Setup/SetupWizard.razor` step 4 (from T053): add the "AGM Month" `InputSelect` bound to `_model.CommitteeRenewalMonth` (repurposed field, research D7), month-name dropdown pattern matching the existing renewal-month selects
- [ ] **T058** [US3] `src/StageFright.Reports/Providers/CommitteeReportProvider.cs`: re-key `GenerateAsync`'s grouping from `GroupBy(r => r.Membership.Year)` to `GroupBy(r => r.PositionRecord.CommitteeTermId is not null ? r.PositionRecord.CommitteeTerm.LabelYear : r.PositionRecord.Year)`; section heading becomes the resolved label year

**Checkpoint**: Every committee term recorded after this feature ships is dated AGM-to-AGM and labeled by majority-of-days year (SC-006), independently of US2 and US4.

---

## Phase 6: User Story 4 - Record a mid-term replacement (special election) (Priority: P3)

**Goal**: A coordinator can close out a departing office holder/committee member and record their replacement without a full AGM, preserving both holders' service dates.

**Independent Test**: Record an AGM assigning a position, then run a special election replacing that holder partway through the term; confirm both holders show with correct start/end dates.

### Tests for User Story 4

**Wave 1 — independent (different files):**

- [ ] **T059** [P] [US4] Extend `tests/StageFright.Core.Tests/Modules/Agm/AgmServiceTests.cs`: `RecordSpecialElectionAsync` success (closes outgoing record with `EndDate`, creates incoming record with `StartDate`), throws `DataIntegrityException` when the target term is already closed (Edge Case), throws `ValidationException` when the incoming member already holds an open slot in the term (FR-008 reused)
- [ ] **T060** [P] [US4] Extend `tests/StageFright.Reports.Tests/CommitteeReportProviderTests.cs` (from T056): a position with >1 holder in one term renders `"Name (StartDate–EndDate or 'present')"` per holder ordered by `StartDate`; a single-holder position still renders name-only, no dates (FR-029)
- [ ] **T061** [P] [US4] New `tests/StageFright.UI.Tests/Pages/Events/RecordSpecialElectionTests.cs`: outgoing/incoming member selection, replacement-date input, submit calls `IAgmService.RecordSpecialElectionAsync`

### Implementation for User Story 4

**Wave 2 — independent (different files):**

- [ ] **T062** [P] [US4] New `src/StageFright.UI/Pages/Events/RecordSpecialElection.razor(.cs)` at route `/events/agm/special-election/new`: select the currently-open term's filled positions, choose an incoming member, set the replacement date, submit
- [ ] **T063** [US4] `src/StageFright.Reports/Providers/CommitteeReportProvider.cs` `BuildPositionLines`: holder-count-aware formatter (single open holder → name only; ≥2 holders for the same `(CommitteeTermId, OfficeHolderTypeId)` → dated list ordered by `StartDate`) per contract's Report contract section

**Checkpoint**: A special election preserves both outgoing and incoming holders with correct dates, visible on screen and in print (SC-007), independently of US2/US3/US5.

---

## Phase 7: User Story 5 - Review a past AGM's record (Priority: P3)

**Goal**: A browsable, most-recent-first list of past AGMs, each opening to a read-only detail view; past AGMs can be archived.

**Independent Test**: Save AGMs via US1, confirm the list is browsable most-recent-first, and each entry opens to its correct read-only attendance/election detail.

### Tests for User Story 5

**Wave 1 — independent (different files):**

- [ ] **T064** [P] [US5] New `tests/StageFright.UI.Tests/Pages/Events/AgmListTests.cs`: renders past AGMs most-recent-first with date + attendance count, row click navigates to `/events/agm/{id}`
- [ ] **T065** [P] [US5] New `tests/StageFright.UI.Tests/Pages/Events/AgmDetailTests.cs` (covers T047's base read-only rendering from US1, plus): archive action calls `IAgmService.ArchiveAsync` and navigates back to the list; archived AGM no longer appears in `AgmList`'s default view

### Implementation for User Story 5

**Wave 2 — independent (different files):**

- [ ] **T066** [P] [US5] New `src/StageFright.UI/Pages/Events/AgmList.razor(.cs)` at route `/events/agm`: `RadzenDataGrid` of `IAgmService.GetPastAsync()` results, date + attendance count columns, most-recent-first, matches Members-grid reference conventions (`AllowSorting="true" AllowPaging="true" PageSize="15" class="rz-shadow-0"`)
- [ ] **T067** [US5] `src/StageFright.UI/Pages/Events/AgmDetail.razor(.cs)` (from T047): add the "Archive" action calling `IAgmService.ArchiveAsync`, redirecting to `/events/agm`

**Checkpoint**: Reviewing any past AGM takes no more than two navigation steps from the Events menu (SC-004), independently of US2/US3/US4.

---

## Phase 8: Polish

**Purpose**: Cross-cutting validation against spec.md's Success Criteria; the one remaining ripple update not owned by a single story.

- [ ] **T068** [P] New `tests/StageFright.Integration.Tests/Scenarios/V18_AgmWorkflowTests.cs` (research D12 — next sequential scenario after `V17_BankDepositTests.cs`): full journey — record an AGM, verify committee reporting picks it up (SC-002), record a special election, review a past AGM, archive it; `Data Source=:memory:` + real `Database.MigrateAsync()`, no DI container (existing integration-test convention)
- [ ] **T069** [P] Delete `tests/StageFright.Integration.Tests/Scenarios/V13_CommitteeResetAgmBannerTests.cs` (its coverage is superseded by T068 and the deleted `CommitteeAnnualResetService`)
- [ ] **T070** [P] Update `src/StageFright.App/Seeding/DebugDataSeeder.cs`: rewrite `SeedAgmAsync`/`SeedCommitteeAsync` against `IAgmService.RecordAsync` and the new office-holder-type/committee-term model (research D4 — dev/test fixture, regenerated fresh, not migrated)
- [ ] **T071** Remove the legacy one-member-at-a-time committee-position field from `src/StageFright.UI/Pages/Members/MemberForm.razor(.cs)`: delete the `isCommittee` checkbox + `CommitteePosition` text input (lines ~92-105 of the `.razor`), the `_isCommitteeMember`/`OnCommitteeCheckChanged` fields/handler, `MemberFormModel.CommitteePosition`, the `OnInitializedAsync` pre-population from `CommitteeService.GetHistoryAsync` (lines 44-51), and both `CommitteeService.AddOrUpdateAsync(...)` calls in `SaveAsync` (lines 96-97, 115-116) — this is the exact one-at-a-time workflow spec.md's problem statement frames as replaced by the new Record AGM screen, discovered during codebase verification and not covered by plan.md/data-model.md/contracts (flagged to and confirmed by the user); update `tests/StageFright.UI.Tests/Pages/Members/MemberFormTests.cs` (from T039) to remove its committee-checkbox assertions accordingly. `ICommitteeService.AddOrUpdateAsync`/`GetHistoryAsync` themselves stay (still used by `MemberDetail.razor.cs`'s read-only history display, T035)
- [ ] **T072** Run `dotnet build` and the full `dotnet test` suite (all five projects, no `--no-build`) and confirm every test is green; manually verify via `dotnet run --project src/StageFright.App/` that Record AGM → Past AGMs → AGM Detail → Special Election flows work end-to-end in the running app

---

## Dependencies & Execution Order

- **Phase 1 (Setup)** → **Phase 2 (Foundational)** → user story phases (3–7) → **Phase 8 (Polish)**.
- **Phase 2** internal order: Wave 1 (entities/Settings) → T008 (Member nav rename) → Wave 2 (configurations) → T015 (DbContext) → T016 (migration, sequential) → Wave 3 (repositories) → Wave 4 (services); Wave 5 (delete old reset mechanism), Wave 6 (EventType selectable filter), and Wave 7 (backup DTOs/mapping, depends only on T002–T006) can all run alongside Waves 1–4 since they touch disjoint files; T035 (MemberDetail) waits on T031's renamed DTO only insofar as it needs the renamed entity from T006 and `CommitteeTerm` from T005.
- **User stories 2, 3, 4, 5** each depend only on Phase 2 completing — not on each other or on US1's UI (though US1's `AgmService`/`RecordAgm.razor` is the natural way to generate the data US3/US4/US5 read). US1 is the priority build order (P1/MVP); US2 and US3 (P2) may be built in either order relative to each other; US4 and US5 (P3) likewise.
- Within each story, "Tests" wave(s) come before "Implementation" waves (write-tests-first per CLAUDE.md's coverage rule); implementation waves are internally ordered foundation-first (service before the UI that calls it).
- **Phase 8** depends on every user story phase being complete (Polish validates the whole feature against Success Criteria).
