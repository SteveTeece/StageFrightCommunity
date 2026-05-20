# StageFright Community MVP — Implementation Plan

**Template-Version**: 2.1.0  
**Spec-Reference**: `specs/001-initial-mvp/spec.md`  
**Constitution-Version**: 2.2.1  
**Last-Updated**: 2026-05-15  
**Feature-Branch**: `001-initial-mvp`  
**Status**: Ready for Task Generation

---

## Executive Summary

This document outlines the complete implementation strategy for the StageFright Community MVP, a desktop application designed to streamline operations for small performing arts groups. The plan encompasses architecture decisions, implementation phases, data modeling, API contracts, technical stack selection, and risk mitigation strategies.

The MVP establishes a clean, modular foundation with built-in extensibility through a plugin architecture while delivering immediate value through core operational features: member management, rehearsal/event scheduling, financial tracking with double-entry accounting, and comprehensive reporting.

---

## 1. Architecture Design

### 1.1 Architectural Principles

The architecture follows **SOLID principles** and **clean code standards** per Constitution §3:

- **Single Responsibility**: Each module owns distinct domain concerns
- **Open/Closed**: System extensible via plugins without core modification
- **Liskov Substitution**: Provider contracts enable interchangeable implementations
- **Interface Segregation**: Focused, minimal contracts (IDashboardTileProvider, IReportProvider, etc.)
- **Dependency Inversion**: High-level modules depend on abstractions, not concrete implementations

### 1.2 Layered Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    UI Layer (Blazor Components)             │
│         (Hybrid MAUI BlazorWebView - Single View)           │
├─────────────────────────────────────────────────────────────┤
│         Application Layer (Module Controllers)               │
│  (Dashboard, Members, Rehearsals, Events, Finance, Settings)│
├─────────────────────────────────────────────────────────────┤
│         Domain Layer (Business Logic & Entities)            │
│  (Member, Rehearsal, Event, Payment, Fee, Category, etc.)  │
├─────────────────────────────────────────────────────────────┤
│     Infrastructure Layer (Data Access, External Services)   │
│  (Entity Framework Core, SQLite, Audit Trail, Logging)      │
├─────────────────────────────────────────────────────────────┤
│     Plugin Extension Points (Provider Contracts)            │
│  (IDashboardTileProvider, IReportProvider, IDataAccessProvider)|
└─────────────────────────────────────────────────────────────┘
```

### 1.3 Blazor Hybrid MAUI Architecture

- **Framework**: MAUI (Multi-platform App UI) with BlazorWebView for Windows and macOS desktop only
- **Target Platforms**: Windows 10.0.19041+ and macOS 10.15+ via Mac Catalyst (no mobile, iOS, Android, or Linux)
- **Single-View Pattern**: MAUI shell contains single BlazorWebView; all UI rendered through Blazor components
- **Navigation**: All route transitions via `NavigationManager.NavigateTo(...)` (NavigateTo-only enforcement per NFR-001)
- **Styling**: Bootstrap 5 with custom CSS for pastel/muted color palette (HSL lightness 60–80%, saturation <50%)
- **Desktop Shell**: 
  - Dark brand strip (top) with purple StageFright wordmark
  - White navigation bar with organization title (left) and module links (right)
  - Two-column dashboard card layout (default)
  - Tabbed interfaces for multi-function modules (using Blazor tab controls with WCAG semantics)

**Justification**: MAUI + Blazor provides native Windows and macOS desktop support with modern web UI capabilities. Single BlazorWebView simplifies navigation, state management, and deployment. BlazorWebView integrates with native desktop APIs while leveraging web technologies for UI. Desktop-only targeting eliminates mobile complexity and reduces platform-specific dependencies.

### 1.4 Centralized Data Access Layer (DAL)

**Design**: All MVP module data access consolidated in a single, reusable, extensible DAL:

- **Pattern**: Repository pattern with Entity Framework Core (EF Core)
- **Database**: SQLite (file-based, no server required)
- **Entities**: Member, Rehearsal, Event, Fee, Payment, Transaction, Category, Settings, CommitteeMembership, AuditTrail
- **Repositories**: One repository contract per entity type, all in base DAL
- **Migration Strategy**: Code-first migrations for schema versioning and evolution

**Example Repository Contracts**:
```csharp
public interface IMemberRepository
{
    Task<Member> GetByIdAsync(Guid id);
    Task<IEnumerable<Member>> GetActiveMembersAsync();
    Task<IEnumerable<Member>> GetHistoricalActiveMembers(DateTime asOfDate);
    Task CreateAsync(Member member);
    Task UpdateAsync(Member member);
    Task SoftDeleteAsync(Guid id, string deletedBy);
    Task RestoreAsync(Guid id);
}

public interface ITransactionRepository
{
    Task<IEnumerable<Transaction>> GetByMemberAsync(Guid memberId, DateTime from, DateTime to);
    Task<decimal> GetMemberBalanceAsync(Guid memberId);
    Task CreatePairAsync(Transaction debit, Transaction credit);
    Task<bool> ValidateGLBalance();
}
```

**Extensibility**: Plugins can register custom entities and repositories via `IDataAccessProvider` contract; base DAL auto-discovers and integrates plugin DbContext extensions with code-first migrations.

### 1.5 Plugin Architecture

**Discovery Mechanism**:
- Application scans `Plugins` directory (auto-created on startup if missing)
- Each plugin is an assembly (.dll) containing provider implementations
- Plugins registered via assembly reflection and dependency injection

**Plugin Extension Points**:

1. **Dashboard Tiles** (`IDashboardTileProvider`):
   - Plugins contribute tiles to dashboard without core modification
   - Tiles load progressively and degrade gracefully on failure

2. **Settings Tabs** (`ISettingsTabProvider`):
   - Plugins contribute settings tabs to Settings module
   - Core tabs: 0–99; plugin tabs: 100+

3. **Data Access** (`IDataAccessProvider`):
   - Plugins define custom entities and repository implementations
   - Base DAL auto-discovers and runs migrations

4. **Reports** (`IReportProvider`):
   - Plugins contribute custom reports to Reports menu
   - Each plugin specifies module name, report ID, display order, and data generation method
   - Common report infrastructure handles rendering, printing, CSV export

**Error Handling**: Failed plugin loads logged; application continues with available plugins.

### 1.6 Separation of Concerns

**Module Responsibilities**:

| Module | Responsibilities |
|--------|------------------|
| **Dashboard** | Tile aggregation, progressive loading, graceful degradation |
| **Members** | Member CRUD, lifecycle management, committee tracking, member reports |
| **Rehearsals** | Scheduling, attendance recording, attendance fee accrual |
| **Events** | Scheduling, participation tracking, event type management |
| **Finance** | Payments, GL transactions, category management, financial reports, balance tracking |
| **Settings** | Organization configuration, category definitions, theme persistence, backup/restore |
| **Reports Infrastructure** | Report viewer, PDF printing, CSV export (shared across all modules) |
| **Plugin Architecture** | Assembly discovery, provider registration, dependency injection |
| **Data Access Layer** | Entity Framework context, repositories, migrations, queries |
| **Audit Trail** | Logging all data modifications, 12-month retention, startup purge |

### 1.7 Code Organization Standards

**One Class Per File**: The codebase enforces **one public class/interface per file**. This principle:
- **Improves maintainability**: Each file has a single, clear purpose
- **Enables faster navigation**: Developers find classes through filenames rather than searching within large files
- **Reduces merge conflicts**: Multiple developers working on different classes avoid file contention
- **Supports IDE tooling**: VS Code and Visual Studio optimize refactoring, navigation, and search when files follow this pattern
- **Exception**: Interfaces without implementations may be grouped (e.g., multiple small related interfaces), but once implementation is added, split into separate files

**File Naming Convention**: Class names match filenames exactly.
- Example: `public class MemberRepository` lives in `MemberRepository.cs`
- Data model classes: `Member.cs`, `Rehearsal.cs`, `Fee.cs`, etc.
- Interfaces: `IMemberRepository.cs`, `IRepository.cs`, etc.
- Support/DTO classes: `ReportFilter.cs`, `TileData.cs`, etc.

**Namespace Organization**:
- Entities: `StageFright.Core.Entities`
- Services: `StageFright.Core.Services`
- Repositories: `StageFright.Data.Repositories`
- Plugin Contracts: `StageFright.Plugins.Contracts`
- Reports/Models: `StageFright.Reports.Models`
- UI Components: `StageFright.UI.Pages`, `StageFright.UI.Shared`, etc.

**Impact on Code Review**: Code reviews focus on logic, design, and testing rather than file organization, reducing reviewer cognitive load.

### 1.8 XML Documentation (Mandatory Code Comments)

**Comprehensive XML Documentation on All Public APIs**: The codebase enforces **XML documentation comments (triple-slash `///`) on all public types and members**. This enables IntelliSense support, generates external documentation, and provides critical context during code review and maintenance.

**MANDATORY XML Documentation (Required)**:
- All public classes and structs with summary of purpose
- All public interfaces with contract description
- All public enums and enum values
- All public methods and constructors with parameter, return value, and exception documentation
- All public properties and indexers with get/set semantics described
- All public delegates and events
- All public constants

**Example - Annotated Method**:
```csharp
/// <summary>
/// Retrieves all unpaid fees for a specific member, ordered by fee date (oldest first).
/// </summary>
/// <param name="memberId">The member's unique identifier.</param>
/// <returns>Enumerable of unpaid Fee records, or empty collection if none found.</returns>
/// <exception cref="ArgumentNullException">Thrown when memberId is null or empty.</exception>
/// <exception cref="DataAccessException">Thrown when database query fails.</exception>
public async Task<IEnumerable<Fee>> GetUnpaidAsync(Guid memberId)
{
    // Implementation
}
```

**OPTIONAL XML Documentation (Recommended but Not Enforced)**:
- Internal/private methods (recommended for complex logic but not required)
- Test code (test class public methods should have summary; test implementation details do not require XML comments)

**Code Review Verification**: All pull requests must include XML documentation on new public APIs. Missing documentation on public types/methods must be addressed before code review approval.

**Documentation Generation**: XML comments can be extracted and converted to external documentation (Markdown, HTML, etc.) using tools like `docfx`. Documentation tools are configured in the project build process.

---

## 2. Implementation Phases

### Phase 0: Project Setup & Infrastructure (1–2 weeks)

**Deliverables**:
- MAUI project structure with Blazor support
- Entity Framework Core + SQLite setup
- Base DAL scaffold (repository interfaces)
- DI container configuration
- Logging infrastructure (structured logging)
- Custom exception hierarchy
- Database migration framework
- Unit test infrastructure

**Key Tasks**:
1. Create MAUI project with BlazorWebView
2. Install EF Core, SQLite, and dependency packages
3. Define DbContext and base repository class
4. Scaffold migration tooling
5. Configure logging (Serilog or similar)
6. Create custom exception types (ValidationException, DataAccessException, etc.)
7. Build DI registration helpers for modules and plugins
8. Set up CI/CD pipeline and test harness

**Definition of Done**:
- All packages installed and configured
- Sample DbContext and repository compile
- First migration runs successfully
- Unit test harness validates core infrastructure

---

### Phase 1: Core Modules & Data Model (3–4 weeks)

**Deliverables**:
- Member module (CRUD, lifecycle)
- Rehearsal module (scheduling, attendance)
- Event module (scheduling, participation)
- Settings module (organization config, categories, event types)
- First-run setup wizard
- Dashboard with core tiles (Members, Rehearsals, Events, Finance placeholders)
- Database schema (complete with soft-delete fields, effective dates, GL structure)
- Entity validation and business logic

**Key Tasks**:

**Subphase 1a: Data Model & Schema** (1 week)
1. Define all entity models with relationships
2. Implement soft-delete pattern (IsDeleted, DeletedAt, DeletedBy fields)
3. Add effective date fields (ActivateDate, InactivateDate) for historical queries
4. Create GL account structure (Asset, Revenue, Expense account mapping)
5. Write schema migration (v1.0.0)
6. Implement query filters for soft-deleted records
7. Add database constraints for data integrity

**Subphase 1b: Repositories & Queries** (1 week)
1. Implement IMemberRepository with active/inactive/archived queries
2. Implement IRehearsalRepository with attendance queries
3. Implement IEventRepository with participation queries
4. Implement ICategoryRepository with archival validation
5. Implement ISettingsRepository (singleton pattern)
6. Write integration tests for all CRUD operations
7. Verify soft-delete behavior and historical queries

**Subphase 1c: UI & Modules** (2 weeks)
1. Build Settings module (tabs: General, Categories, Event Types, Backup, Restore)
2. Implement first-run setup wizard
3. Build Members module (list, add, edit, filter by status, committee tracking)
4. Build Rehearsals module with **batch attendance interface per rehearsal**: Member Name | [Attended ☐] | [Paid ☐] checkboxes for all active members, with Save/OK for atomic record creation; override checkbox for marking fees as unpaid at creation time
5. Build Events module (schedule, record participation) — Events do NOT create fees; AGM event type with no fee impact
6. Implement dashboard shell (brand strip, nav bar, card layout)
7. Add theme toggle and persistence
8. Implement placeholder tiles

**Definition of Done**:
- All entities created and migrated to database
- Members, Rehearsals, Events modules fully functional
- Settings module allows configuration
- Dashboard displays with placeholder tiles
- First-run setup wizard completes successfully
- All core workflows have acceptance tests passing

---

### Phase 2: Financial & Reporting Infrastructure (3–4 weeks)

**Deliverables**:
- Finance module (payments, GL transactions)
- Double-entry accounting implementation
- Financial reports (Income Statement, Trial Balance, Account Register, Member Account Summary)
- Common report viewing infrastructure
- Reports menu aggregation
- Backup/restore with protobuf serialization
- Annual fee application batch processing

**Key Tasks**:

**Subphase 2a: GL & Financial Data Access** (1 week)
1. Implement ITransactionRepository with GL pair creation and validation
2. Implement IFeeRepository with immutability enforcement
3. Implement IPaymentRepository with FIFO allocation support
4. Add GL balance validation (debits = credits)
5. Implement payment GL transaction pair creation
6. Add fee accrual on attendance recording
7. Write integration tests for GL integrity

**Subphase 2b: Finance Module UI** (1 week)
1. Build Payment recording form (method, type, category, notes)
2. Build Member balance view (annual + attendance fee breakdown)
3. Build Category management interface
4. Build Annual Fee application confirmation dialog
5. Implement Finance tile on dashboard (outstanding balance display)
6. Add validation for payment amounts and categories
7. Write acceptance tests for payment recording and balance updates

**Subphase 2c: Reports Infrastructure** (1 week)
1. Design common report viewer component (display, print, export)
2. Implement report data abstraction (rows/columns with headers)
3. Create IReportProvider contract
4. Build report menu aggregation and auto-discovery
5. Implement PDF printing (via Blazor print API or IronPDF)
6. Implement CSV export
7. Add report loading indicators and error handling

**Subphase 2d: Financial Reports** (1 week)
1. Implement Income Statement report provider
2. Implement Trial Balance report provider with GL balance verification
3. Implement Account Register report provider with running balance
4. Implement Member Account Summary report provider with aging buckets
5. Register Members module reports (Member List, Committee Report)
6. Add date range filtering and category filtering
7. Write integration tests verifying report accuracy

**Definition of Done**:
- Finance module fully operational with GL integrity
- All four financial reports generate correctly with accurate totals
- PDF printing works for all reports
- CSV export produces properly formatted files
- Backup/restore cycle preserves all financial data
- All financial workflows have acceptance tests passing

---

### Phase 3: Advanced Features & Polish (2–3 weeks)

**Deliverables**:
- Backup/restore with protobuf serialization
- Import validation (schema version, entity completeness, atomic transactions)
- Audit trail logging (12-month retention, startup purge)
- Committee membership tracking with year-based assignments
- Member reactivation with automatic debt forgiveness (GL write-offs)
- Error handling and user-friendly messaging
- WCAG AA contrast compliance in both themes
- Graceful degradation for slow/failing dashboard tiles

**Key Tasks**:

**Subphase 3a: Backup & Restore** (1 week)
1. Design protobuf schema for all entities
2. Implement backup service (export to protobuf binary)
3. Implement restore service (import from protobuf with validation)
4. Add pre-import backup checkpoint creation
5. Add schema version validation and entity completeness checking
6. Implement atomic import (validate all before committing)
7. Write integration tests for backup/restore cycles

**Subphase 3b: Committee & Member Lifecycle** (1 week)
1. Implement committee membership tracking (year-based)
2. Build committee history display on member detail screen with **semantic HTML + ARIA**: current year as `<strong>2026 <span role="status">Current</span> - Treasurer</strong>`, historical as plain `<span>2025 - Secretary</span>`, badge with pastel background color (WCAG AA contrast compliant)
3. Implement **manual committee reset trigger** (Settings > General tab button "Reset Committee for New Year") with confirmation dialog; add AGM reminder logic (banner if AGM event exists and reset not completed 7 days post-AGM)
4. Build member reactivation GL write-off logic with **fee override capability**: dialog shows fees by year (prior years pre-checked, current year optional); coordinator can override to forgive current-year fees case-by-case
5. Implement debt forgiveness workflow with GL reversals per selected fees
6. Write acceptance tests for committee operations, manual reset, AGM reminder, reactivation with override
7. Add audit trail entries for all member lifecycle events

**Subphase 3c: Polish & Error Handling** (1 week)
1. Implement comprehensive error handling with user-friendly messages
2. Add graceful degradation for dashboard tiles (failed tiles skip without blocking render)
3. Add loading indicators for slow operations
4. Implement WCAG AA contrast compliance testing (automated)
5. Verify dark/light theme consistency
6. Add structured error logging for all failures
7. Test edge cases (corrupted database, missing directories, plugin failures, etc.)

**Definition of Done**:
- Backup/restore produces no data loss
- Import validation prevents corrupted restores
- Committee membership operations work correctly
- Member reactivation produces GL write-offs with audit trail
- All user-facing errors have clear, tested messages
- WCAG AA compliance verified in both themes
- All edge cases handled gracefully

---

### Phase 4: Testing & Documentation (1–2 weeks)

**Deliverables**:
- Unit tests for all domain logic (≥90% code coverage)
- Integration tests for all data access operations
- UI/acceptance tests for all user workflows (per spec user stories)
- Performance benchmarks (optional advisory)
- Architecture documentation
- API documentation for plugin developers
- Test automation in CI/CD pipeline

**Key Tasks**:
1. Write unit tests for all business logic classes
2. Write integration tests for all repository operations
3. Write UI acceptance tests for all user workflows
4. Verify all acceptance scenarios from spec pass
5. Test plugin registration and loading
6. Test error scenarios and edge cases
7. Document plugin development guide
8. Create architecture documentation for future maintainers

**Definition of Done**:
- All user story acceptance scenarios pass
- ≥90% code coverage on business logic
- CI/CD pipeline runs full test suite on all PRs
- All tests pass consistently
- Documentation complete and reviewed

---

## 3. Data Model & Schema Design

### 3.1 Entity Relationship Diagram (ERD)

```
Member
├── id (PK)
├── name
├── streetAddress
├── phone (optional)
├── email (optional)
├── joinDate
├── dateOfBirth (optional)
├── status (Active|Inactive)
├── activateDate
├── inactivateDate
├── isDeleted (soft-delete)
├── deletedAt
├── deletedBy
├── 1:N → Attendance
├── 1:N → Participation
├── 1:N → Fee
├── 1:N → Payment
├── 1:N → Transaction
├── 1:N → CommitteeMembership
└── 1:N → AuditTrail

CommitteeMembership
├── id (PK)
├── memberId (FK)
├── year
├── position
├── isDeleted
├── createdAt
├── modifiedAt
└── (UK: memberId + year)

Rehearsal
├── id (PK)
├── date
├── time
├── notes (optional)
├── storedAttendanceRate (decimal %, immutable; calculated at recording time)
├── 1:N → Attendance
└── 1:N → Fee (attendance fees)

Event
├── id (PK)
├── date
├── eventType (FK)
├── notes (optional)
├── storedParticipationRate (decimal %, immutable; calculated at recording time)
├── 1:N → Participation
└── (no direct fees; participation tracked only)

Attendance
├── id (PK)
├── rehearsalId (FK)
├── memberId (FK)
├── recordedAt
├── (UK: rehearsalId + memberId)

Participation
├── id (PK)
├── eventId (FK)
├── memberId (FK)
├── recordedAt
├── (UK: eventId + memberId)

Fee
├── id (PK)
├── memberId (FK)
├── feeType (Annual|Attendance|Other)
├── amount (decimal 2+ places)
├── feeDate
├── dueDate
├── createdAt (for FIFO tiebreaker)
└── (immutable after creation; no soft-delete fields per Constitution §3.4)

Payment
├── id (PK)
├── date (immutable)
├── amount (immutable)
├── paymentMethod (Cash|Check|Card|Electronic Transfer|Other; immutable)
├── paymentType (Annual|Attendance|Other; immutable)
├── memberId (FK)
├── category (FK to Category; immutable)
├── notes (editable with audit trail)
├── createdAt
├── updatedAt (updates ONLY when Notes changes)
├── 1:N → Transaction (GL pairs)
└── (immutable except notes; field-level immutability enforced)

Transaction (General Ledger)
├── id (PK)
├── date (required)
├── category (FK; implies GL account)
├── debitAmount (decimal 2+ places)
├── creditAmount (decimal 2+ places)
├── memberId (FK, optional)
├── paymentId (FK, optional)
├── description
├── createdAt
├── modifiedAt
└── (immutable paired entries; no soft-delete fields per Constitution §3.4)

Category
├── id (PK)
├── name
├── type (Income|Expense)
├── sortOrder
├── isArchived (soft-delete)
├── glAccount (auto-assigned: GL#0100-0101 for Assets, GL#10xx for Income, GL#20xx for Expense, GL#9900 for BadDebtExpense)
└── 1:N → Transaction

Settings
├── id (PK, singleton)
├── organizationName
├── annualFee
├── attendanceFee
├── renewalMonth (1-12, for annual fee application)
├── committeeRenewalMonth (1-12, default 1, for annual committee status reset)
├── lastCommitteeResetYear (int, default current year - 1, tracks last reset for guard against duplicates)
├── maxAgeRange (default 150)
├── minimumMemberAge (default 0)
├── theme (Dark|Light)
├── createdAt
└── modifiedAt

AuditTrail
├── id (PK)
├── entityType
├── entityId
├── action (Create|Update|Delete)
├── userId (fixed "system" in MVP; user ID in Phase 2+)
├── timestamp
├── oldValue
└── newValue
```

### 3.2 Key Schema Decisions

**Soft-Delete Pattern** (Constitution §3.4):
- All entities (Member, Rehearsal, Event, Category, CommitteeMembership) include `IsDeleted` (boolean), `DeletedAt` (DateTime?), `DeletedBy` (string?) fields
- Set ONLY on explicit archive operations; NOT on inactivation (Member status change is separate)
- All queries automatically filter `WHERE IsDeleted=false` unless explicitly retrieving deleted records
- **Exception**: Financial records (Fee, Transaction) are EXEMPT from soft-delete pattern per Constitution §3.4; these entities have NO soft-delete fields at all, ensuring immutability at the schema level. Corrections to financial data are handled via GL reversing transactions (paired debits/credits), never via deletion or modification.

**Effective Dates for Historical Queries**:
- Member includes `ActivateDate`, `InactivateDate` (immutable after set)
- Enable accurate historical active-member counts for attendance/participation rates
- Query pattern: `WHERE Status='Active' AND ActivateDate <= event_date AND (InactivateDate IS NULL OR InactivateDate > event_date)`
- **Stored Rates**: Rehearsal and Event entities include `StoredAttendanceRate` and `StoredParticipationRate` (decimal, immutable) calculated at event recording time; rates are frozen in history and never recalculated; archival does NOT retroactively change past rates; archive date affects only future rate calculations

**Attendance Immutability**:
- Attendance records are **immutable after creation**; recorded via batch interface per rehearsal (Member Name | [Attended ☐] | [Paid ☐] checkboxes)
- Attendance fees default to PAID; override checkbox "Mark fee as unpaid (override)" available at creation time only (during batch recording)
- After Save/OK, records locked (no clearing, no editing, no deletion in UI)
- If coordinator needs to correct attendance error post-save, must use manual GL reversals via Finance module (debit MemberReceivable + credit Income category)
- Attendance records remain permanent in database

**Financial Immutability** (Constitution §3.5):
- Fee records: Immutable after creation; Amount, Date, Type, DueDate locked; no edits in UI; fee state (PAID/UNPAID) locked at creation
- Transaction records: Immutable; paired debit/credit entries; no deletions
- Payment records: Amount/Date/Category locked; only Notes editable with audit trail
- Member Reactivation: GL write-offs for selected fees (prior-year default; current-year optional override per coordinator selection)
- Corrections via reversing transactions (GL pairs), not deletions

**GL Pair Structure**:
- Every financial event creates exactly TWO Transaction records: Debit + Credit
- Debit.Amount = Credit.Amount
- GL account structure (per FR-032 clarification):
  - **Asset Accounts**: GL#0100 (Cash), GL#0101 (MemberReceivable) — Fixed
  - **Income Accounts**: GL#10xx range — Auto-assigned sequentially (first income category GL#1000, second GL#1001, etc.)
  - **Expense Accounts**: GL#20xx range — Auto-assigned sequentially (first expense category GL#2000, second GL#2001, etc.)
  - **Write-off Account**: GL#9900 (BadDebtExpense) — Fixed for reactivation debt forgiveness
- GL account assigned automatically via GLAccountAssignmentService when coordinator creates a new category (no user input needed)
- **GLAccountAssignmentService.AssignGLAccountAsync(Category category) Algorithm**:
  ```
  AssignGLAccountAsync(category):
  - If category.Type == Income:
    a. Query all Income categories where IsArchived=false AND IsDeleted=false, 
       ordered by: CreatedAt ASC (oldest first), then Id ASC (GUID comparison for determinism)
    b. Count matching categories = N
    c. Assign GL# = 1000 + N (e.g., first income = GL#1000, second = GL#1001, etc.)
    d. Max GL# for Income = 1099 (100 categories max per type)
    e. If N >= 100, reject with error: "Cannot create category: maximum 100 income 
       categories already defined. Please archive unused categories first."
  - If category.Type == Expense:
    a. Same logic: GL# = 2000 + N (max 2099)
  - Timestamp tiebreaker: If multiple categories have identical CreatedAt (rare), use Id ascending 
    (deterministic GUID comparison) for stable, consistent ordering
  - Storage: Persist GL# to Category.GlAccount field (nullable string, e.g., "1000")
  ```
- GL account derived deterministically from Category type at creation time; no runtime GL lookups needed
- Example: Payment of $100 Cash creates: Debit $100 on GL#0100 (Cash) + Credit $100 on GL#0101 (MemberReceivable)
- Example: Fee creation creates: Debit $50 on GL#0101 (MemberReceivable) + Credit $50 on GL#1000 (first income category)

**Committee Membership**:
- CommitteeMembership entity with year-based unique constraint (Member + Year)
- Annual reset based on configurable CommitteeRenewalMonth setting (default January=1, distinct from membership renewal month)
- **Manual trigger only**: Coordinator clicks "Reset Committee for New Year" button in Settings > General tab; system displays confirmation dialog then clears all current-year committee status (CommitteeMembership records with Year = current year); preserves prior-year history as read-only; updates Settings.LastCommitteeResetYear
- **AGM Reminder Logic**: On app startup, system checks if AGM event exists in current year and LastCommitteeResetYear < current year; if AGM date is >7 days ago, displays banner on Settings page: "⚠️ Committee membership has not been reset for [current year]. AGM was [N days ago]. [Click to reset]"; banner remains until reset completed
- Reset ensures exactly once per committee year (guarded by LastCommitteeResetYear); coordinators manually re-enter committee assignments via member edit form
- Historical records preserved as read-only in Committee History section with **current-year entry visually distinct using semantic HTML**: `<strong>2026 <span role="status" aria-label="Current year">Current</span> - Position</strong>` (current year renders bold with badge; historical as plain text); badge uses pastel background color (light theme: hsl(120, 40%, 70%), dark theme: hsl(120, 35%, 55%)) with WCAG AA contrast compliance

---

### 3.3 Database Migration Strategy

**Versioning**: Semantic versioning (major.minor.patch) for schema versions

**Migration Tools**: EF Core Code-First migrations with custom context factory

**Plugin Integration**: Base DAL discovers plugin DbContexts; plugins provide custom migrations

**Backup Compatibility**: Schema version included in backup manifest; import validates version compatibility

---

## 4. API & Interface Contracts

### 4.1 Dashboard Tile Provider Contract

```csharp
public interface IDashboardTileProvider
{
    /// <summary>
    /// Unique identifier for this tile (e.g., "members", "rehearsals", "finance")
    /// </summary>
    string TileId { get; }

    /// <summary>
    /// Display title (e.g., "Members", "Outstanding Balance")
    /// </summary>
    string Title { get; }

    /// <summary>
    /// Display order (lower numbers first)
    /// </summary>
    int DisplayOrder { get; }

    /// <summary>
    /// Load tile data. Must complete within 5 seconds.
    /// </summary>
    Task<DashboardTileData> GetTileDataAsync();

    /// <summary>
    /// Render the tile content as a Blazor component type.
    /// </summary>
    Type GetComponentType();
}

public class DashboardTileData
{
    public string TileId { get; set; }
    public object Data { get; set; }
    public DateTime LoadedAt { get; set; }
    public bool IsSuccess { get; set; }
    public string ErrorMessage { get; set; } // null if success
}
```

**Core Tiles**:
- **Members Tile**: Active member count + Inactive count
- **Rehearsals Tile**: Most recent past rehearsal date + attendance rate (%) + running count
- **Events Tile**: Most recent past event date + participation rate (%) + running count
- **Finance Tile**: Total outstanding balance (with muted Green/Red color coding)

**Error Handling**: Failed tiles logged; render skipped; other tiles continue

---

### 4.2 Report Provider Contract

```csharp
public interface IReportProvider
{
    /// <summary>
    /// Module name this report belongs to (e.g., "Members", "Finance", "Attendance Analytics")
    /// </summary>
    string ModuleName { get; }

    /// <summary>
    /// Unique report identifier within module (e.g., "member-list", "income-statement")
    /// </summary>
    string ReportId { get; }

    /// <summary>
    /// Display name (e.g., "Member List", "Income Statement")
    /// </summary>
    string ReportName { get; }

    /// <summary>
    /// Display order within module (lower numbers first)
    /// </summary>
    int DisplayOrder { get; }

    /// <summary>
    /// Generate report data. Returns structured rows/columns with headers.
    /// </summary>
    Task<ReportData> GenerateAsync(ReportFilter filter);
}

public class ReportData
{
    public string ReportId { get; set; }
    public string ReportName { get; set; }
    public List<string> ColumnHeaders { get; set; }
    public List<List<string>> Rows { get; set; }
    public List<ReportSummary> Summaries { get; set; } // Subtotals, totals, etc.
    public DateTime GeneratedAt { get; set; }
}

public class ReportFilter
{
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public string CategoryFilter { get; set; } // Optional category ID
    public string MemberStatusFilter { get; set; } // "Active" | "Inactive" | "Archived" | "All"
    public Dictionary<string, string> CustomFilters { get; set; } // For plugin-specific filters
}
```

**MVP Report Implementations**:

1. **Member List** (Members module):
   - Columns: Name, Street Address, Phone, Email, Join Date, Age (if DOB), Status
   - Filters: Member Status (Active/Inactive/Archived/All)
   - Implementation: MemberListReportProvider

2. **Committee Report** (Members module):
   - Columns: Member Name, Year, Position
   - Filters: Member Status (Active Only/Archived Only/All)
   - Organization: By year (most recent first)
   - Implementation: CommitteeReportProvider

3. **Income Statement** (Finance module):
   - Sections: Revenue (income categories), Expenses (expense categories)
   - Columns: Category | Amount
   - Subtotals: Revenue total, Expense total
   - Grand Total: Net Income/Loss
   - Filters: Date range, category
   - Implementation: IncomeStatementReportProvider

4. **Trial Balance** (Finance module):
   - Sections: Asset Accounts, Income Accounts, Expense Accounts
   - Columns: Account Name | Debit | Credit | Balance
   - Validation: Total Debits MUST = Total Credits (within 0.01)
   - Filters: Date range
   - Implementation: TrialBalanceReportProvider

5. **Account Register** (Finance module):
   - Columns: Date, Description, Category, Debit, Credit, Running Balance
   - Sort: Chronological by date
   - Filters: Date range, category
   - Implementation: AccountRegisterReportProvider

6. **Member Account Summary** (Finance module):
   - Columns: Member Name, Opening Balance, Transactions (with dates), Closing Balance, Aging
   - Aging Buckets: Current, 30 days, 60 days, 90+ days
   - Includes: Archived members (for financial completeness)
   - Filters: Date range, member status
   - Implementation: MemberAccountSummaryReportProvider

---

### 4.3 Data Access Provider Contract (Plugin Extensibility)

```csharp
public interface IDataAccessProvider
{
    /// <summary>
    /// Provides custom DbContext with plugin-defined entities
    /// </summary>
    DbContext GetDbContext();

    /// <summary>
    /// Register custom entity types with base DAL
    /// </summary>
    void RegisterEntities(ModelBuilder modelBuilder);

    /// <summary>
    /// Provide repository implementations for plugin entities
    /// </summary>
    IEnumerable<object> GetRepositories();
}
```

**Usage**: Base DAL auto-discovers plugin implementations; configures DI container with plugin repositories

---

### 4.4 Settings Tab Provider Contract

```csharp
public interface ISettingsTabProvider
{
    /// <summary>
    /// Tab identifier (unique within Settings module)
    /// </summary>
    string TabId { get; }

    /// <summary>
    /// Display label (e.g., "General", "Categories", "Analytics Settings")
    /// </summary>
    string TabLabel { get; }

    /// <summary>
    /// Display order (core: 0–99; plugins: 100+)
    /// </summary>
    int DisplayOrder { get; }

    /// <summary>
    /// Render tab content as Blazor component type
    /// </summary>
    Type GetComponentType();
}
```

**Core Tabs** (Settings module):
- General Settings (order 0): Organization name, fees, renewal month, age settings
- Categories (order 10): Income/expense category management
- Event Types (order 20): Event type configuration
- Backup (order 30): Backup functionality
- Restore (order 40): Restore functionality

**Plugin Tabs**: Start at order 100+; platform auto-discovers and renders

---

## 5. Tech Stack & Tooling

### 5.1 Core Technologies

| Layer | Technology | Version | Justification |
|-------|-----------|---------|---------------|
| **Framework** | .NET | 8+ | LTS release, modern C# features, cross-platform |
| **UI** | MAUI BlazorWebView | Latest | Native Windows/macOS, web UI, single-view navigation |
| **Components** | Blazor | Latest | Component-based UI, C# code-sharing, reactive data binding |
| **Database** | SQLite | Latest | File-based, no server, embedded, portable |
| **ORM** | Entity Framework Core | Latest | Code-first migrations, LINQ, strong typing |
| **Styling** | Bootstrap 5 | 5.x | Responsive grid, components, theme support |
| **Serialization** | Protocol Buffers (protobuf) | 3.x | Compact binary, fast deserialization, schema versioning |
| **Logging** | Serilog | Latest | Structured logging, sinks, semantic logging |
| **Testing** | xUnit + Moq | Latest | Unit testing, mocking, fluent assertions |
| **DI** | Microsoft.Extensions.DependencyInjection | Latest | Built-in, lightweight, plugin registration |

### 5.2 NuGet Package List

**Core**:
- `Microsoft.Maui`
- `Microsoft.AspNetCore.Components.WebView.Maui`
- `EntityFrameworkCore.Sqlite`
- `EntityFrameworkCore.Tools`

**Utilities**:
- `Serilog`
- `Serilog.Sinks.Console`
- `Serilog.Sinks.File`
- `Google.Protobuf` (protobuf support)
- `Google.Protobuf.Tools` (protoc compiler)
- `iTextSharp` or similar (PDF generation for reports)

**Testing**:
- `xUnit`
- `Moq`
- `FluentAssertions`

### 5.3 Project Structure

```
StageFrightCommunity/
├── src/
│   ├── StageFright.Maui/                 # Main MAUI app project
│   │   ├── App.xaml / App.xaml.cs
│   │   ├── MainPage.xaml / MainPage.xaml.cs
│   │   ├── MauiProgram.cs                # DI configuration
│   │   ├── Platforms/                    # Platform-specific code
│   │   └── wwwroot/                      # Blazor static assets
│   │
│   ├── StageFright.Core/                 # Core domain logic
│   │   ├── Entities/                     # Member, Rehearsal, Event, etc.
│   │   ├── Exceptions/                   # Custom exception types
│   │   ├── Services/                     # Business logic services
│   │   ├── Contracts/                    # Repository interfaces
│   │   └── Constants/                    # Enums, constants
│   │
│   ├── StageFright.Data/                 # Data access layer
│   │   ├── Context/                      # DbContext
│   │   ├── Repositories/                 # Repository implementations
│   │   ├── Migrations/                   # EF Core migrations
│   │   └── Queries/                      # Query helpers
│   │
│   ├── StageFright.UI/                   # Blazor components
│   │   ├── Components/                   # Reusable components
│   │   ├── Pages/                        # Module pages (Members/, Finance/, etc.)
│   │   ├── Shared/                       # Shared layouts (MainLayout, Shell)
│   │   ├── Styles/                       # CSS (Bootstrap overrides, themes)
│   │   └── Services/                     # UI-specific services
│   │
│   ├── StageFright.Plugins/              # Plugin infrastructure
│   │   ├── Contracts/                    # IDashboardTileProvider, IReportProvider, etc.
│   │   ├── Discovery/                    # Plugin discovery & loading
│   │   └── Registration/                 # DI plugin registration
│   │
│   ├── StageFright.Reports/              # Reports infrastructure
│   │   ├── Contracts/                    # Report provider contracts
│   │   ├── Services/                     # Report generation & aggregation
│   │   ├── Components/                   # Common report viewer
│   │   └── Exporters/                    # PDF, CSV exporters
│   │
│   └── StageFright.Proto/                # Protobuf definitions
│       ├── *.proto                       # .proto files for backup/restore
│       └── Generated/                    # Generated C# code
│
├── tests/
│   ├── StageFright.Core.Tests/
│   ├── StageFright.Data.Tests/
│   ├── StageFright.UI.Tests/
│   └── StageFright.Integration.Tests/
│
├── docs/
│   └── PLUGIN_DEVELOPMENT.md             # Plugin development guide
│
└── Plugins/                              # Plugin directory (auto-created at runtime)
    └── (plugin assemblies loaded here)
```

### 5.4 Configuration & Environment

**appsettings.json**:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  },
  "Database": {
    "ConnectionString": "Data Source=statefright.db"
  },
  "Backup": {
    "DefaultPath": "~/Documents/StageFright/Backups"
  },
  "Plugins": {
    "Directory": "Plugins",
    "AutoLoad": true
  }
}
```

**Theme Configuration** (CSS Variables):
```css
/* Light Theme */
:root.light {
  --primary-color: hsl(270, 35%, 70%);    /* Muted purple */
  --background: #ffffff;
  --text: #333333;
  /* ... */
}

/* Dark Theme */
:root.dark {
  --primary-color: hsl(270, 35%, 50%);    /* Darker muted purple */
  --background: #1a1a1a;
  --text: #f5f5f5;
  /* ... */
}
```

---

## 6. Risks & Mitigation

### 6.1 Risk Register

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|-----------|
| **Double-entry accounting complexity** | Medium | High | Comprehensive testing, GL balance validation before reports, detailed documentation, pair programming on GL logic |
| **Plugin architecture tight coupling** | Medium | Medium | Strict interface contracts, dependency injection, comprehensive plugin integration tests, error isolation |
| **Financial data integrity loss** | Low | Critical | Immutable fee/transaction design, atomic transactions, comprehensive backup/restore tests, soft-delete audit trails |
| **Report generation performance degradation** | Low | Medium | 5-second timeout with cancel option, synchronous generation (simpler), performance benchmarking on startup |
| **WCAG compliance across themes** | Low | Medium | Automated contrast ratio testing, WCAG audit on both themes, accessibility testing with screen readers |
| **Member effective date historical calculation errors** | Medium | High | Comprehensive test scenarios for historical queries, edge case testing (status changes, archives), query validation in reports |
| **Protobuf schema versioning complexity** | Low | Medium | Detailed migration documentation, backward-compatibility testing, clear version upgrade guidance in UI |
| **Plugin discovery failures** | Low | Low | Graceful error handling (skip failed plugins), structured error logging, user notification in Settings |
| **Soft-delete filter performance** | Low | Medium | Database indexes on IsDeleted field, query optimization, performance monitoring |
| **Payment allocation FIFO accuracy** | Medium | High | Comprehensive FIFO test scenarios, manual aging analysis verification, clear documentation of allocation algorithm |

### 6.2 Risk Mitigation Strategies

**1. Double-Entry Accounting Validation**:
- Implement GL balance verification before every report generation
- Automated tests for all GL transaction scenarios (payments, fees, write-offs, reversals)
- Paired transaction creation enforced at repository level
- Trial Balance report built-in validation (debits = credits)

**2. Plugin Architecture Stability**:
- Strict, minimal interface contracts (no implementation details exposed)
- Exception handling at plugin boundary (catch and log, don't propagate)
- Plugin load failures isolated; application continues with remaining plugins
- Comprehensive plugin integration tests before release

**3. Financial Data Safety**:
- Immutable Fee/Transaction design (locked fields, no deletions)
- Soft-delete pattern for audit trail (Constitution §3.4)
- Pre-import backup checkpoints (FR-013)
- Atomic import with full validation before commit (FR-015)
- Comprehensive backup/restore integration tests

**4. Member Historical Accuracy**:
- Effective date fields (ActivateDate, InactivateDate) immutable after set
- Historical query tests covering all status transitions
- Edge case scenarios: member reactivation, archive/unarchive, date boundaries
- Validation queries in attendance/participation rate calculations

**5. Theme & Accessibility**:
- Automated HSL color palette validation (saturation <50%, lightness 60–80%)
- WCAG AA contrast ratio testing on both themes
- Screen reader testing for semantic HTML (tab roles, labels, etc.)
- Accessibility audit as part of QA gate

**6. Performance & Responsiveness**:
- 5-second report generation timeout with UI cancel option
- Dashboard tile loading with timeout and graceful fallback
- SQLite query optimization and indexing strategy
- Performance benchmarking on startup with advisory logging

---

## 7. Constitution Compliance Checklist

✅ **§3.1 Clean Code**: All modules follow naming conventions, single responsibility, focused functions  
✅ **§3.2 SOLID Principles**: Interface segregation (IReportProvider, IDashboardTileProvider), dependency inversion (repositories)  
✅ **§3.3 Separation of Concerns**: Clear layer boundaries (UI, Application, Domain, Data), no cross-boundary coupling  
✅ **§3.4 Soft Delete Pattern**: IsDeleted/DeletedAt/DeletedBy fields on all entities except financial records; immutable soft-delete  
✅ **§3.5 Financial Data Preservation**: Fee/Transaction records immutable; member reactivation via GL write-offs, not deletion  
✅ **§4.3 Settings System**: Tabbed interface with module-specific tabs and plugin extensibility  
✅ **§6.0 Audit Trail**: 12-month retention with startup-only purge; all modifications logged (who, what, when)  
✅ **§6.7 Data Preservation**: All historical data retained; soft-delete enables restore; archived members queryable for reports

---

## 8. Success Criteria

**Phase Completion Gates**:
- Phase 0: All infrastructure compiles; database migrations work; first repository test passes
- Phase 1: All user story acceptance scenarios pass; dashboard displays; first-run setup completes successfully
- Phase 2: All financial reports generate with accurate totals; GL balance verification passes; backup/restore cycle preserves all data
- Phase 3: Committee operations work correctly; member reactivation produces GL write-offs; all error messages tested for clarity
- Phase 4: All acceptance tests pass; ≥90% code coverage; CI/CD pipeline runs all tests on every PR

**Merge Gate Requirements**:
- All unit & integration tests pass (≥90% coverage)
- All user story acceptance tests pass
- Code review approved (2+ reviewers)
- No new WCAG violations introduced
- No regressions in existing functionality

---

## 9. Task Generation Readiness

This implementation plan provides sufficient detail to support automated task generation:

- **Clear deliverables** for each phase and subphase
- **Specific technical decisions** (MAUI, Blazor, EF Core, protobuf)
- **Entity models** with relationships and constraints
- **Interface contracts** for plugins and reports
- **Acceptance criteria** per user story in spec
- **Risk mitigation** strategies identified
- **Testing strategy** (unit, integration, acceptance, accessibility)

The plan is **ready for task generation** and can be used to create granular, independently-testable sprint tasks with clear acceptance criteria tied to specification requirements.

---

## Appendix: Referenced Specifications

- **Specification**: [specs/001-initial-mvp/spec.md](../001-initial-mvp/spec.md)
- **Constitution**: [.specify/memory/constitution.md](../../.specify/memory/constitution.md)
- **Architectural Guidance**: [docs/ARCHITECTURE.md](../../docs/ARCHITECTURE.md)
