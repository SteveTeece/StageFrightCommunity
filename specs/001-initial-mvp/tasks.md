# Tasks: StageFright Community — Initial MVP

**Input**: Design documents from `specs/001-initial-mvp/`

**Prerequisites**: plan.md ✅, spec.md ✅, data-model.md ✅, contracts/ ✅, quickstart.md ✅, research.md ✅

**Tests**: MANDATORY — Constitution §11.0 and NFR-005 require exhaustive code-path coverage. Test tasks are included in every user story phase.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing.

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks in same phase)
- **[Story]**: Which user story this task belongs to (US1–US11)
- Exact file paths in all descriptions

## Path Conventions

All source under `src/` and tests under `tests/` at repository root. Projects:
- `src/StageFright.App/` — MAUI Blazor Hybrid host
- `src/StageFright.Core/` — Domain + application logic (no EF, no UI)
- `src/StageFright.Data/` — Centralized DAL (EF Core + SQLite)
- `src/StageFright.Plugins.Contracts/` — Extension-point interfaces
- `src/StageFright.Reports/` — Shared report infrastructure
- `src/StageFright.UI/` — Razor class library (all application UI)
- `tests/StageFright.Core.Tests/` — Unit tests
- `tests/StageFright.Data.Tests/` — DAL integration tests
- `tests/StageFright.Reports.Tests/` — Report rendering tests
- `tests/StageFright.UI.Tests/` — bUnit component tests
- `tests/StageFright.Integration.Tests/` — Cross-layer acceptance tests
- `tests/StageFright.TestPlugin/` — Plugin fixture (SC-007)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Solution and project skeleton; NuGet packages; MAUI host wiring; Blazor entry point.

- [X] T001 Create `StageFrightCommunity.sln` and all 11 `.csproj` files (App, Core, Data, Plugins.Contracts, Reports, UI, Core.Tests, Data.Tests, Reports.Tests, UI.Tests, Integration.Tests) with correct project references and target frameworks
- [X] T002 [P] Add all NuGet packages: MAUI + Blazor Hybrid to App; EF Core 10 + SQLite to Data; Radzen.Blazor + Bootstrap 5.3 to UI; Serilog + OpenTelemetry to App; protobuf-net to Core; QuestPDF to Reports; CsvHelper to Reports; xUnit + bUnit + NSubstitute to test projects
- [X] T003 [P] Configure `src/StageFright.App/MauiProgram.cs` skeleton (DI builder, `AddMauiBlazorWebView`, platform targets Windows/MacCatalyst; plugin discovery and migrations to be filled in Phase 2)
- [X] T004 [P] Create `src/StageFright.App/MainPage.xaml` and `MainPage.xaml.cs` hosting a single `BlazorWebView` loaded programmatically (no XAML BlazorWebView, per commit `acdad7b` pattern)
- [X] T005 [P] Create `src/StageFright.App/wwwroot/index.html` (Bootstrap 5.3 CDN link, `app.css` link, Blazor script)
- [X] T006 [P] Create `src/StageFright.App/wwwroot/app.css` with CSS custom properties for light/dark pastel palette (HSL lightness 60–80%, saturation <50%; Finance balance colors HSL(120,35%,70%) green, HSL(0,35%,70%) red, HSL(0,0%,60%) neutral)
- [X] T007 [P] Create `src/StageFright.UI/App.razor` (Blazor `<Router>` entry point; `<Found>` renders `ShellLayout`; `<NotFound>` shows not-found component)
- [X] T008 [P] Create test project scaffolding: `tests/StageFright.Data.Tests/Infrastructure/DbContextFactory.cs` (SQLite in-memory helper), `tests/StageFright.Core.Tests/Fixtures/TestBase.cs`, shared `CancellationTokenSource` helpers

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: All 13 entities, all enums, all exceptions, all repository + plugin contracts, EF DbContext + configurations, all repository implementations, unit of work, core services, and MAUI composition root. No user-story work can start until this phase is complete.

**⚠️ CRITICAL**: User stories depend on all tasks in this phase.

### Entities (one file per type; all parallelizable)

- [ ] T009 [P] Create `src/StageFright.Core/Entities/Member.cs` (all fields from data-model.md: Id, Name, StreetAddress, Phone?, Email?, JoinDate, DateOfBirth?, Status, ActivateDate?, InactivateDate?, soft-delete fields, CreatedAt, UpdatedAt; XML docs on all public members)
- [ ] T010 [P] Create `src/StageFright.Core/Entities/CommitteeMembership.cs` (Id, MemberId FK, Year, Position, soft-delete fields, CreatedAt, UpdatedAt; unique constraint noted in XML docs)
- [ ] T011 [P] Create `src/StageFright.Core/Entities/Rehearsal.cs` (Id, Date, Time, Notes?, StoredAttendanceRate?, soft-delete fields, CreatedAt, UpdatedAt)
- [ ] T012 [P] Create `src/StageFright.Core/Entities/AttendanceRecord.cs` (Id, RehearsalId FK, MemberId FK, Attended bool, CreatedAt; soft-delete fields present but note immutability in XML docs; unique constraint noted)
- [ ] T013 [P] Create `src/StageFright.Core/Entities/Event.cs` (Id, Date, EventTypeId FK, Notes?, StoredParticipationRate?, soft-delete fields, CreatedAt, UpdatedAt)
- [ ] T014 [P] Create `src/StageFright.Core/Entities/EventType.cs` (Id, Name, IsSystemDefault bool, soft-delete fields, CreatedAt, UpdatedAt)
- [ ] T015 [P] Create `src/StageFright.Core/Entities/ParticipationRecord.cs` (Id, EventId FK, MemberId FK, Participated bool, CreatedAt, soft-delete fields; unique constraint noted)
- [ ] T016 [P] Create `src/StageFright.Core/Entities/Fee.cs` (Id, MemberId FK, FeeType, Amount decimal(18,2), FeeDate, DueDate, PaidAtCreation bool, RehearsalId? FK, CreatedAt; **NO soft-delete fields** per Constitution §3.4; XML docs on immutability)
- [ ] T017 [P] Create `src/StageFright.Core/Entities/Payment.cs` (Id, MemberId FK, Date, Amount decimal(18,2), PaymentMethod, PaymentType, Notes?, CreatedAt, UpdatedAt; **NO soft-delete fields**; XML docs on Notes-only mutability)
- [ ] T018 [P] Create `src/StageFright.Core/Entities/Transaction.cs` (Id, Date, CategoryId FK, DebitAmount, CreditAmount, GLAccount string, MemberId? FK, PaymentId? FK, FeeId? FK, Description?, CreatedAt; **NO soft-delete fields**; XML docs on paired-entry invariant)
- [ ] T019 [P] Create `src/StageFright.Core/Entities/Category.cs` (Id, Name, Type CategoryType, GLAccount string, SortOrder, IsSystem bool, soft-delete fields, CreatedAt, UpdatedAt; XML docs on auto-assignment and archive guard)
- [ ] T020 [P] Create `src/StageFright.Core/Entities/Settings.cs` (Id, OrganizationName, AnnualFee, AttendanceFee, MembershipRenewalMonth, CommitteeRenewalMonth, MaxAgeRangeYears, MinimumMemberAge, Theme, LastCommitteeResetYear?, SchemaVersion, soft-delete fields, CreatedAt, UpdatedAt)
- [ ] T021 [P] Create `src/StageFright.Core/Entities/AuditTrailEntry.cs` (Id, EntityType, EntityId Guid, Action AuditAction, OldValue?, NewValue?, UserId string, Timestamp; no soft-delete fields; XML docs on retention policy)

### Enums

- [ ] T022 [P] Create enums in `src/StageFright.Core/Enums/`: `MemberStatus.cs` {Active, Inactive}, `FeeType.cs` {Annual, Attendance, Other}, `PaymentMethod.cs` {Cash, Check, Card, ElectronicTransfer, Other}, `PaymentType.cs` {Annual, Attendance, Other}, `CategoryType.cs` {Income, Expense}, `Theme.cs` {Light, Dark}, `AuditAction.cs` {Create, Update, Delete, Restore, StatusChange, Forgiveness, CommitteeReset, Import, Export}, `ReportColumnAlignment.cs`, `ReportColumnFormat.cs`, `ReportFilterType.cs` (all with XML docs on all enum values; one file per enum)

### Exceptions

- [ ] T023 [P] Create `src/StageFright.Core/Exceptions/ValidationException.cs`, `DataAccessException.cs`, `EntityNotFoundException.cs`, `DuplicateEntityException.cs`, `GLBalanceException.cs` (each extends `Exception`; includes EntityType, EntityId, OperationContext, Timestamp, CorrelationId, inner exception; XML docs; one class per file)
- [ ] T024 [P] Create `src/StageFright.Core/Exceptions/ImportException.cs`, `PluginLoadException.cs`, `ConcurrencyException.cs`, `DataIntegrityException.cs` (same pattern as T023; one class per file)

### Repository + Service Contracts

- [ ] T025 [P] Create `src/StageFright.Core/Contracts/IRepository.cs` (GetByIdAsync, GetAllAsync, AddAsync, UpdateAsync) and `ISoftDeletableRepository.cs` (extends IRepository; ArchiveAsync, RestoreAsync, GetArchivedAsync) with XML docs
- [ ] T026 [P] Create `src/StageFright.Core/Contracts/IMemberRepository.cs` (extends ISoftDeletable; GetByStatusAsync, GetActiveAsOfAsync) and `ICommitteeMembershipRepository.cs` (extends ISoftDeletable; GetByMemberAsync, GetByYearAsync, SoftDeleteCurrentYearAsync)
- [ ] T027 [P] Create `src/StageFright.Core/Contracts/IRehearsalRepository.cs` (GetMostRecentPastAsync), `IEventRepository.cs` (GetMostRecentPastAsync, AgmExistsInYearAsync), `IEventTypeRepository.cs`, `IAttendanceRepository.cs` (ExistsAsync, AddBatchAsync), `IParticipationRepository.cs`
- [ ] T028 [P] Create `src/StageFright.Core/Contracts/IFeeRepository.cs` (GetByIdAsync, AddAsync, GetByMemberAsync, GetUnpaidOrderedFifoAsync, AnnualFeeExistsAsync, AttendanceFeeExistsAsync — no Update/Delete per immutability), `IPaymentRepository.cs` (GetByIdAsync, AddAsync, UpdateNotesAsync, GetByMemberAsync), `IGLRepository.cs` (AddPairAsync, GetMemberBalanceAsync, GetTotalOutstandingAsync, GetByDateRangeAsync, GetByMemberAsync, GetBalanceTotalsAsync)
- [ ] T029 [P] Create `src/StageFright.Core/Contracts/ICategoryRepository.cs` (IsReferencedByTransactionsAsync, GetNextGLAccountAsync, ReorderAsync), `ISettingsRepository.cs` (GetAsync, SaveAsync), `IAuditTrailRepository.cs` (AddAsync, GetByEntityAsync, PurgeOlderThanAsync), `IUnitOfWork.cs` (ExecuteInTransactionAsync), `IAuditTrailService.cs` (LogAsync overloads)

### Plugin Contracts

- [ ] T030 [P] Create `src/StageFright.Plugins.Contracts/IDashboardTileProvider.cs` (TileId, Title, ModuleName, DisplayOrder, TileComponentType, GetTileDataAsync) and `TileData.cs` (Metrics dict, AccentColor?, NavigateRoute?)
- [ ] T031 [P] Create `src/StageFright.Plugins.Contracts/ISettingsTabProvider.cs` (TabTitle, TabIcon, TabKey, DisplayOrder, SettingsComponentType), `IMenuItemProvider.cs` (ModuleName, DisplayOrder, GetMenuItems), `MenuItem.cs` (Title, Route, Icon?, DisplayOrder, SubItems, BadgeText?)
- [ ] T032 [P] Create `src/StageFright.Plugins.Contracts/IDataAccessProvider.cs` (PluginName, DbContextType, RegisterServices)
- [ ] T033 [P] Create `src/StageFright.Plugins.Contracts/IReportProvider.cs` (ReportId, ReportName, ModuleName, DisplayOrder, Filters, GenerateAsync)

### Report Data Models

- [ ] T034 [P] Create `src/StageFright.Reports/Models/ReportData.cs` (Title, SubTitle?, GeneratedAt, Columns, Sections, GrandTotal?), `ReportColumn.cs` (Header, Alignment, Format), `ReportSection.cs` (Heading?, Rows, Subtotal?), `ReportRow.cs` (Cells, IsEmphasized)
- [ ] T035 [P] Create `src/StageFright.Reports/Models/ReportFilterDefinition.cs` (Key, Type, Label, Options, DefaultValue), `ReportFilterValues.cs` (Dictionary<string,string>), `ReportMenuSection.cs` (ModuleName, Reports list)
- [ ] T036 [P] Create `src/StageFright.Reports/Registry/IReportProviderRegistry.cs` (GetMenuSections, GetProvider)
- [ ] T037 [P] Create `src/StageFright.Reports/Rendering/IPdfReportRenderer.cs` (Render returns byte[]) and `ICsvReportExporter.cs` (Export returns string)

### EF Core DbContext and Configurations

- [ ] T038 Create `src/StageFright.Data/StageFrightDbContext.cs` (DbSet<> per entity; soft-delete global query filters; constructor accepting DbContextOptions; seeding system categories Cash/MemberReceivable/BadDebtExpense on model creation)
- [ ] T039 [P] Create 13 `IEntityTypeConfiguration<T>` classes in `src/StageFright.Data/Configurations/` (one per entity: `MemberConfiguration.cs`, `CommitteeMembershipConfiguration.cs`, `RehearsalConfiguration.cs`, `AttendanceRecordConfiguration.cs`, `EventConfiguration.cs`, `EventTypeConfiguration.cs`, `ParticipationRecordConfiguration.cs`, `FeeConfiguration.cs`, `PaymentConfiguration.cs`, `TransactionConfiguration.cs`, `CategoryConfiguration.cs`, `SettingsConfiguration.cs`, `AuditTrailEntryConfiguration.cs`; each sets HasPrecision(18,2) for decimals, unique indexes, DeleteBehavior.Restrict, soft-delete query filters where applicable)
- [ ] T040 Create initial EF Core database migration `InitialCreate` in `src/StageFright.Data/Migrations/` using `dotnet ef migrations add InitialCreate` targeting `StageFrightDbContext`

### Repository Implementations

- [ ] T041 [P] Create `src/StageFright.Data/Repositories/BaseRepository.cs` (generic IRepository<T> impl; GetByIdAsync, GetAllAsync, AddAsync, UpdateAsync; translates EF exceptions to DataAccessException/EntityNotFoundException/DuplicateEntityException) and `SoftDeletableBaseRepository.cs` (extends BaseRepository; ArchiveAsync, RestoreAsync, GetArchivedAsync; validates not-already-deleted)
- [ ] T042 [P] Create `src/StageFright.Data/Repositories/MemberRepository.cs` (GetByStatusAsync, GetActiveAsOfAsync using ActivateDate/InactivateDate effective-date query) and `CommitteeMembershipRepository.cs` (GetByMemberAsync, GetByYearAsync, SoftDeleteCurrentYearAsync)
- [ ] T043 [P] Create `src/StageFright.Data/Repositories/RehearsalRepository.cs` (GetMostRecentPastAsync), `AttendanceRepository.cs` (ExistsAsync, AddBatchAsync — no soft-delete operations exposed), `EventRepository.cs` (GetMostRecentPastAsync, AgmExistsInYearAsync), `EventTypeRepository.cs`, `ParticipationRepository.cs`
- [ ] T044 [P] Create `src/StageFright.Data/Repositories/FeeRepository.cs` (no Update/Delete methods; GetUnpaidOrderedFifoAsync uses GL-derived balance: FeeDate ASC, CreatedAt ASC, Id ASC), `PaymentRepository.cs` (UpdateNotesAsync validates only Notes changed, bumps UpdatedAt, audits old/new), `GLRepository.cs` (AddPairAsync validates equal amounts; GetMemberBalanceAsync: Σdebits − Σcredits; GetBalanceTotalsAsync for Trial Balance)
- [ ] T045 [P] Create `src/StageFright.Data/Repositories/CategoryRepository.cs` (IsReferencedByTransactionsAsync checks any Transaction.CategoryId match; GetNextGLAccountAsync: counts existing Income/Expense categories by CreatedAt ASC to determine next 10xx/20xx), `SettingsRepository.cs` (GetAsync/SaveAsync singleton), `AuditTrailRepository.cs` (AddAsync, GetByEntityAsync, PurgeOlderThanAsync — hard delete log records only)
- [ ] T046 Create `src/StageFright.Data/UnitOfWork.cs` (implements IUnitOfWork; wraps IDbContextTransaction; on exception → RollbackAsync; verifies GL balance via IGLRepository after each financial operation)
- [ ] T047 [P] Create `src/StageFright.Data/PluginData/PluginMigrationRunner.cs` (discovers IDataAccessProvider implementations; constructs plugin DbContext on shared connection string with `__EFMigrationsHistory_{PluginName}` table; runs Database.Migrate(); catches failures → PluginLoadException + log + skip)

### Core Application Services

- [ ] T048 [P] Create `src/StageFright.Core/Modules/AuditTrailService.cs` (implements IAuditTrailService; LogAsync(entityType, entityId, action, oldValue, newValue, userId="system"); calls IAuditTrailRepository.AddAsync; startup purge PurgeOlderThanAsync(now − 12 months) with failure tolerance)
- [ ] T049 [P] Create `src/StageFright.Core/Modules/Finance/GLAccountAssignmentService.cs` (AssignNextAsync(CategoryType): queries ICategoryRepository.GetNextGLAccountAsync; Income → "10{nn}", Expense → "20{nn}"; fixed accounts: Cash="0100", MemberReceivable="0101", BadDebtExpense="9900")

### MAUI Composition Root

- [ ] T050 Complete `src/StageFright.App/MauiProgram.cs` with: Serilog configuration (file + console sinks, structured properties), OpenTelemetry traces + metrics, all repository + service DI registrations, startup sequence (core migration → plugin discovery → PluginMigrationRunner → audit purge → `Plugins/` directory auto-creation)
- [ ] T051 [P] Create `src/StageFright.App/PluginLoader.cs` (scans `Plugins/*.dll`; loads each in AssemblyLoadContext; reflects for IDashboardTileProvider, ISettingsTabProvider, IMenuItemProvider, IReportProvider, IDataAccessProvider implementations; registers found providers; catches per-assembly failures → PluginLoadException log → skip)

### Blazor UI Shell

- [ ] T052 Create `src/StageFright.UI/Layout/ShellLayout.razor` and `ShellLayout.razor.cs` (dark brand strip with purple "StageFright" wordmark, white navigation bar; injects `IEnumerable<IMenuItemProvider>` for nav items; Dashboard always first, Settings always last; `NavigationManager.NavigateTo` for all navigation; tab-accessible)
- [ ] T053 [P] Create `src/StageFright.UI/Pages/Settings/SettingsPage.razor` and `SettingsPage.razor.cs` (`@page "/settings"`; tab container rendering `IEnumerable<ISettingsTabProvider>` ordered by DisplayOrder; deep-link via `?tab=` query param; failing tab provider skipped gracefully)

### Foundational Tests

- [ ] T054 [P] Create entity field constraint unit tests in `tests/StageFright.Core.Tests/Entities/` (verify Fee/Payment/Transaction have NO IsDeleted field via reflection; verify Member soft-delete fields present; verify decimal precision attributes; verify unique constraint annotations)
- [ ] T055 [P] Create repository integration tests in `tests/StageFright.Data.Tests/` using SQLite in-memory connections: CRUD for all 13 entity repositories, global query filter (soft-deleted records excluded from GetAllAsync), ArchiveAsync/RestoreAsync, UpdateNotes-only immutability on Payment, FeeRepository no-Update enforcement

**Checkpoint**: Foundation ready — all entities, repositories, DAL, DI, Blazor shell, and base infrastructure complete. User story implementation can begin.

---

## Phase 3: User Story 1 — First-Run Setup (Priority: P1) 🎯 MVP

**Goal**: On first launch (no Settings record), display setup wizard; on save, initialize Settings singleton + seed system categories; redirect to dashboard. No fees created.

**Independent Test**: Launch app with empty DB → wizard appears → enter valid data → dashboard displays → DB has zero Fee records → system categories present.

### Tests for User Story 1

- [ ] T056 [P] [US1] Write SetupWizard validation unit tests in `tests/StageFright.Core.Tests/Modules/Settings/SetupServiceTests.cs` (all fields mandatory, fees ≥ 0, renewal month 1–12) — ensure tests FAIL before implementation
- [ ] T057 [P] [US1] Write SetupService integration tests in `tests/StageFright.Data.Tests/Settings/SetupServiceIntegrationTests.cs` (Settings persisted with correct values, system categories Cash/MemberReceivable/BadDebtExpense seeded with correct GL accounts, zero Fee records after setup) — ensure tests FAIL before implementation

### Implementation for User Story 1

- [ ] T058 [P] [US1] Create `src/StageFright.Core/Modules/Settings/SetupService.cs` (InitializeAsync: creates Settings singleton, seeds system categories with fixed GL accounts, logs setup-completion event; IsSetupCompleteAsync: checks Settings != null)
- [ ] T059 [P] [US1] Create `src/StageFright.Core/Modules/Settings/SettingsService.cs` (GetAsync, SaveAsync, thin wrapper over ISettingsRepository; audits changes)
- [ ] T060 [US1] Implement first-run detection in `src/StageFright.UI/App.razor`: `OnInitializedAsync` checks `ISetupService.IsSetupCompleteAsync()`; if false → `NavigationManager.NavigateTo("/setup")`
- [ ] T061 [P] [US1] Create `src/StageFright.UI/Pages/Setup/SetupWizard.razor` and `SetupWizard.razor.cs` (`@page "/setup"`; form fields: OrganizationName required, AnnualFee ≥ 0, AttendanceFee ≥ 0, MembershipRenewalMonth 1–12; Bootstrap 5.3 validation; on valid submit → `SetupService.InitializeAsync()` → navigate to `/dashboard`)
- [ ] T062 [P] [US1] Create bUnit tests in `tests/StageFright.UI.Tests/Pages/Setup/SetupWizardTests.cs` (renders form, all fields mandatory, fee < 0 rejected, month 13 rejected, valid submit calls SetupService + navigates)
- [ ] T063 [US1] Create integration acceptance test `tests/StageFright.Integration.Tests/Scenarios/V1_FirstRunSetupTests.cs` (end-to-end: empty DB → wizard render → fill valid data → save → redirect → Settings persisted → zero Fees → system categories present)

**Checkpoint**: US1 complete — first-run setup fully functional and independently testable.

---

## Phase 4: User Story 2 — Member Registration and Management (Priority: P1)

**Goal**: Member CRUD, Active/Inactive status with effective dates, committee membership per year, age calculation, committee history with ARIA badge.

**Independent Test**: Create members with/without DOB, toggle inactive, mark committee, view history, verify age — independently of rehearsals/fees.

### Tests for User Story 2

- [ ] T064 [P] [US2] Write `AgeCalculationServiceTests.cs` in `tests/StageFright.Core.Tests/Modules/Members/` (all FR-002a examples: DOB=1990-02-28 today=2026-02-27 → 35; leap DOB=1992-02-29 today=2026-02-28 → 33; future DOB rejected; today DOB rejected; age < MinimumMemberAge rejected with exact error message) — FAIL first
- [ ] T065 [P] [US2] Write `MemberServiceTests.cs` (create default Active, inactivate sets InactivateDate, reactivate sets ActivateDate, audit entry on status change, **inactivation does NOT cascade to CommitteeMembership records** (remain intact), archive cascades CommitteeMembership soft-delete for current year, committee-less member has no history section)
- [ ] T066 [P] [US2] Write `MemberRepositoryIntegrationTests.cs` in `tests/StageFright.Data.Tests/Repositories/` (GetByStatus Active/Inactive, GetActiveAsOfAsync effective-date query, archived member excluded from GetAllAsync)

### Implementation for User Story 2

- [ ] T067 [P] [US2] Create `src/StageFright.Core/Modules/Members/AgeCalculationService.cs` (UTC-based algorithm: age = today.Year − dob.Year; decrement if today < birthday this year; returns null when DateOfBirth null)
- [ ] T068 [P] [US2] Create `src/StageFright.Core/Modules/Members/MemberValidationService.cs` (ValidateAsync: required name/address/joinDate; email Regex; phone Regex; DOB < today, within MaxAgeRangeYears, resulting age ≥ MinimumMemberAge; throws ValidationException with FR-002a verbatim messages)
- [ ] T069 [US2] Create `src/StageFright.Core/Modules/Members/MemberService.cs` (CreateAsync, UpdateAsync, GetByIdAsync, GetByStatusAsync, ArchiveAsync (cascades soft-delete to CommitteeMembership current-year records), InactivateAsync (sets InactivateDate+audit; **does NOT cascade to CommitteeMembership** — assignments remain intact), ActivateAsync sets ActivateDate+triggers ReactivationForgivenessService if prior fees exist+audit; uses IUnitOfWork for status-change+audit atomicity)
- [ ] T070 [P] [US2] Create `src/StageFright.Core/Modules/Members/CommitteeService.cs` (AddOrUpdateAsync: Position required, (MemberId, Year) unique; GetHistoryAsync: ordered year DESC; SoftDeleteCurrentYearAsync called by CommitteeAnnualResetService)
- [ ] T071 [P] [US2] Create `src/StageFright.Core/Modules/Members/MemberMenuItemProvider.cs` (ModuleName="Members", DisplayOrder=1; menu item "/members" with subitems: Active Members, Add Member)
- [ ] T072 [P] [US2] Create `src/StageFright.Core/Modules/Dashboard/MembersDashboardTileProvider.cs` (TileId="members", DisplayOrder=10; GetTileDataAsync: active count, inactive count)
- [ ] T073 [P] [US2] Create `src/StageFright.UI/Pages/Members/MemberList.razor` and `MemberList.razor.cs` (`@page "/members"`; Radzen DataGrid; filter Active/Inactive toggle; Age column if DOB present; navigate to detail/edit)
- [ ] T074 [P] [US2] Create `src/StageFright.UI/Pages/Members/MemberForm.razor` and `MemberForm.razor.cs` (`@page "/members/new"`, `@page "/members/edit/{id:guid}"`; all fields per FR-002; Committee Member checkbox shows Position field when checked; Bootstrap 5.3 validation feedback)
- [ ] T075 [P] [US2] Create `src/StageFright.UI/Pages/Members/MemberDetail.razor` and `MemberDetail.razor.cs` (`@page "/members/{id:guid}"`; shows profile, calculated age if DOB present, Committee History section; current year rendered as `<strong>YYYY <span role="status" aria-label="Current year">Current</span> - Position</strong>` with HSL(120,40%,70%) badge; historical as plain `<span>`)
- [ ] T076 [P] [US2] Create bUnit tests in `tests/StageFright.UI.Tests/Pages/Members/` for MemberForm (required-field validation, age validation message exact text, committee checkbox→position required, submit) and MemberDetail (ARIA badge on current year, no section when no history)
- [ ] T077 [US2] Create integration acceptance test `tests/StageFright.Integration.Tests/Scenarios/V2_MemberManagementTests.cs` (create with/without DOB → age; invalid email/phone/future DOB → errors; inactivate → hidden from Active list; committee history display with badge)

**Checkpoint**: US2 complete — member management independently functional.

---

## Phase 5: User Story 3 — Rehearsal Scheduling and Attendance Recording (Priority: P1)

**Goal**: Schedule rehearsals, batch attendance grid, atomic Fee+GL creation, PaidAtCreation defaults, StoredAttendanceRate frozen. Records immutable after save.

**Independent Test**: Schedule rehearsal → batch attendance → verify fees created (paid by default) → verify GL pairs → verify StoredAttendanceRate → no edit UI exists.

### Tests for User Story 3

- [ ] T078 [P] [US3] Write `AttendanceServiceTests.cs` in `tests/StageFright.Core.Tests/Modules/Rehearsals/` (active member attended unchecked → Fee PaidAtCreation=true + GL debit MemberReceivable/credit Income + debit Cash/credit MemberReceivable + **auto-created Payment record (PaymentMethod=Cash, PaymentType=Attendance, Amount=Settings.AttendanceFee)**; "Mark as unpaid" checked → Fee PaidAtCreation=false + GL debit MemberReceivable/credit Income only + **no Payment record created**; inactive member → no Fee created; duplicate attendance → idempotent; batch is atomic) — FAIL first
- [ ] T079 [P] [US3] Write `RehearsalServiceTests.cs` (StoredAttendanceRate = present/active-as-of-date × 100; archived members excluded from denominator; rate frozen after recording)

### Implementation for User Story 3

- [ ] T080 [P] [US3] Create `src/StageFright.Core/Modules/Rehearsals/RehearsalService.cs` (ScheduleAsync, GetAllAsync, GetMostRecentPastAsync; after attendance saved: compute active-as-of-date count via IMemberRepository.GetActiveAsOfAsync, store StoredAttendanceRate immutably)
- [ ] T081 [US3] Create `src/StageFright.Core/Modules/Rehearsals/AttendanceService.cs` (RecordBatchAsync: inside IUnitOfWork; for each member: skip if not Attended; check idempotency via IAttendanceRepository.ExistsAsync; create AttendanceRecord; if active member → create Fee (PaidAtCreation per UI flag) + GL accrual pair (Debit MemberReceivable / Credit Income); if PaidAtCreation=true → additionally create GL payment pair (Debit Cash / Credit MemberReceivable) **and auto-create Payment record (PaymentMethod=Cash, PaymentType=Attendance, Amount=Settings.AttendanceFee, Date=rehearsal.Date)**; if inactive → no Fee; verify GL balance; commit)
- [ ] T082 [P] [US3] Create `src/StageFright.Core/Modules/Dashboard/RehearsalsDashboardTileProvider.cs` (TileId="rehearsals", DisplayOrder=20; GetTileDataAsync: most recent past rehearsal date + StoredAttendanceRate)
- [ ] T083 [P] [US3] Create `src/StageFright.Core/Modules/Rehearsals/RehearsalMenuItemProvider.cs` (ModuleName="Rehearsals", DisplayOrder=2; "/rehearsals")
- [ ] T084 [P] [US3] Create `src/StageFright.UI/Pages/Rehearsals/RehearsalList.razor` and `RehearsalList.razor.cs` (`@page "/rehearsals"`; list with date/time/attendance rate; link to attendance recording)
- [ ] T085 [P] [US3] Create `src/StageFright.UI/Pages/Rehearsals/RehearsalForm.razor` and `RehearsalForm.razor.cs` (`@page "/rehearsals/new"`; date, time required; notes optional)
- [ ] T086 [P] [US3] Create `src/StageFright.UI/Pages/Rehearsals/AttendanceGrid.razor` and `AttendanceGrid.razor.cs` (`@page "/rehearsals/{id:guid}/attendance"`; table: Member Name | Attended ☐ | Mark as unpaid ☐ | Fee amount; batch Save/OK button; calls AttendanceService.RecordBatchAsync; no edit after save)
- [ ] T087 [P] [US3] Create bUnit test `tests/StageFright.UI.Tests/Pages/Rehearsals/AttendanceGridTests.cs` (renders all active members; default unchecked "Mark as unpaid"; save triggers service; no edit route exists post-save)
- [ ] T088 [US3] Create integration acceptance test `tests/StageFright.Integration.Tests/Scenarios/V3_RehearsalAttendanceTests.cs` (schedule → batch attendance → verify Fee PaidAtCreation + GL pairs created atomically → StoredAttendanceRate frozen → no clear UI)

**Checkpoint**: US3 complete — rehearsal scheduling and fee accrual independently functional.

---

## Phase 6: User Story 4 — Annual Membership Fee Application (Priority: P1)

**Goal**: Batch apply annual fees to all eligible active members. Skip inactive, skip any existing current-year annual fee (paid or unpaid). Confirmation dialog with count.

**Independent Test**: Active + inactive members present → Apply Annual Fees → only active eligible charged → GL pairs created → Finance tile balance updates.

### Tests for User Story 4

- [ ] T089 [P] [US4] Write `FeeServiceTests.cs` in `tests/StageFright.Core.Tests/Modules/Finance/` (active eligible → Fee + GL; inactive → skipped; **existing current-year annual fee (paid or unpaid) → skipped**; batch atomic rollback on failure) — FAIL first
- [ ] T090 [P] [US4] Write `FeeRepositoryIntegrationTests.cs` in `tests/StageFright.Data.Tests/` (AnnualFeeExistsAsync true/false; AttendanceFeeExistsAsync idempotency check)

### Implementation for User Story 4

- [ ] T091 [P] [US4] Create `src/StageFright.Core/Modules/Finance/FeeService.cs` (GetEligibleMembersAsync: active, **no existing annual fee record for current year (paid or unpaid)**; ApplyAnnualFeesAsync(memberIds): IUnitOfWork; per member → Fee(Annual, Settings.AnnualFee, FeeDate=Jan1, DueDate=Dec31, PaidAtCreation=false) + GL debit MemberReceivable/credit Income category + audit; returns count)
- [ ] T092 [P] [US4] Create `src/StageFright.Core/Modules/Dashboard/FinanceDashboardTileProvider.cs` (TileId="finance", DisplayOrder=40; GetTileDataAsync: IGLRepository.GetTotalOutstandingAsync; AccentColor: >0 → HSL(120,35%,70%), <0 → HSL(0,35%,70%), =0 → HSL(0,0%,60%))
- [ ] T093 [P] [US4] Create `src/StageFright.Core/Modules/Finance/FinanceMenuItemProvider.cs` (ModuleName="Finance", DisplayOrder=4; "/finance" with subitems: Balances, Payments, Apply Annual Fees)
- [ ] T094 [P] [US4] Create `src/StageFright.UI/Pages/Finance/AnnualFeeApplication.razor` and `AnnualFeeApplication.razor.cs` (confirmation dialog shows eligible count; on confirm → FeeService.ApplyAnnualFeesAsync; success message with count applied)
- [ ] T095 [P] [US4] Create bUnit test `tests/StageFright.UI.Tests/Pages/Finance/AnnualFeeApplicationTests.cs` (dialog renders with count; confirm triggers service; cancels without applying)
- [ ] T096 [US4] Create integration acceptance test `tests/StageFright.Integration.Tests/Scenarios/V4_AnnualFeeApplicationTests.cs` (active+inactive members → apply → only active eligible billed → balance updated in Finance tile)

**Checkpoint**: US4 complete — annual fee batch independently functional.

---

## Phase 7: User Story 7 — Category Management for Income and Expenses (Priority: P1)

**Goal**: Category CRUD in Settings > Categories tab. GL auto-assigned sequentially. Archive blocked if referenced. Reorder supported.

**Independent Test**: Create income/expense categories → verify GL assignment (10xx/20xx) → archive referenced one → blocked; archive unreferenced → succeeds; restore works; reorder persists.

### Tests for User Story 7

- [ ] T097 [P] [US7] Write `GLAccountAssignmentServiceTests.cs` in `tests/StageFright.Core.Tests/Modules/Finance/` (first income → GL#1000; second income → GL#1001; first expense → GL#2000; creation-order determinism) — FAIL first
- [ ] T098 [P] [US7] Write `CategoryServiceTests.cs` (archive blocked while Transaction references; archive unblocked after GL reversals; system categories immutable; reorder updates SortOrder)
- [ ] T099 [P] [US7] Write `CategoryRepositoryIntegrationTests.cs` in `tests/StageFright.Data.Tests/` (IsReferencedByTransactionsAsync true/false; GetNextGLAccountAsync sequential ordering by CreatedAt ASC)

### Implementation for User Story 7

- [ ] T100 [P] [US7] Create `src/StageFright.Core/Modules/Settings/CategoryService.cs` (CreateAsync: assigns GL via GLAccountAssignmentService, audits; ArchiveAsync: checks IsReferencedByTransactionsAsync first, throws ValidationException if referenced; RestoreAsync; ReorderAsync; system categories (IsSystem=true) blocked from edit/archive)
- [ ] T101 [P] [US7] Create `src/StageFright.UI/Pages/Settings/CategorySettingsTabProvider.cs` (TabTitle="Categories", TabKey="categories", DisplayOrder=10, SettingsComponentType=typeof(SettingsCategoryTab))
- [ ] T102 [P] [US7] Create `src/StageFright.UI/Pages/Settings/SettingsCategoryTab.razor` and `SettingsCategoryTab.razor.cs` (list income + expense categories with GL account column; create form; archive/restore buttons; drag-reorder; system categories shown as read-only)
- [ ] T103 [P] [US7] Create bUnit test `tests/StageFright.UI.Tests/Pages/Settings/SettingsCategoryTabTests.cs` (renders by type; create; archive blocked message; restore; system category buttons disabled)
- [ ] T104 [US7] Create integration acceptance test `tests/StageFright.Integration.Tests/Scenarios/V7_CategoryManagementTests.cs` (create income + expense → GL sequential; archive referenced → blocked; archive unreferenced → archive view; reorder persists)

**Checkpoint**: US7 complete — category management independently functional.

---

## Phase 8: User Story 8 — Dashboard Overview and Plugin Extensibility (Priority: P1)

**Goal**: 4 core tiles load in parallel; failed/slow tiles degrade gracefully; plugin tiles render in separate Extensions section; test plugin verified.

**Independent Test**: 4 tiles visible; deliberate failure tile shows "Unable to load" without blocking others; TestPlugin tile appears in Extensions section.

### Tests for User Story 8

- [ ] T105 [P] [US8] Write `DashboardServiceTests.cs` in `tests/StageFright.Core.Tests/Modules/Dashboard/` (parallel loading; throwing provider isolated; provider ordering Core 0-99 / plugin 100+) — FAIL first
- [ ] T106 [P] [US8] Write `DashboardTests.razor` bUnit tests in `tests/StageFright.UI.Tests/Pages/Dashboard/` (4 core tiles render; loading state shown; error tile shows "Unable to load"; Extensions section present when plugin registered)

### Implementation for User Story 8

- [ ] T107 [US8] Create `src/StageFright.Core/Modules/Dashboard/DashboardService.cs` (GetTilesAsync: returns providers ordered by DisplayOrder; LoadTileAsync: wraps GetTileDataAsync in try/catch → TileLoadResult with Data or Error; callers initiate all loads in parallel via Task.WhenAll)
- [ ] T108 [P] [US8] Create `src/StageFright.Core/Modules/Dashboard/DashboardMenuItemProvider.cs` (ModuleName="Dashboard", DisplayOrder=0; single item "/dashboard", always first)
- [ ] T109 [P] [US8] Create `src/StageFright.UI/Pages/Dashboard/Dashboard.razor` and `Dashboard.razor.cs` (`@page "/dashboard"`; two sections: Core Metrics 2-column grid (DisplayOrder 0-99), Extensions 2-column grid (DisplayOrder 100+); loads all tiles in parallel via DashboardService; each tile in TileRenderer.razor)
- [ ] T110 [P] [US8] Create `src/StageFright.UI/Shared/TileRenderer.razor` (renders IDashboardTileProvider tile body component; shows spinner while loading; shows "Unable to load [TileTitle]" with structured error on failure; never blocks siblings)
- [ ] T111 [P] [US8] Create `tests/StageFright.TestPlugin/TestTileProvider.cs` (implements IDashboardTileProvider; DisplayOrder=100; GetTileDataAsync returns test metrics)
- [ ] T112 [US8] Create integration acceptance test `tests/StageFright.Integration.Tests/Scenarios/V8_DashboardPluginTests.cs` (4 core tiles load; deliberately slow tile doesn't block; TestPlugin tile in Extensions section; missing-dep plugin logged and skipped)

**Checkpoint**: US8 complete — dashboard with parallel loading and plugin extensibility functional.

---

## Phase 9: User Story 6 — Finance Tracking and Outstanding Balance Visibility (Priority: P1)

**Goal**: Payment recording with FIFO allocation, GL pairs per allocation, balance from GL, reactivation forgiveness, Payment immutability (Notes-only editable).

**Independent Test**: Create fees → apply payment → FIFO oldest-first → balance = Σdebits − Σcredits; reactivation dialog shows fees by year; forgiven fees write off to BadDebtExpense.

### Tests for User Story 6

- [ ] T113 [P] [US6] Write `PaymentServiceTests.cs` in `tests/StageFright.Core.Tests/Modules/Finance/` (FIFO: oldest FeeDate first; partial payment; overpayment → credit GL; GL pairs created; Amount/Date/Method/Type/Category rejected on update; Notes update audits old/new) — FAIL first
- [ ] T114 [P] [US6] Write `ReactivationForgivenessServiceTests.cs` (prior-year fees pre-selected; current-year unchecked; GL write-off pairs to GL#9900 per selected fee; audit entries with default/override flag; Fee records untouched)
- [ ] T115 [P] [US6] Write `GLRepositoryIntegrationTests.cs` in `tests/StageFright.Data.Tests/` (GetMemberBalanceAsync, GetTotalOutstandingAsync, GetBalanceTotalsAsync, date-range query)
- [ ] T116 [P] [US6] Write `PaymentRepositoryIntegrationTests.cs` (UpdateNotesAsync bumps UpdatedAt; rejected updates to Amount/Date/Method/Type/Category throw ValidationException)

### Implementation for User Story 6

- [ ] T117 [US6] Create `src/StageFright.Core/Modules/Finance/PaymentService.cs` (RecordAsync: IUnitOfWork; create Payment; FIFO via IFeeRepository.GetUnpaidOrderedFifoAsync; per fee: full or partial GL debit Cash/credit MemberReceivable pair; overpayment: GL debit MemberReceivable/credit Cash pair; verify balance; audit; UpdateNotesAsync: delegates to IPaymentRepository.UpdateNotesAsync)
- [ ] T118 [P] [US6] Create `src/StageFright.Core/Modules/Finance/ReactivationForgivenessService.cs` (GetForgivenessItemsAsync(memberId): returns fees grouped by year, prior years flagged IsDefaultForgiven=true; ApplyForgivenessAsync(memberId, selectedFeeIds): IUnitOfWork; per selected fee → GL debit MemberReceivable/credit BadDebtExpense(GL#9900) pair; audit entry Action=Forgiveness with old/new)
- [ ] T119 [P] [US6] Create `src/StageFright.Core/Modules/Finance/MemberBalanceService.cs` (GetBalanceAsync(memberId): IGLRepository.GetMemberBalanceAsync; GetAllMemberBalancesAsync: per-member GL query)
- [ ] T120 [P] [US6] Create `src/StageFright.UI/Pages/Finance/FinancePage.razor` and `FinancePage.razor.cs` (`@page "/finance"`; Radzen tabs: Balances, Payments, Apply Annual Fees; `role="tablist"` semantics per NFR-010; deep-link via `?tab=`)
- [ ] T121 [P] [US6] Create `src/StageFright.UI/Pages/Finance/MemberBalanceList.razor` (table: member, total outstanding; expand to show annual + attendance fee breakdown; click member → navigate to PaymentForm)
- [ ] T122 [P] [US6] Create `src/StageFright.UI/Pages/Finance/PaymentForm.razor` and `PaymentForm.razor.cs` (`@page "/finance/payment/{memberId:guid}"`; date, amount, method (default Cash), type, category dropdown, notes; amount > 0 required; all non-Notes fields hidden/disabled on edit mode)
- [ ] T123 [P] [US6] Create `src/StageFright.UI/Shared/ReactivationForgivenessDialog.razor` (Radzen Dialog; fee checkboxes by year; prior years pre-checked; current year unchecked; confirms → calls ReactivationForgivenessService.ApplyForgivenessAsync)
- [ ] T124 [P] [US6] Create bUnit tests in `tests/StageFright.UI.Tests/Pages/Finance/` for PaymentForm (method defaults Cash, immutable fields disabled post-save) and ReactivationForgivenessDialog (checkbox state, year grouping)
- [ ] T125 [US6] Create integration acceptance tests `tests/StageFright.Integration.Tests/Scenarios/V5_PaymentsTests.cs` (FIFO allocation, partial/overpayment, Notes-only edit, immutability error) and `V12_ReactivationForgivenessTests.cs` (prior-year precheck, current-year override, GL write-offs, Fee records untouched)

**Checkpoint**: US6 complete — payment recording, FIFO, balance tracking, reactivation forgiveness independently functional.

---

## Phase 10: User Story 6a — Accounting Reports and Financial Statements (Priority: P1)

**Goal**: Income Statement, Trial Balance (balance verified), Account Register, Member Account Summary. PDF print, CSV export. GL-sourced data.

**Independent Test**: Create transactions across multiple categories → generate all 4 reports → verify accuracy → force Trial Balance imbalance → error generated → print → export.

### Tests for User Story 6a

- [ ] T126 [P] [US6a] Write `IncomeStatementReportProviderTests.cs` in `tests/StageFright.Reports.Tests/` (income section with subtotal; expense section with subtotal; net income/loss; date range filter; empty sections handled) — FAIL first
- [ ] T127 [P] [US6a] Write `TrialBalanceReportProviderTests.cs` (Assets/Income/Expenses sections; Debit/Credit columns; Σdebits = Σcredits pass → report generated; forced imbalance → GLBalanceException with exact FR-034 message "GL Balance Verification Failed: Total Debits ($X.XX) ≠ Total Credits ($Y.YY)…")
- [ ] T128 [P] [US6a] Write `AccountRegisterReportProviderTests.cs` (chronological order; running balance correct after each row; date range filter)
- [ ] T129 [P] [US6a] Write `MemberAccountSummaryReportProviderTests.cs` (opening balance; period transactions; closing balance; aging by DueDate: current/30/60/90+; archived members included)
- [ ] T130 [P] [US6a] Write `PdfReportRendererTests.cs` and `CsvReportExporterTests.cs` (PDF non-empty byte[]; CSV first row = headers; commas/quotes in values RFC 4180 escaped)

### Implementation for User Story 6a

- [ ] T131 [P] [US6a] Create `src/StageFright.Core/Modules/Reports/IncomeStatementReportProvider.cs` (ReportId="income-statement", ModuleName="Finance"; date-range filter defaulting to current calendar year; sections: Income → rows per income category, subtotal; Expenses → rows per expense category, subtotal; GrandTotal=NetIncome)
- [ ] T132 [P] [US6a] Create `src/StageFright.Core/Modules/Reports/TrialBalanceReportProvider.cs` (ReportId="trial-balance"; sections: Assets (Cash GL#0100, MemberReceivable GL#0101), Income categories, Expense categories; each row: AccountName | DebitAmount | CreditAmount; GrandTotal row; calls IGLRepository.GetBalanceTotalsAsync; if |TotalDebits − TotalCredits| > 0.01 → throws GLBalanceException with exact FR-034 message)
- [ ] T133 [P] [US6a] Create `src/StageFright.Core/Modules/Reports/AccountRegisterReportProvider.cs` (ReportId="account-register"; date/description/category/debit/credit/running-balance columns; category filter; chronological; running balance recomputed per row)
- [ ] T134 [P] [US6a] Create `src/StageFright.Core/Modules/Reports/MemberAccountSummaryReportProvider.cs` (ReportId="member-account-summary"; includes archived members (IgnoreQueryFilters); per member: opening balance at start of period, period transactions, closing balance, fee aging by Fee.DueDate as-of today)
- [ ] T135 [P] [US6a] Create `src/StageFright.Reports/Rendering/PdfReportRenderer.cs` (implements IPdfReportRenderer; uses QuestPDF Document to render title, subtitle/date range, generation date, all column headers, section headings, rows, subtotals, grand totals; professional formatting per FR-037)
- [ ] T136 [P] [US6a] Create `src/StageFright.Reports/Rendering/CsvReportExporter.cs` (implements ICsvReportExporter; uses CsvHelper; headers as first row; all data rows; RFC 4180 quote-escaping for commas and quotes in field values; FR-041)
- [ ] T137 [US6a] Create integration acceptance test `tests/StageFright.Integration.Tests/Scenarios/V6_AccountingReportsTests.cs` (generate all 4; Trial Balance totals match; forced imbalance error; Account Register running balance; Member Account Summary aging; CSV escaping; PDF non-empty)

**Checkpoint**: US6a complete — all 4 accounting reports with print/export independently functional.

---

## Phase 11: User Story 11 — Reports Menu and Shared Report Viewer Infrastructure (Priority: P1)

**Goal**: Root Reports menu item aggregating all module reports; ReportViewer.razor (synchronous, "Generating report…" modal, cancel after 5s, print/export); graceful failure isolation.

**Independent Test**: Reports menu shows Members section (Member List, Committee Report) + Finance section (4 reports); each viewable, printable, exportable; provider failure shows friendly error without blocking others.

### Tests for User Story 11

- [ ] T138 [P] [US11] Write `ReportProviderRegistryTests.cs` in `tests/StageFright.Core.Tests/Modules/Reports/` (Members section before Finance before plugins; duplicate ReportId skipped + logged; failing provider skipped) — FAIL first
- [ ] T139 [P] [US11] Write bUnit test `tests/StageFright.UI.Tests/Shared/ReportViewerTests.cs` (renders loading modal on generation; Cancel button appears after 5s stub; Print button triggers IPdfReportRenderer; Export triggers ICsvReportExporter; error state shown on GenerateAsync throw)

### Implementation for User Story 11

- [ ] T140 [P] [US11] Create `src/StageFright.Reports/Registry/ReportProviderRegistry.cs` (implements IReportProviderRegistry; GetMenuSections: Members first, Finance second, then plugins alphabetically; GetProvider by ReportId; GenerateAsync failures caught, logged, error result returned — FR-049)
- [ ] T141 [P] [US11] Create `src/StageFright.Core/Modules/Reports/ReportMenuItemProvider.cs` (ModuleName="Reports", DisplayOrder=5; builds submenu hierarchy from IReportProviderRegistry.GetMenuSections())
- [ ] T142 [P] [US11] Create `src/StageFright.UI/Pages/Reports/ReportsPage.razor` and `ReportsPage.razor.cs` (`@page "/reports/{reportId?}"`; renders report selector sidebar + ReportViewer for selected report)
- [ ] T143 [P] [US11] Create `src/StageFright.UI/Shared/ReportViewer.razor` and `ReportViewer.razor.cs` (on report selected: show "Generating report..." Radzen modal with spinner always; call IReportProvider.GenerateAsync; display ReportData; Print button → IPdfReportRenderer.Render → OS print dialog; Export CSV button → ICsvReportExporter.Export → file-save dialog; Cancel after 5s CancellationToken; no caching between actions; user-friendly error on failure — FR-049)
- [ ] T144 [P] [US11] Create `src/StageFright.Core/Modules/Reports/MemberListReportProvider.cs` (ReportId="member-list", ModuleName="Members"; columns: Name, Address, Phone, Email, JoinDate, Age, Status; memberStatus filter: Active (default)/Inactive/Archived/All — FR-051) and `CommitteeReportProvider.cs` (ReportId="committee-report"; Member | Year | Position; year DESC; filter: Active Only (default)/Archived Only/All — FR-052)
- [ ] T145 [US11] Create integration acceptance test `tests/StageFright.Integration.Tests/Scenarios/V11_ReportsMenuTests.cs` (Reports menu structure; Member List filter persistence; Committee Report filter; provider failure graceful; TestPlugin report in alphabetical section)

**Checkpoint**: US11 complete — reports menu and shared viewer infrastructure independently functional.

---

## Phase 12: User Story 5 — Event/Performance Scheduling and Participation Tracking (Priority: P2)

**Goal**: Event types configured in Settings (incl. AGM). Schedule events, record participation. No fees from events. StoredParticipationRate frozen.

**Independent Test**: Create event types → schedule events → record participation → verify no fees created → verify StoredParticipationRate.

### Tests for User Story 5

- [ ] T146 [P] [US5] Write `EventServiceTests.cs` in `tests/StageFright.Core.Tests/Modules/Events/` (schedule event, record participation, StoredParticipationRate formula, AGM event no fees, agmExistsInYear) — FAIL first
- [ ] T147 [P] [US5] Write `EventTypeServiceTests.cs` (default event types seeded including AGM; archive blocked if referenced by non-deleted Event; IsSystemDefault types cannot be archived)

### Implementation for User Story 5

- [ ] T148 [P] [US5] Create `src/StageFright.Core/Modules/Events/EventTypeService.cs` (GetAllAsync; CreateAsync; ArchiveAsync: blocks if referenced; seeded defaults via SetupService: Performance, Eisteddfod, Fund raiser, Promotional, Annual General Meeting with IsSystemDefault=true)
- [ ] T149 [P] [US5] Create `src/StageFright.Core/Modules/Events/EventService.cs` (ScheduleAsync; RecordParticipationAsync: computes active-as-of-date count, stores StoredParticipationRate, creates ParticipationRecord batch — **no Fee or GL records created for events** per FR-006; AgmExistsInYearAsync delegates to IEventRepository)
- [ ] T150 [P] [US5] Create `src/StageFright.Core/Modules/Dashboard/EventsDashboardTileProvider.cs` (TileId="events", DisplayOrder=30; most recent past event date + StoredParticipationRate)
- [ ] T151 [P] [US5] Create `src/StageFright.Core/Modules/Events/EventsMenuItemProvider.cs` (DisplayOrder=3; "/events")
- [ ] T152 [P] [US5] Create `src/StageFright.UI/Pages/Settings/EventTypesSettingsTabProvider.cs` (TabKey="event-types", DisplayOrder=20) and `EventTypesTab.razor` / `EventTypesTab.razor.cs` (list + create event types; system defaults shown read-only)
- [ ] T153 [P] [US5] Create `src/StageFright.UI/Pages/Events/EventList.razor`, `EventForm.razor`, `ParticipationGrid.razor` (similar to Rehearsals grid but no "Paid" checkbox column; `@page` directives as appropriate)
- [ ] T154 [P] [US5] Create bUnit tests `tests/StageFright.UI.Tests/Pages/Events/` for EventList, EventForm, ParticipationGrid (no fee columns visible)
- [ ] T155 [US5] Create integration acceptance test `tests/StageFright.Integration.Tests/Scenarios/V5_EventsParticipationTests.cs` (event types created; AGM event → no fees; participation recorded → StoredParticipationRate set; Events tile shows rate)

**Checkpoint**: US5 complete — event scheduling and participation tracking independently functional.

---

## Phase 13: User Story 9 — Backup and Restore (Priority: P2)

**Goal**: Protobuf backup of all 10 entity types (incl. soft-deleted). Strict import: version check, all-10-types completeness, pre-import checkpoint, atomic PK-upsert.

**Independent Test**: Create data → backup → clear DB → restore → data intact; missing-entity-type backup → rejected with exact error.

### Tests for User Story 9

- [ ] T156 [P] [US9] Write `BackupServiceTests.cs` in `tests/StageFright.Core.Tests/Modules/Settings/` (export includes soft-deleted records; export EntityCounts match; import version mismatch → ImportException; missing Categories list → ImportException with exact "Import file incomplete: missing Categories" message; valid complete restore → all data present) — FAIL first
- [ ] T157 [P] [US9] Write import atomicity integration test `tests/StageFright.Data.Tests/BackupImportTests.cs` (import failure mid-upsert → rollback, original data unchanged; pre-import checkpoint file created before write)

### Implementation for User Story 9

- [ ] T158 [P] [US9] Create backup DTOs in `src/StageFright.Core/Modules/Settings/Backup/`: `BackupEnvelope.cs` (protobuf-net `[ProtoContract]`; 10 entity collections + EntityCounts; SchemaVersion, GeneratedAt, ApplicationVersion; field numbers append-only), plus 10 DTO classes mirroring entities (MemberBackupDto.cs through AuditTrailBackupDto.cs) — one file per type
- [ ] T159 [US9] Create `src/StageFright.Core/Modules/Settings/BackupService.cs` (ExportAsync: reads all 10 entity types via IgnoreQueryFilters; serializes with protobuf-net to `.sfbak` file; logs entity counts; ImportAsync: deserialize → major-version check → completeness check of all 10 collections → pre-import checkpoint → IUnitOfWork PK-upsert → post-commit audit + log)
- [ ] T160 [P] [US9] Create `src/StageFright.UI/Pages/Settings/BackupSettingsTabProvider.cs` (TabKey="backup", DisplayOrder=30) and `BackupRestoreTab.razor` / `BackupRestoreTab.razor.cs` (Backup button → file-save dialog; Restore button → file-open dialog → confirmation dialog showing entity counts + checkpoint path → ImportAsync; error display for ImportException)
- [ ] T161 [US9] Create integration acceptance test `tests/StageFright.Integration.Tests/Scenarios/V9_BackupRestoreTests.cs` (backup → clear → restore → data intact; missing-entity-type file → rejected with exact error; corrupt file → ImportException "Backup file is corrupted")

**Checkpoint**: US9 complete — backup/restore independently functional.

---

## Phase 14: User Story 10 — Dark/Light Theme Support (Priority: P2)

**Goal**: Theme toggle; Bootstrap 5.3 `data-bs-theme` on root; WCAG AA in both themes; preference persisted in Settings.

**Independent Test**: Toggle theme → UI switches → restart → preference restored; automated contrast tests pass for both themes.

### Tests for User Story 10

- [ ] T162 [P] [US10] Write `ThemeProviderTests.cs` bUnit in `tests/StageFright.UI.Tests/Layout/` (toggle changes data-bs-theme attribute; Light default; preference persisted via SettingsService) — FAIL first
- [ ] T163 [P] [US10] Write WCAG AA contrast tests in `tests/StageFright.UI.Tests/Accessibility/WcagContrastTests.cs` (HSL(120,35%,70%) green on white/dark backgrounds; HSL(0,35%,70%) red; committee badge HSL(120,40%,70%) light / HSL(120,35%,55%) dark; all pass WCAG AA 4.5:1 minimum)

### Implementation for User Story 10

- [ ] T164 [P] [US10] Fully implement `src/StageFright.UI/Layout/ThemeProvider.razor` (cascading component; applies `data-bs-theme="light"|"dark"` on root `<html>` element; reads initial theme from Settings on mount; exposes ToggleAsync updating Settings.Theme)
- [ ] T165 [P] [US10] Wire theme toggle button in `src/StageFright.UI/Layout/ShellLayout.razor` (sun/moon icon in brand strip; calls ThemeProvider.ToggleAsync; persists via SettingsService.SaveAsync)
- [ ] T166 [P] [US10] Create `src/StageFright.UI/Pages/Settings/GeneralSettingsTabProvider.cs` (TabKey="general", DisplayOrder=0) and `GeneralSettingsTab.razor` / `GeneralSettingsTab.razor.cs` (all 7 Settings fields per FR-018: OrgName, AnnualFee, AttendanceFee, MembershipRenewalMonth, CommitteeRenewalMonth, MaxAgeRangeYears, MinimumMemberAge; theme toggle; "Reset Committee for New Year" button → ReactivationForgivenessDialog/CommitteeAnnualResetService; AGM banner if applicable)
- [ ] T167 [US10] Create integration acceptance test `tests/StageFright.Integration.Tests/Scenarios/V10_ThemeTests.cs` (toggle → attribute changes; persist → restart → preference restored; WCAG confirmed via automated assertions)

**Checkpoint**: US10 complete — theme toggle with persistence and WCAG AA independently functional.

---

## Phase 15: Polish & Cross-Cutting Concerns

**Purpose**: Committee annual reset, AGM banner, startup sequence hardening, full test plugin, CI gate, and code quality tooling.

- [ ] T168 [P] Create `src/StageFright.Core/Modules/Members/CommitteeAnnualResetService.cs` (ResetAsync: IUnitOfWork; ICommitteeMembershipRepository.SoftDeleteCurrentYearAsync for current year; Settings.LastCommitteeResetYear = currentYear; audit Action=CommitteeReset; CheckAgmBannerAsync: AgmExistsInYearAsync && LastCommitteeResetYear < currentYear && AGM.Date < today − 7 days → return banner message)
- [ ] T169 [P] Wire AGM banner into `src/StageFright.UI/Pages/Settings/GeneralSettingsTab.razor.cs` (OnInitializedAsync checks CommitteeAnnualResetService.CheckAgmBannerAsync; if non-null renders Bootstrap alert with exact FR-031 text and "Click to reset" link)
- [ ] T170 [P] Implement startup audit log purge in `src/StageFright.App/MauiProgram.cs` startup sequence: call `IAuditTrailService.PurgeOlderThanAsync(now − 12 months)`; catch all exceptions → log structured error; startup continues (FR-022)
- [ ] T171 [P] Implement `Plugins/` directory auto-creation in `src/StageFright.App/MauiProgram.cs` startup (FR-021): `Directory.CreateDirectory(pluginsPath)` with catch IOException/UnauthorizedAccessException → structured log → skip
- [ ] T172 [P] Implement graceful corrupted-database handling in `src/StageFright.App/MauiProgram.cs`: catch `SqliteException` / `DbUpdateException` on startup migration → show user-friendly Radzen Dialog with recovery options (open file location, create new database)
- [ ] T173 [P] Complete `tests/StageFright.TestPlugin/` with `TestTileProvider.cs` (IDashboardTileProvider, DisplayOrder=100), `TestReportProvider.cs` (IReportProvider, ModuleName="TestPlugin"), `TestDataAccessProvider.cs` (IDataAccessProvider; defines `TestPluginEntity.cs` + minimal DbContext; proves plugin migration pattern) — used by V8 + V11 acceptance tests
- [ ] T174 [P] Create `tests/StageFright.Core.Tests/Modules/Members/CommitteeAnnualResetServiceTests.cs` (reset clears current-year only, preserves history, updates LastCommitteeResetYear, audit entry; AGM banner condition: agm exists + last-reset < current year + agm > 7 days ago)
- [ ] T175 [P] Create `tests/StageFright.Integration.Tests/Scenarios/V13_CommitteeResetAgmBannerTests.cs` (AGM event recorded → banner shows on Settings; click reset → current-year cleared → history preserved → banner disappears)
- [ ] T176 [P] Create `tests/StageFright.Integration.Tests/Scenarios/StartupSequenceTests.cs` (plugin discovery success + failure isolation; audit purge runs; Plugins/ auto-created; corrupted DB error dialog)
- [ ] T177 [P] Run all quickstart.md validation scenarios V1–V13 as automated acceptance tests; add any missing coverage for scenarios not yet covered by prior phase acceptance tests in `tests/StageFright.Integration.Tests/Scenarios/`
- [ ] T178 [P] Configure `.editorconfig` + Roslyn analyzers (CA1515 / custom) for one-class-per-file enforcement in all `src/` projects; add to CI build as warnings-as-errors for the one-class rule
- [ ] T179 [P] Configure XML documentation generation in all `src/*.csproj` files (`<GenerateDocumentationFile>true</GenerateDocumentationFile>`, `<NoWarn>` for non-public members); missing public API XML docs treated as build warning
- [ ] T180 Configure CI merge gate in `StageFrightCommunity.sln` / GitHub Actions workflow: `dotnet test` all 5 test projects (Core, Data, Reports, UI, Integration) must be green; build must be warning-free on one-class rule; NFR-005 compliance

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 — **BLOCKS all user stories**
- **User Stories (Phases 3–14)**: All depend on Phase 2 completion
  - US1 (Phase 3) must complete before any UI involving Settings can function
  - US7 (Phase 7) must complete before US6, US6a, US9 (categories required for GL)
  - US6 (Phase 9) must complete before US6a (payments needed for report data)
  - US6a (Phase 10) and US11 (Phase 11) can proceed in parallel after US6
  - US3 (Phase 5) depends on US2 (Phase 4) — members required for attendance
  - US4 (Phase 6) depends on US2 (Phase 4) + US7 (Phase 7)
  - US5, US9, US10 (Phases 12–14) are independent P2 stories; depend only on Phase 2
- **Polish (Phase 15)**: Depends on all user stories being implemented

### User Story Dependencies

| Story | Depends On | Notes |
|-------|-----------|-------|
| US1 (Phase 3) | Phase 2 | Independent entry point |
| US2 (Phase 4) | US1 | Members module needs Settings singleton |
| US3 (Phase 5) | US2 | Attendance requires members |
| US4 (Phase 6) | US2, US7 | Fees need categories for GL |
| US7 (Phase 7) | US1 | Categories go in Settings |
| US8 (Phase 8) | US2, US3, US4, US7 | Dashboard tiles aggregate all modules |
| US6 (Phase 9) | US2, US7 | Payments need members + categories |
| US6a (Phase 10) | US6, US7 | Reports need transactions |
| US11 (Phase 11) | US6a | Report viewer wraps report providers |
| US5 (Phase 12) | US2 | Events need members |
| US9 (Phase 13) | All prior | Backup covers all entity types |
| US10 (Phase 14) | US1 | Theme persisted in Settings |

### Parallel Opportunities (within each phase)

- Phase 2: All entity tasks (T009–T021), all enum tasks (T022), all exception tasks (T023–T024), all contract tasks (T025–T037), all EF configurations (T039), all repository implementations (T041–T047) are fully parallelizable within their groups
- All test tasks marked [P] within each phase can run in parallel
- All `[P]` tasks within a phase have no intra-phase file conflicts

---

## Parallel Example: Phase 2 Entities

```
# All entities can be authored simultaneously (different files):
T009: Member.cs
T010: CommitteeMembership.cs
T011: Rehearsal.cs
T012: AttendanceRecord.cs
T013: Event.cs
T014: EventType.cs
T015: ParticipationRecord.cs
T016: Fee.cs
T017: Payment.cs
T018: Transaction.cs
T019: Category.cs
T020: Settings.cs
T021: AuditTrailEntry.cs
```

---

## Implementation Strategy

### MVP First (US1 + US2 + US7 — Minimum Walking Skeleton)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational
3. Complete Phase 3 (US1): First-Run Setup → launch app, see wizard
4. Complete Phase 4 (US2): Member Registration → register members
5. Complete Phase 7 (US7): Category Management → define GL categories
6. **STOP and VALIDATE**: Members created, categories defined, Settings persisted — organizational data ready
7. Deploy/demo walking skeleton

### Incremental Delivery (P1 stories)

1. Setup + Foundational → skeleton running
2. US1 + US2 + US7 → organizational data foundation
3. US3 (Rehearsals) + US4 (Fees) → core workflow
4. US8 (Dashboard) → visibility into data
5. US6 (Finance) + US6a (Reports) + US11 (Report Infrastructure) → financial compliance
6. **MVP P1 COMPLETE**: All P1 stories done → release candidate

### P2 Completion

7. US5 (Events) + US9 (Backup) + US10 (Themes) in parallel
8. Phase 15 (Polish) → CI gate, analyzer config, full smoke-test suite
9. **FULL MVP**: All user stories verified against quickstart.md V1–V13

---

## Notes

- All `[P]` tasks write to different files — no conflicts within a phase
- `[Story]` label maps each task to a specific user story for traceability and independent merge
- Financial entities (Fee, Payment, Transaction) have **no soft-delete fields** — verified by T054
- Every user story phase has: tests (written first to fail), implementation, and an end-to-end acceptance test
- Constitution §11.0 non-negotiable: work is **not complete** until code-path coverage evidence exists
- One class per file enforced via T178 analyzer — PR merge blocked on violation
- No custom JavaScript anywhere — Radzen Blazor components + QuestPDF/CsvHelper handle all rich interactions
