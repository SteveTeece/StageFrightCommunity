# StageFright Community — Initial MVP Specification

**Template-Version**: 2.1.0  
**Required-Constitution-Version**: 2.1.1  
**Last-Updated**: 2026-05-15  
**Feature Branch**: `001-initial-mvp`  
**Created**: 2026-05-15  
**Status**: Draft  
**Input**: Create initial MVP for StageFright Community desktop application

---

## 1. Purpose *(mandatory)*

StageFright Community MVP is a desktop application that provides small performing arts groups with a unified tool to manage core operations: member registration, rehearsal attendance, performance scheduling, and basic financial tracking. This MVP replaces manual spreadsheet-based workflows with an intuitive, modern interface supporting Windows and macOS, emphasizing reliability, simplicity, and modularity for long-term maintainability.

The MVP establishes the foundation for extensibility through a plugin architecture while delivering immediate value through essential operational features.

---

## 2. Scope *(mandatory)*

### 2.1 In Scope

**Core Modules:**
- **Dashboard Module**: Display at-a-glance statistics for Members, Rehearsals, Events, and Finance with plugin-driven extensibility
- **Members Module**: Register, manage, and track member profiles with status tracking (Active/Inactive)
- **Rehearsals Module**: Schedule rehearsals, record attendance, and track per-rehearsal attendance fees
- **Events Module**: Schedule performances/events, track participation, and manage event types
- **Settings Module**: Organization settings, annual fee configuration, renewal-month management, and category/event-type administration
- **Finance Module**: Control all financial transactions (membership fees, payments, income/expenses), user-defined categories, accounting compliance, and generation of standard accounting reports (Income Statement, Trial Balance, Account Register, Member Account Summary) with print capability (PDF + physical printer) and CSV export
- **Theme Management**: Dark/light theme toggle with persisted user preference

**Core Features:**
- Local SQLite database storage with backup/restore capabilities
- First-run setup wizard (organization name, fee configuration, renewal month)
- Annual membership fee application with batch processing
- Outstanding balance tracking (annual + attendance fees combined)
- Member lifecycle management (Active/Inactive/Archived states)
- Committee membership tracking with per-year assignments and historical records
- Audit trail logging (12-month retention with startup purge)
- Pre-import backup checkpoint with user confirmation
- Atomic import/export with schema version validation
- Plugin architecture: plugin discovery in `Plugins` directory with auto-creation on startup
- Bootstrap 5 styling with pastel/muted color palette and rounded corners
- Desktop shell with dark brand strip, purple StageFright wordmark, and white navigation bar

**UI/UX:**
- Two-tier startup shell (brand strip + navigation bar)
- Two-column dashboard card layout
- Tabbed interfaces for multi-function pages using Blazor tab controls
- Dark and light theme support with WCAG contrast compliance
- Progressive tile loading with graceful degradation for slow/failing tiles
- Module-contributed menu entries with deep-linking via query parameters

### 2.2 Out of Scope

- Cloud synchronization and online storage
- Multi-user authentication and role-based access control
- Online payments and payment processing
- Public-facing features (websites, ticketing, marketing)
- Handheld and tablet device versions
- Real-time collaboration
- Enterprise features (advanced reporting, data warehousing)
- Non-desktop operating system variants

---

## 3. User Scenarios & Testing *(mandatory)*

### User Story 1 - First-Run Setup (Priority: P1)

A new user launches StageFright Community for the first time. The system presents a setup wizard allowing the user to configure their organization (name, annual membership fee, attendance fee per rehearsal, and membership renewal month). Upon completion, the system initializes the database and presents an empty dashboard ready for use.

**Why this priority**: Without first-run setup, users cannot get started. This is the on-ramp that enables all downstream functionality.

**Independent Test**: Can be fully tested by launching the application with an empty database, completing the setup wizard, and verifying organization data is persisted.

**Acceptance Scenarios**:

1. **Given** a fresh installation, **When** the application launches, **Then** a setup wizard is displayed with fields for organization name, annual fee, attendance fee, and renewal month
2. **Given** the setup wizard is displayed, **When** the user enters valid data and clicks Save, **Then** organization settings are persisted and the dashboard is displayed
3. **Given** the dashboard is displayed after setup, **When** the user navigates to Settings, **Then** the previously entered organization settings are shown

---

### User Story 2 - Member Registration and Management (Priority: P1)

A group coordinator registers new members into the system. The system records member name, contact information, and date of joining. Members can be marked as Active or Inactive. The coordinator can view a list of all members, filter by status, and edit member details. Additionally, the coordinator can track which members serve on the committee each year and record their position/role on the committee. Committee membership history is preserved across years.

**Why this priority**: Member management is foundational—all other features (rehearsals, events, fees) depend on having registered members. Committee tracking is essential for governance and organizational transparency.

**Independent Test**: Can be fully tested by creating members, listing them, editing details, toggling inactive status, marking committee membership, and viewing committee history independently of other modules.

**Acceptance Scenarios**:

1. **Given** the Members module is open, **When** the user clicks "Add Member", **Then** a form is displayed with fields for name, phone, email, and join date
2. **Given** a member form is displayed, **When** the user enters valid data and clicks Save, **Then** the member is created and listed in the active members view
3. **Given** an active member is listed, **When** the user clicks to mark them Inactive, **Then** the member is hidden from the default active list but remains in the database
4. **Given** an inactive member exists, **When** the user reactivates them, **Then** the member is returned to the active list and prior year unpaid fee status is cleared (fresh-start behavior)
5. **Given** a member is listed, **When** the user clicks Edit, **Then** all fields are editable and changes are persisted
6. **Given** a member edit form is displayed, **When** the user checks "Committee Member" checkbox, **Then** a position field becomes required and editable
7. **Given** a member is marked as committee member with position entered, **When** the user saves, **Then** the member is recorded as committee member for the current year
8. **Given** a member detail screen is displayed, **When** the member has committee history, **Then** a "Committee History" section shows all years of service with positions, with current year visually distinct from historical records
9. **Given** a member detail screen for someone with no committee history, **When** I view the page, **Then** no committee section is displayed or shows "No committee history"
10. **Given** calendar year advances to a new year, **When** I view a member who was on committee previous year, **Then** that historical record is preserved and new committee status can be assigned for the current year independently

---

### User Story 3 - Rehearsal Scheduling and Attendance Recording (Priority: P1)

A group coordinator schedules upcoming rehearsals and records member attendance. When attendance is recorded, the system automatically creates an attendance fee record (unpaid by default). The coordinator can view rehearsal history, attendance rates, and outstanding attendance fee balances.

**Why this priority**: Rehearsal tracking is core to group operations. Accurate attendance enables financial tracking and participation metrics.

**Independent Test**: Can be fully tested by scheduling rehearsals, recording attendance, and verifying fee accrual independently.

**Acceptance Scenarios**:

1. **Given** the Rehearsals module is open, **When** the user clicks "Schedule Rehearsal", **Then** a form is displayed with date, time, and optional notes
2. **Given** a rehearsal is scheduled, **When** the user records attendance by selecting members present, **Then** attendance records are created and attendance fees are automatically marked as unpaid
3. **Given** attendance has been recorded, **When** the user views the Rehearsals tile on the dashboard, **Then** historical attendance rate is displayed (based on members active on that date)
4. **Given** attendance fees exist, **When** the user views the Finance tile, **Then** outstanding attendance fees are included in the total outstanding balance

---

### User Story 4 - Annual Membership Fee Application (Priority: P1)

At the configured renewal month, a group coordinator applies annual membership fees to all active members. The system checks for unpaid fees from the current year and skips members who already have an outstanding fee. A batch processing dialog shows progress and allows the user to confirm before applying.

**Why this priority**: Automated fee application reduces manual administrative work and ensures consistent billing practices.

**Independent Test**: Can be fully tested by setting the renewal month, adding active members, and executing the fee application batch process.

**Acceptance Scenarios**:

1. **Given** active members exist and the renewal month arrives, **When** the user clicks "Apply Annual Fees", **Then** a confirmation dialog is displayed showing the number of members to be charged
2. **Given** the confirmation dialog is displayed, **When** the user confirms, **Then** annual fee records are created for all eligible members (excluding those with existing unpaid fees for the current year)
3. **Given** annual fees have been applied, **When** the user views the Finance tile, **Then** outstanding annual fees are visible in the total outstanding balance

---

### User Story 5 - Event/Performance Scheduling and Participation Tracking (Priority: P2)

A group coordinator schedules upcoming performances and records participation. Event types (e.g., Performance, Eisteddfod, Fund raiser) are configured in Settings. When an event is recorded, the coordinator can mark which members participated. The system tracks historical participation data and displays it on the dashboard.

**Why this priority**: Event tracking enables participation metrics and builds a historical record. Secondary to core rehearsal/member management but important for performance groups.

**Independent Test**: Can be fully tested by creating event types, scheduling events, recording participation, and viewing participation data independently.

**Acceptance Scenarios**:

1. **Given** the Settings module is open, **When** the user navigates to Event Types, **Then** default types (Performance, Eisteddfod, Fund raiser, Promotional) are displayed
2. **Given** event types are configured, **When** the user creates an event in the Events module, **Then** a form is displayed with event date, type, and optional notes
3. **Given** an event is created, **When** the user records participation by selecting members present, **Then** participation records are created
4. **Given** participation has been recorded, **When** the user views the Events tile on the dashboard, **Then** historical participation rate is displayed

---

### User Story 6 - Finance Tracking and Outstanding Balance Visibility (Priority: P1)

The Finance module displays the total outstanding balance (annual fees + attendance fees combined). The coordinator can view individual member balances, payment history, and apply payments. Financial data is categorized (income/expense types) and can be reported on. All financial transactions follow accounting standards and best practices including accurate transaction dating, categorization, and audit trails.

**Why this priority**: Financial visibility is essential for group administration. Accurate balance tracking and accounting compliance enable informed decision-making and meet legal/governance requirements.

**Independent Test**: Can be fully tested by creating fees, applying payments, viewing categorized transactions, verifying balance calculations, and generating accounting reports independently.

**Acceptance Scenarios**:

1. **Given** annual and attendance fees have been recorded, **When** the user views the Finance tile, **Then** total outstanding balance is displayed as the sum of all unpaid annual + attendance fees with muted Green (positive balance/surplus) or muted Red (negative balance/deficit) color coding
2. **Given** the Finance module is open, **When** the user views member balances, **Then** each member's outstanding fees are displayed with individual annual and attendance fee breakdowns
3. **Given** outstanding fees exist, **When** the user applies a payment, **Then** a payment record is created with date, amount, payment method (Cash, Check, Card, etc.), category, and optional notes
4. **Given** a payment is applied, **When** the user views the balance, **Then** outstanding fees are reduced accordingly and transaction history is updated
5. **Given** a financial transaction is recorded, **When** the user views transaction details, **Then** all accounting information is visible: transaction date, amount, category, description, member (if applicable), and accounting status (income/expense)
6. **Given** the Finance module is open, **When** the user views categorized transactions, **Then** transactions are grouped by category (income categories and expense categories) with running totals

---

### User Story 6a - Accounting Reports and Financial Statements (Priority: P1)

The Finance module generates standard accounting reports viewable on screen with print capability. Reports include Income Statement (revenues and expenses by category with subtotals), Trial Balance (all account balances for verification), Account Register (detailed transaction list by account/category with running balance), and Member Account Summary (individual member balances with transaction history). All reports follow accounting standards with proper date ranges, categorization, subtotals, and totals. Reports can be printed to PDF or physical printer.

**Why this priority**: Accounting reports are essential for financial management, audit trails, and group governance. Non-negotiable requirement per constitutional mandate.

**Independent Test**: Can be fully tested by creating transactions across multiple categories, generating each report type, verifying accuracy and totals, and testing print functionality independently.

**Acceptance Scenarios**:

1. **Given** the Finance module is open, **When** the user accesses Reports, **Then** a Report Selection interface displays available reports: Income Statement, Trial Balance, Account Register, and Member Account Summary
2. **Given** a report is selected, **When** the user specifies date range and other filters (category, member), **Then** the report is generated and displayed with all data populated correctly
3. **Given** an Income Statement report is displayed, **When** reviewing the data, **Then** income categories are listed with subtotal, expense categories are listed with subtotal, and net income/loss is calculated and displayed
4. **Given** a Trial Balance report is displayed, **When** reviewing the data, **Then** all account balances are shown with debit/credit columns and totals verify to zero (accounting fundamental)
5. **Given** an Account Register report is displayed, **When** reviewing transactions, **Then** transactions are sorted chronologically by date with running balance updated after each transaction
6. **Given** a Member Account Summary report is displayed, **When** reviewing member balances, **Then** each member shows opening balance, transactions for period, and closing balance with aging of outstanding fees (current/30/60/90+ days)
7. **Given** a report is displayed on screen, **When** the user clicks Print, **Then** a print dialog appears allowing user to print to PDF or physical printer with professional formatting
8. **Given** a report is printed, **When** the output is reviewed, **Then** all headers, column labels, subtotals, and grand totals are properly formatted and clearly readable
9. **Given** a report is displayed on screen, **When** the user clicks Export to CSV, **Then** a CSV file is generated with all column headers and data rows with proper comma-escaping and quote-escaping for special characters
10. **Given** a CSV export is downloaded, **When** the file is opened in a spreadsheet application, **Then** all columns are properly aligned and all data is readable with headers intact

---

### User Story 7 - Category Management for Income and Expenses (Priority: P1)

The Settings module provides a Category Management interface where users can create, edit, archive, and restore custom income/expense categories. The system prevents archiving categories that are referenced by any transaction (including soft-deleted transactions). Categories can be reordered by the user.

**Why this priority**: Custom categories enable flexible financial tracking aligned with each group's accounting practices. This is an MVP-required feature per the core spec.

**Independent Test**: Can be fully tested by creating categories, applying them to transactions, and verifying archival validation independently.

**Acceptance Scenarios**:

1. **Given** the Settings module is open, **When** the user navigates to Categories, **Then** existing categories are displayed with edit and archive options
2. **Given** the Categories interface is displayed, **When** the user creates a new category, **Then** the category is added to the list
3. **Given** a category exists, **When** the user attempts to archive it while it is referenced by transactions, **Then** an error is displayed explaining the reference prevents archival
4. **Given** a category is archived, **When** the user navigates to the archive view, **Then** archived categories are displayed with a restore option

---

### User Story 8 - Dashboard Overview and Plugin Extensibility (Priority: P1)

The dashboard displays four core tiles: Members (count of active/inactive), Rehearsals (last attendance rate and most recent past rehearsal date), Events (last participation rate and most recent past event date), and Finance (total outstanding balance). Third-party plugins can contribute additional tiles without modifying core code. Tiles load progressively and degrade gracefully if slow or failing.

**Why this priority**: The dashboard is the application entry point. Extensibility enables future feature additions without core modifications.

**Independent Test**: Can be fully tested by verifying core tiles display correct data and that a test plugin tile can be registered and displayed.

**Acceptance Scenarios**:

1. **Given** the dashboard is displayed, **When** the page loads, **Then** all four core tiles are visible and data loads progressively
2. **Given** a core tile is loading data, **When** the tile provider returns data slowly, **Then** the dashboard remains responsive and displays other tiles while waiting
3. **Given** a core tile provider fails, **When** the dashboard renders, **Then** the failed tile is skipped, other tiles display, and a structured error is logged
4. **Given** a plugin registers a dashboard tile, **When** the dashboard renders, **Then** the plugin tile is displayed after core tiles

---

### User Story 9 - Backup and Restore (Priority: P2)

The user can manually trigger a backup of all application data (members, rehearsals, events, financial records, settings, categories). The system exports data in a versioned schema format. The user can restore from a previous backup. Before any import, the system requires an explicit pre-import backup checkpoint and user confirmation.

**Why this priority**: Data protection and disaster recovery are essential. Backup enables non-destructive import workflows.

**Independent Test**: Can be fully tested by creating data, backing up, clearing the database, and restoring independently.

**Acceptance Scenarios**:

1. **Given** the Settings module is open, **When** the user clicks "Backup", **Then** a backup file is created with a timestamp and schema version metadata
2. **Given** a backup file exists, **When** the user clicks "Restore", **Then** a confirmation dialog is displayed with pre-import backup creation and user acknowledgment required
3. **Given** the user confirms restore, **When** the restore completes, **Then** all data is restored and the system displays a success message

---

### User Story 10 - Dark/Light Theme Support (Priority: P2)

The user can toggle between dark and light themes. The theme preference is persisted in the Settings database record. All UI surfaces comply with WCAG contrast requirements in both themes. Bootstrap 5 styling and pastel/muted color palette are applied consistently across both themes.

**Why this priority**: Theme support improves user experience and accessibility. Secondary to core features but important for long-term usability.

**Independent Test**: Can be fully tested by toggling themes and verifying UI renders correctly in both modes.

**Acceptance Scenarios**:

1. **Given** the application is running in light theme, **When** the user clicks the theme toggle, **Then** the UI switches to dark theme
2. **Given** the theme is changed, **When** the user closes and reopens the application, **Then** the previously selected theme is restored
3. **Given** both themes are rendered, **When** color contrast is measured, **Then** all text and UI elements meet WCAG AA contrast requirements

---

### User Story 11 - Reports Menu and Shared Report Viewing/Printing Infrastructure (Priority: P1)

The application provides a "Reports" root menu item that aggregates reports from all MVP modules and plugins. Members module contributes member-focused reports; Finance module contributes accounting reports. Each module is responsible for generating report data and rendering the report view. A shared, common report viewing and printing infrastructure handles display, PDF export, and CSV export capabilities that any module can use. Reports are printed to PDF through the common infrastructure.

**Why this priority**: Centralized reporting enables consistent user experience and extensibility for future modules. Common report infrastructure eliminates code duplication and ensures all modules follow the same print/export patterns.

**Independent Test**: Can be fully tested by registering reports from multiple modules (Members, Finance), viewing each report, printing to PDF, and exporting to CSV independently.

**Acceptance Scenarios**:

1. **Given** the application is running, **When** the user clicks the Reports menu, **Then** a Reports root menu item is displayed with submenus from all modules that contribute reports
2. **Given** the Reports menu is expanded, **When** the user views available reports, **Then** Member reports are listed (e.g., "Member List", "Committee Report") and Finance reports are listed (e.g., "Income Statement", "Trial Balance", "Account Register", "Member Account Summary")
3. **Given** a report is selected from the menu, **When** the report is clicked, **Then** the report is generated and displayed in a common report viewer component with print and export buttons
4. **Given** a report is displayed in the viewer, **When** the user clicks Print, **Then** the report is exported to PDF and a print dialog appears
5. **Given** a report is displayed in the viewer, **When** the user clicks Export to CSV, **Then** the CSV file is generated and downloaded
6. **Given** a plugin module is loaded, **When** the plugin registers a report via `IReportProvider` contract, **Then** the plugin report is added to the Reports menu under the plugin module's section
7. **Given** a report is loading data, **When** the module is processing report logic, **Then** the common report viewer displays a loading indicator while the report data is being prepared
8. **Given** a report generation fails, **When** the error occurs, **Then** the common report viewer displays a user-friendly error message explaining the failure and recovery options

---

### Edge Cases

- What happens when the application is launched with a corrupted database file? → Graceful error handling with recovery options
- How does the system handle missing required directories (Plugins, database path)? → Auto-create on startup, log event
- What happens when an import file has an unsupported schema version? → Reject with clear upgrade guidance
- How does the system handle payment records if the referenced member is archived? → Payment records remain; archived member remains queryable
- What happens if the audit log retention purge fails on startup? → Log structured error and continue startup; skip purge for that run
- How does the system handle plugin assembly loading failures? → Log structured error, skip failed plugin, continue startup with available plugins
- What happens when a Settings tab provider (from a plugin) fails? → Skip failed tab, render other tabs, log structured error
- How does the system handle member reactivation when prior unpaid fees from different years exist? → Keep historical records; reactivation clears current-year fee status only
- What happens when a report provider (from a plugin) fails to register or render? → Log structured error, skip failed report, render other reports in Reports menu, display error message

---

## 4. Requirements *(mandatory)*

### Functional Requirements

**FR-001**: System MUST display a first-run setup wizard that captures organization name, annual membership fee, attendance fee per rehearsal, and membership renewal month (1-12) and persists these to the Settings database record.

**FR-002**: System MUST provide a Members module enabling user to create, edit, list, and filter members by Active/Inactive status; members MUST include name, phone, email, and join date fields.

**FR-003**: System MUST support member lifecycle states: Active (participating), Inactive (not participating but not deleted), and Archived (soft-deleted). Marking a member Inactive MUST NOT set soft-delete fields (`IsDeleted`/`DeletedAt`/`DeletedBy`); archival is a separate explicit action.

**FR-004**: System MUST automatically apply annual membership fees to all active members at the configured renewal month, skipping members with existing unpaid fees for the current year, and display a batch processing confirmation dialog.

**FR-005**: System MUST provide a Rehearsals module enabling the user to schedule rehearsals (date, time, optional notes) and record attendance. Recording attendance MUST automatically create attendance fee records marked as unpaid by default.

**FR-006**: System MUST provide an Events module enabling the user to schedule performances/events (date, event type, optional notes) and record participation. Event types MUST be configurable in Settings with defaults: Performance, Eisteddfod, Fund raiser, Promotional.

**FR-007**: System MUST track and display historical attendance rate (members present / members active on that date × 100%) for the most recent past rehearsal and participation rate for the most recent past event.

**FR-008**: System MUST track outstanding balances combining annual membership fees and per-rehearsal attendance fees, displaying total outstanding balance on the Finance tile with muted Green for positive balance (income > expenses) and muted Red for negative balance (expenses > income).

**FR-009**: System MUST support custom income/expense categories in the Settings module with create/edit/archive/restore/reorder operations. Archiving MUST be prevented if any transaction (including soft-deleted transactions) references the category.

**FR-010**: System MUST implement a dashboard with four core tiles (Members, Rehearsals, Events, Finance) and support plugin-driven dashboard tile registration without core modifications.

**FR-011**: System MUST display dashboard tiles progressively and degrade gracefully if a tile provider fails or is slow; failed providers MUST log structured errors and not block dashboard render.

**FR-012**: System MUST provide Backup functionality exporting all application data (members, rehearsals, events, financial records, settings, categories, relationships) in a versioned schema format with metadata including `schemaVersion`, generation timestamp, and entities map.

**FR-013**: System MUST require a pre-import backup checkpoint and explicit user confirmation before any import writes data; import MUST be atomic (validate full payload first, then commit all changes in one transaction).

**FR-014**: System MUST validate import schema version and reject unsupported major versions with clear upgrade guidance to the user.

**FR-015**: System MUST use non-destructive import mode (upsert): existing records are updated, missing records are inserted, and local records not present in the source remain unchanged.

**FR-016**: System MUST support payment recording with date, amount, payment method (Cash, Check, Card, etc.), and optional notes; payments reduce outstanding balances accordingly.

**FR-017**: System MUST allow Notes field editing on financial records with audit trail logging (who changed what when); Amount, Date, and Category fields MUST remain locked after creation.

**FR-018**: System MUST provide a Settings module with tabs: General Settings (organization + fees), Categories, Event Types, Backup, and Restore. Plugin modules may contribute additional Settings tabs via tab provider contract.

**FR-019**: System MUST support dark and light theme toggle with persisted preference in the Settings database record; all UI surfaces MUST comply with WCAG AA contrast requirements in both themes.

**FR-020**: System MUST use Bootstrap 5 card styling with rounded corners and compact spacing; pastel/muted color palette (HSL lightness 60–80%, saturation <50%) for UI surfaces and accents; dark-theme variants may adjust absolute lightness while maintaining WCAG contrast compliance.

**FR-021**: System MUST auto-create the `Plugins` directory at application root on startup if missing; plugin discovery MUST scan the `Plugins` directory for assemblies; gracefully handle read-only filesystem errors.

**FR-022**: System MUST implement audit trail logging (who, what, when) for all data modifications and retain logs for 12 months; purge expired logs on application startup only. If startup purge fails, log structured error and continue startup.

**FR-023**: System MUST maintain member activation/inactivation effective dates to enable historical active-member count computation based on the rehearsal/event date (not current date).

**FR-024**: System MUST reset member fee status to current year upon reactivation (Inactive→Active); prior unpaid fees from other years remain in historical records but are not actively owed unless explicitly restored.

**FR-025**: System MUST handle payment method as required field on Payment records, defaulting to `Cash` when not explicitly selected; PaymentType remains separate representing fee type (Annual/Attendance).

**FR-026**: System MUST define and use custom exceptions for domain/application/infrastructure failures; raw framework exceptions MUST be translated before crossing boundaries.

**FR-027**: System MUST track committee membership for each member on a per-calendar-year basis. Member edit form MUST include a "Committee Member" checkbox and a "Position" text field (max 100 characters). If checkbox is marked, Position field MUST be required. If checkbox is unchecked, no position is recorded.

**FR-028**: System MUST preserve committee membership history across calendar years. Each year's committee assignment is independent; members can have different positions in different years or no position in some years.

**FR-029**: System MUST display committee history on the member detail screen showing all years in which the member served on committee with their corresponding positions. Current year MUST be visually distinct from historical records. Members with no committee history MUST have no committee section displayed.

**FR-030**: System MUST include automated tests proving coverage of all reachable code paths for committee membership operations (add/update/remove/query); all committee-related workflows MUST have corresponding UI integration tests.

**FR-031**: System MUST require current year committee membership to be reassigned each year; historical records from previous years MUST be automatically preserved and displayed when the calendar year advances.

**FR-032**: System MUST implement accounting compliance by ensuring all financial transactions are recorded with: transaction date (required), category (required, selected from user-defined categories), amount (required), member reference (when applicable), payment method (required for payments), and description/notes (optional). All transactions MUST follow the categorization as either income or expense.

**FR-033**: System MUST generate an Income Statement report showing revenue (income categories with amounts) and expenses (expense categories with amounts) organized by category with subtotals for each section and net income/loss calculation. Report MUST allow date range filtering.

**FR-034**: System MUST generate a Trial Balance report displaying all accounts with their balances organized as income accounts and expense accounts. Debit and credit columns MUST be shown with totals verifying to equal amounts (fundamental accounting principle). Report MUST include date as of which the trial balance is calculated.

**FR-035**: System MUST generate an Account Register report showing all transactions in chronological order by date within selected categories. Each transaction MUST display: date, description, category, debit amount (for expenses), credit amount (for income), and running balance. Running balance MUST update correctly after each transaction.

**FR-036**: System MUST generate a Member Account Summary report showing each member with opening balance (beginning of period), all transactions affecting that member during the period (fees, payments, adjustments), and closing balance (end of period). Outstanding fees MUST be aged showing current, 30-day, 60-day, and 90+ day categories.

**FR-037**: System MUST provide print capability for all financial reports allowing users to print to PDF or physical printer. Printed reports MUST include: title, date range, generation date, all column headers, all data rows with proper alignment, subtotals, and grand totals. Printed format MUST be professional and clearly readable.

**FR-038**: System MUST ensure all financial transaction amounts are stored with proper precision (minimum 2 decimal places); all calculations MUST maintain precision without rounding errors throughout arithmetic operations.

**FR-039**: System MUST enforce accounting transaction integrity: every debit MUST have corresponding credit (balanced entry principle). When a payment is recorded, income account is credited and cash/receivable is debited. When a fee is assigned, expense/receivable account is debited and income account is credited.

**FR-040**: System MUST include automated tests proving coverage of all reachable code paths for Finance module operations (payments, reporting, categorization); all financial workflows MUST have corresponding UI integration tests verifying report accuracy.

**FR-041**: System MUST provide CSV export capability for all financial reports (Income Statement, Trial Balance, Account Register, Member Account Summary). Exported CSV files MUST include all column headers as the first row and all data rows with proper CSV formatting (comma-separated values, quote-escaping for special characters, comma-escaping for field values containing commas).

**FR-042**: System MUST implement a centralized, extensible base data access layer (DAL) that owns all MVP module data access (Members, Rehearsals, Events, Finance, Settings, Categories, Audit Trail). The DAL MUST use Entity Framework Core with SQLite and provide repository contracts for each entity type. DAL MUST support schema migration-based extensibility allowing plugins to define their own entities and create corresponding database tables through code-first migrations without modifying core DAL code.

**FR-043**: System MUST provide a plugin data access contract allowing plugins to register custom entity types and repository implementations with the base DAL. Plugins MUST be able to define new database entities, create tables, and provide repository implementations for their own data without modifying core MVP module data access. Plugin data MUST be persisted to the same SQLite database with automatic schema migration support.

**FR-044**: System MUST include automated tests proving coverage of all reachable code paths for data access layer operations (CRUD, transactions, migrations); all data persistence workflows MUST have corresponding integration tests verifying data integrity and schema correctness.

**FR-045**: System MUST implement a Reports root menu item in the main navigation that aggregates all available reports from all modules (MVP and plugins). Reports MUST be organized by module name with submenus for each module's reports. Reports menu organization MUST follow the module order: Members reports, then Finance reports, then plugin reports alphabetically by module name.

**FR-046**: System MUST provide a report provider contract (`IReportProvider`) allowing MVP modules and plugins to register custom reports. Each report provider MUST specify: report name, report ID, module name, display order within module, and a method to generate report data. Module providers MUST generate report data; the report infrastructure MUST handle display, printing, and exporting.

**FR-047**: System MUST implement a shared common report viewer component that all modules use for displaying reports. The report viewer MUST support: displaying report data on screen, printing to PDF through a print dialog, exporting to CSV, and displaying loading indicators while report data is being generated. The viewer MUST have consistent UI/UX across all modules.

**FR-048**: System MUST support report data generation abstraction where each module provides the report data (structured as rows/columns with headers) and the common viewer handles rendering, print-to-PDF, and CSV export. Modules MUST NOT implement their own print or export logic; all modules MUST use the common infrastructure.

**FR-049**: System MUST include error handling for report providers: if a report provider fails to register or fails to generate report data, the system MUST log a structured error, skip the failed report, continue rendering other reports in the Reports menu, and display a user-friendly error message in the report viewer if the user attempts to view the failed report.

**FR-050**: System MUST support the following MVP module reports: Member module reports (Member List, Committee Report) and Finance module reports (Income Statement, Trial Balance, Account Register, Member Account Summary). All reports MUST be accessible through the Reports menu.

**FR-051**: System MUST include automated tests proving coverage of all reachable code paths for report provider registration, report data generation, common report viewer rendering, print-to-PDF, and CSV export functionality; all report workflows MUST have corresponding UI integration tests.

---

### Non-Functional Requirements

**NFR-001**: **Architecture**: System MUST use a modular Blazor Hybrid (MAUI BlazorWebView) architecture; all functional UI MUST be rendered by Blazor components; all route transitions MUST be executed through `NavigationManager.NavigateTo(...)` in a single BlazorWebView. Navigation-policy audit compliance is documented in `CONSTITUTION-COMPLIANCE-UPDATE.md`.

**NFR-002**: **Data Storage**: System MUST use local SQLite database for all data persistence; schema versioning MUST follow semver notation (major.minor.patch); import/export manifests MUST include `schemaVersion`.

**NFR-003**: **Performance**: Performance testing is not required for MVP acceptance; teams may profile and record advisory benchmarks for their environment but no numeric SLAs are mandated.

**NFR-004**: **Accessibility**: All UI elements MUST comply with WCAG AA contrast requirements. All user-facing error messages MUST be validated in user testing for clarity (≥90% of users understand the message without assistance).

**NFR-005**: **Testing & Merge Gate**: Full acceptance suite (all per-story acceptance tests) MUST run on every PR. All user journeys MUST have corresponding UI integration tests.

**NFR-006**: **Theme Support**: System MUST support both dark and light themes; color palette compliance (HSL lightness 60–80%, saturation <50%) MUST be verified by automated tests.

**NFR-007**: **Error Handling**: System MUST provide graceful error handling with user-friendly messages for validation failures, business logic errors, and technical failures. Dashboard tiles MUST degrade gracefully if slow or failing.

**NFR-008**: **Observability**: System MUST log structured errors (context, timestamps, error types) for all failures including plugin load failures, import validation failures, and dashboard tile provider failures.

**NFR-009**: **UI/UX Shell**: System MUST implement a desktop shell with dark brand strip (purple StageFright wordmark) and white module navigation bar with organization title on left and route links on right. Dashboard MUST default to two-column card layout.

**NFR-010**: **Tab Controls**: Every page except the dashboard that exposes multiple functions MUST use Blazor tab controls with accessible semantics (`role="tablist"`, `role="tab"`, `role="tabpanel"`). Module/plugin-contributed tabs MUST support deep-linking via query parameters.

**NFR-011**: **Plugin Architecture**: System MUST support plugin registration via assembly discovery in the `Plugins` directory; plugins contribute dashboard tiles and optional Settings tabs via provider contracts without modifying core code.

**NFR-012**: **Data Preservation**: All audit trails, payment records, and historical data MUST be preserved per Constitution §6.7; soft-delete fields (`IsDeleted`, `DeletedAt`, `DeletedBy`) MUST be set only for explicit archive operations, not for inactivation.

**NFR-013**: **Security**: MVP requires single-user, no authentication/authorization; unrestricted access to all functions. Security and multi-user support deferred to Phase 2+. All audit trails logged for compliance.

**NFR-014**: **Data Protection**: MVP uses OS/device-level protection; app-level database encryption is deferred to Phase 2+. No formal external regulatory/compliance framework is required for MVP.

**NFR-015**: **Accounting Compliance**: Finance module MUST follow double-entry accounting principles with debits equaling credits for all transactions. Transaction integrity MUST be enforced at database level with constraints. All reports MUST calculate and display accurate totals and subtotals. Decimal precision MUST be maintained at 2+ decimal places for all monetary amounts throughout all calculations.

**NFR-016**: **Financial Reporting**: Finance module MUST generate all required reports with professional formatting suitable for screen display, printing, and CSV export. Reports MUST be generated in-memory and rendered on-screen with print-to-PDF capability and CSV export functionality. All reports MUST include proper headers, date ranges, categories, subtotals, and grand totals.

**NFR-017**: **Data Access Layer Architecture**: System MUST implement a centralized, extensible base data access layer using Entity Framework Core with SQLite. All MVP module data access (Members, Rehearsals, Events, Finance, Settings, Categories, Audit Trail) MUST be consolidated in the base DAL with repository contracts. Plugin architecture MUST support data access extensibility through entity registration and code-first migrations without requiring core code modifications. Plugin entities MUST be persisted to the shared SQLite database with automatic schema migration.

**NFR-018**: **Reports Infrastructure**: System MUST implement a shared, common reports infrastructure providing consistent report viewing, printing, and exporting capabilities across all modules (MVP and plugins). Each module is responsible for generating report data (structured rows/columns with headers); the common infrastructure handles all display, PDF printing, and CSV exporting. Report provider auto-discovery MUST follow the same pattern as dashboard tiles and settings tabs with graceful error handling for failed providers.

---

### Responsibilities

**Feature Responsibilities (MVP Core)**:
- Dashboard module owns dashboard tile aggregation and progressive loading
- Members module owns member CRUD, lifecycle management, committee membership tracking, fee status tracking, and generation of member-focused reports (Member List, Committee Report) via report provider contract
- Rehearsals module owns scheduling, attendance capture, and attendance fee accrual
- Events module owns performance scheduling, participation tracking, and event type management
- Finance module owns all financial transactions, category management, balance tracking, payment recording, accounting compliance (double-entry principles), and generation of accounting report data (Income Statement, Trial Balance, Account Register, Member Account Summary) via report provider contract
- Settings module owns organization configuration, user-defined category definitions, theme persistence, and backup/restore
- Reports infrastructure module owns common report viewing, PDF printing, and CSV export capabilities shared across all MVP and plugin modules
- Plugin architecture owns assembly discovery, tile registration, settings tab provider contracts, and report provider contracts

**Non-Responsibilities (Deferred)**:
- Authentication and authorization (Phase 2+)
- Cloud synchronization (Phase 2+)
- Multi-user support (Phase 2+)
- Advanced reporting and data warehousing (Phase 2+)
- Online payments (Phase 2+)
- Mobile/tablet variants (Phase 2+)

---

### Interfaces / Contracts

**Inbound Contracts**:
- First-run setup form → Settings initialization
- Member CRUD operations → Member repository
- Rehearsal scheduling form → Rehearsal repository
- Event scheduling form → Event repository
- Payment recording form → Finance repository
- Category management form → Category repository
- Backup/restore triggers → Data export/import service
- Theme toggle → Settings update and UI theme application

**Outbound Dependencies**:
- Data access layer (SQLite repository contracts)
- Domain models (Member, Rehearsal, Event, Payment, Category, Settings)
- Exception translation contracts across layers
- Audit trail logging service
- Plugin discovery and loader service
- Blazor navigation via `NavigationManager`

**Plugin Extension Contracts**:
- Dashboard tile provider: `IDashboardTileProvider` with tile rendering and data refresh
- Settings tab provider: `ISettingsTabProvider` with tab UI component, display order, and deep-link support

---

### Dependencies

**Internal Dependencies**:
- Entity Framework Core (EF Core) with SQLite provider for all data access
- Base data access layer (DAL) with repository contracts for all MVP entities
- Blazor Hybrid UI framework (MAUI BlazorWebView)
- Bootstrap 5 CSS framework
- Custom domain exception types
- Audit trail logging service
- Plugin assembly loader and reflection utilities
- Plugin data access provider contract and auto-discovery

**External Dependencies**:
- .NET 8+ runtime
- Windows and macOS native APIs (via MAUI)
- SQLite database engine
- Bootstrap 5 library

---

### Extension Points

**Plugin Architecture**:
- Dashboard tile provider registration: plugins implement `IDashboardTileProvider` and register via assembly discovery in `Plugins` directory
- Settings tab provider registration: plugins implement `ISettingsTabProvider` and contribute tabs to Settings module
- Settings tabs display order: core tabs use range 0-99, contributed tabs use range 100+; duplicate keys are skipped with logging
- Data access provider registration: plugins implement `IDataAccessProvider` and register custom entity types with the base DAL. Plugins provide DbContext with custom entities and repository implementations. Base DAL MUST auto-discover and integrate plugin entities during startup with code-first migration execution.

**Menu Extensibility**:
- Plugins can contribute menu entries via module-level menu provider contract
- Deep-linking to function tabs via route query keys (e.g., `?tab=plugin-feature`)

**Database Extensibility**:
- Plugins can define custom entities and register them with the base DAL
- Plugin entities are persisted to the shared SQLite database via EF Core migrations
- Plugin repositories are auto-discovered and injected into DI container for plugin modules

**Reports Extensibility**:
- MVP modules and plugins implement `IReportProvider` to register and provide reports
- Report provider auto-discovery via assembly scanning in `Plugins` directory for plugins, core assemblies for MVP modules
- Each report provider specifies: report ID, report name, module name, display order, and report data generation method
- Module generates structured report data (rows/columns with headers); common infrastructure handles rendering, printing, and exporting
- MVP modules (Members, Finance) use report provider contract to register their reports with the Reports menu
- Plugin modules can register custom reports through `IReportProvider` without modifying core Reports infrastructure

---

### Error Handling Requirements

**Exception Taxonomy**:
- `InvalidOperationException` → Business logic violations (e.g., applying fees to inactive members)
- `ValidationException` → Form/data validation failures (e.g., invalid email, missing required fields)
- `DataAccessException` → Database/repository failures (e.g., connection loss, corrupted data)
- `ImportException` → Schema version mismatch, unsupported major versions, payload validation
- `PluginLoadException` → Assembly load failures, missing dependencies

**Boundary Translation**:
- All framework exceptions (SQLite, reflection, IO) MUST be translated to domain exceptions before crossing module boundaries
- Plugin loading failures MUST be caught and logged; failed plugins MUST not prevent application startup

**User-Facing Recovery**:
- Invalid form input → Clear error message with field highlighting and recovery steps
- Database unavailable → Clear message with retry option and support contact
- Import schema mismatch → Message explaining supported versions and upgrade guidance
- Failed dashboard tile → Show "Unable to load" message; other tiles continue rendering
- Plugin load failure → Skip failed plugin; notify user in Settings if applicable

---

### Observability Requirements

**Required Logs**:
- Application startup/shutdown events
- First-run setup completion
- Batch operations (fee application, import/export)
- Member lifecycle changes (create, inactivate, archive, reactivate)
- Payment recording and balance adjustments
- Plugin assembly discovery and loading (success and failures)
- Settings tab provider registration and failures
- Audit trail: all data modifications (who, what, when)
- Backup/restore operations with timestamps and entity counts
- Error events with stack traces and context

**Required Traces/Metrics**:
- Fee application batch processing: members processed, fees created
- Dashboard tile loading: tile name, load time, success/failure
- Import/export operations: entities processed, validation times
- Plugin discovery: assemblies found, successfully loaded, failed

**Failure Telemetry**:
- Import schema validation failures
- Plugin load failures with assembly names and error details
- Dashboard tile provider failures
- Database integrity violations
- Audit log retention purge failures

---

### Constraints

**Platform Constraints**:
- Windows and macOS desktop only
- .NET 8+ and MAUI requirement
- Single-user application (no multi-user auth)

**Policy Constraints**:
- All route transitions via `NavigationManager.NavigateTo(...)` (NavigateTo-only enforcement)
- Soft-delete semantics: only explicit archive sets `IsDeleted`/`DeletedAt`/`DeletedBy`
- Inactivation is separate from archival; reactivation clears current-year fee status only
- Non-destructive import: upsert mode, no deletion of local records
- Atomic import: validate all before committing any
- Import requires pre-backup and user confirmation
- 12-month audit log retention with startup-only purge

**Security/Governance Constraints**:
- Single-user, no authentication required for MVP
- All data preserved per Constitution §6.7
- Graceful error handling required for all user-facing failures
- WCAG AA compliance for all UI elements

---

### Key Entities

**Member**:
- Unique identifier, name, phone, email, join date
- Status: Active, Inactive, or Archived (soft-deleted)
- Activation/inactivation effective dates (for historical active-member computation)
- Outstanding annual fee balance (current year)
- Outstanding attendance fee balance
- Relationships: registrations, payment records, attendance records, committee memberships

**CommitteeMembership**:
- Unique identifier, member reference (FK to Member), calendar year, position (role/title)
- Creation and modification timestamps
- Soft-delete flag (IsDeleted) for archive operations
- Unique constraint: Member + Year (one committee membership per member per year)
- Relationships: member details

**Rehearsal**:
- Unique identifier, date, time, optional notes
- Attendance records (many-to-many with Members)
- Attendance fee records (derived from attendance)
- Relationships: attendance entries, fee records

**Event/Performance**:
- Unique identifier, date, event type, optional notes
- Participation records (many-to-many with Members)
- Relationships: participation entries

**Payment**:
- Unique identifier, date, amount, payment method (Cash, Check, Card, etc.)
- Payment type (Annual membership or Attendance fee)
- Member reference
- Optional notes (editable with audit trail; amount/date/category locked)
- Relationships: audit trail entries

**Transaction** (General Ledger Entry):
- Unique identifier, transaction date (required), category (required, FK to Category)
- Transaction type (Fee/Payment/Adjustment)
- Debit amount (for expenses, fees)
- Credit amount (for income, payments)
- Member reference (when applicable)
- Description/notes
- Created and modified timestamps
- Soft-delete flag (IsDeleted) for archive operations
- Relationships: category, member, audit trail entries
- Constraint: Debit + Credit amounts MUST balance for each transaction

**Category**:
- Unique identifier, name, type (Income or Expense)
- Sort order
- Archived flag (soft-delete)
- Relationships: income/expense records, archived transactions

**Settings**:
- Singleton record: organization name, annual fee, attendance fee, renewal month
- Theme preference (Dark/Light)
- Relationships: audit trail entries for changes

**Audit Trail**:
- Unique identifier, entity type, entity ID, action (Create/Update/Delete)
- User identifier (single-user MVP: system/admin)
- Timestamp, old value, new value
- Retention: 12 months, purged on startup

---

## 5. Clarifications *(optional but recommended)*

### Session 2026-05-15 (MVP Specification Session)

- Q: For MVP scope, should the application support data import/export in addition to backup/restore? → A: Yes. Import/export with full schema versioning, atomic transactions, and pre-import backup checkpoints are MVP-required per FR-012 through FR-015.

- Q: Should the Finance tile prominently display both annual AND attendance fee breakdowns, or just the combined total outstanding balance? → A: Display combined total on the main Finance tile (per Session 2026-02-16 decision). Member-level details in the Finance module show both types separately.

- Q: For the first-run setup wizard, should it allow users to skip any fields or all fields are mandatory? → A: All fields are mandatory for first-run setup (organization name, annual fee, attendance fee, renewal month).

- Q: What accounting standards should the Finance module follow? → A: Double-entry accounting principles (all debits = credits) MUST be enforced. General ledger entries MUST be balanced. Reports MUST follow standard formats (Income Statement, Trial Balance, Account Register, Member Summary) with proper subtotals and grand totals.

- Q: Should the application support inventory or asset tracking? → A: No. MVP Finance module focuses exclusively on member fees, payments, and category-based income/expense tracking. Inventory and fixed asset accounting deferred to Phase 2+.

- Q: What decimal precision is required for financial amounts? → A: Minimum 2 decimal places (e.g., $12.50) MUST be maintained throughout all calculations. No rounding errors permitted.

- Q: Should financial reports be generated by external tools or within the application? → A: Reports MUST be generated in-memory by the Finance module with screen display and print-to-PDF capability. No external tools required.

- Q: What date basis should reports use (fiscal year vs calendar year)? → A: Calendar year basis (January 1 - December 31). Users can select custom date ranges for reports.

- Q: Should each MVP module have its own data access layer, or should there be a centralized DAL? → A: Centralized base data access layer (DAL) MUST own all MVP module data access. All repositories (Member, Rehearsal, Event, Finance, Category, Settings, Audit Trail, CommitteeMembership) MUST be in the base DAL with consistent contracts. This ensures maintainability and provides foundation for plugin extensibility.

- Q: How should plugins extend the data access layer to create their own tables? → A: Plugins register custom entities and repositories through `IDataAccessProvider` contract. Base DAL auto-discovers plugin DbContext extensions and executes EF Core migrations for plugin entities. Plugin repositories are injected into DI for plugin modules to use. Shared SQLite database stores all MVP and plugin data.

- Q: Should each module implement its own report viewing and printing, or should there be a common infrastructure? → A: Common, shared report infrastructure MUST be used across all modules. Each module generates report data (structured rows/columns with headers) via `IReportProvider` contract; the common Reports infrastructure handles all display rendering, PDF printing, and CSV exporting. This eliminates code duplication, ensures consistent UI/UX, and enables future plugins to reuse the same infrastructure without implementing their own print/export logic.

---

## 6. Acceptance Criteria *(mandatory)*

**Specification Completeness**:
- [ ] All user stories have defined acceptance scenarios covering primary and edge-case flows
- [ ] All functional requirements are mapped to at least one user story
- [ ] All non-functional requirements are clear and verifiable
- [ ] Error handling requirements are defined for each requirement category
- [ ] UI/UX requirements (Bootstrap 5, color palette, theme support) are specified

**Technical Specification**:
- [ ] Plugin architecture contracts (`IDashboardTileProvider`, `ISettingsTabProvider`, `IDataAccessProvider`) are documented
- [ ] Database schema entities and relationships are defined
- [ ] Base data access layer (DAL) centralizes all MVP module data access with repository contracts
- [ ] Plugin data access extensibility contract and auto-discovery mechanism documented
- [ ] EF Core migrations support for plugin entity registration and schema extension documented
- [ ] Error taxonomy and boundary translation rules are explicit
- [ ] Audit trail logging requirements are complete (what to log, retention, purge policy)
- [ ] Data preservation rules align with Constitution §6.7
- [ ] Reports infrastructure contracts (`IReportProvider`) for module report registration documented
- [ ] Common report viewer component design and capabilities documented (display, print-to-PDF, CSV export)
- [ ] Report provider auto-discovery and error handling (graceful failure, error messages) documented
- [ ] MVP module reports (Members, Finance) are mapped to IReportProvider contract

**Testing Coverage**:
- [ ] Each user story has corresponding UI integration test scenarios
- [ ] All data access layer operations (CRUD, transactions, migrations) have integration test coverage
- [ ] Plugin data access registration and entity schema migration are testable scenarios
- [ ] Report provider registration, report data generation, and common viewer rendering have test coverage
- [ ] Report print-to-PDF and CSV export functionality has integration test coverage
- [ ] Report provider error handling and graceful failure scenarios are testable
- [ ] Edge cases are explicitly tested
- [ ] Error paths (validation failure, business logic violation, technical failure, data access errors, report generation errors) are covered
- [ ] Theme support compliance (WCAG contrast, dark/light rendering) is testable
- [ ] Plugin loading and graceful failure scenarios are testable

**UI/UX Acceptance**:
- [ ] First-run setup can be completed in under 2 minutes (per SC-001)
- [ ] All user-facing error messages are clear and actionable
- [ ] Dashboard tiles load progressively without blocking
- [ ] Bootstrap 5 styling and pastel color palette are applied consistently
- [ ] Dark and light themes render correctly with WCAG AA contrast

**MVP Scope Validation**:
- [ ] All core modules (Dashboard, Members, Rehearsals, Events, Settings, Finance) are included
- [ ] Reports menu with member and finance reports is implemented with common report infrastructure
- [ ] Common report viewing, printing (PDF), and CSV exporting available to all modules
- [ ] Plugin architecture enables future extensibility (reports and data access)
- [ ] No cloud, multi-user, or authentication requirements leak into MVP
- [ ] Backup/restore with atomic import and schema versioning is fully specified
- [ ] Theme support is complete and accessible

---

## 7. Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can complete first-run setup (organization name, fee values, renewal month) in under 2 minutes, measured from initial launch to successful save.

- **SC-002**: Dashboard displays all four core tiles (Members, Rehearsals, Events, Finance) with at-a-glance statistics and loads within 3 seconds of application startup on a typical development machine.

- **SC-003**: 90% of test users successfully complete their primary task (e.g., recording attendance, applying fees, registering a member) on first attempt in user testing with no assistance.

- **SC-004**: All core data operations (member CRUD, fee application, payment recording) are reversible via explicit user actions or archive/restore workflows; no accidental data loss occurs.

- **SC-005**: All user-facing error messages are validated in user testing for clarity; ≥90% of users understand the message and recommended recovery action without assistance.

- **SC-006**: All UI functions have passing integration tests covering primary and edge-case user journeys; graceful degradation scenarios (e.g., failed dashboard tile, import error) are covered by at least one integration test per user story.

- **SC-007**: Plugin system successfully loads and displays at least one test plugin dashboard tile without modifying core code; plugin loading failures do not prevent application startup.

- **SC-008**: Backup/restore operations complete successfully and restore all application data (members, rehearsals, events, financial records, settings) to the prior state with 100% data integrity.

- **SC-009**: Audit trail accurately records all data modifications (who, what, when) and retains logs for 12 months with startup purge executing without errors.

- **SC-010**: Dark and light themes render all UI elements with WCAG AA contrast compliance measured via automated accessibility tests.

- **SC-011**: First-run users can navigate to all core modules (Dashboard, Members, Rehearsals, Events, Settings, Finance) and understand the purpose of each module without documentation.

- **SC-012**: All financial reports (Income Statement, Trial Balance, Account Register, Member Account Summary) generate with 100% accuracy; report totals match source transaction data within 0.01 (accounting compliance verification).

- **SC-013**: Trial Balance reports verify that total debits equal total credits within 0.01 (fundamental double-entry accounting principle); any imbalance triggers an error preventing report generation.

- **SC-014**: Financial transaction decimal precision is maintained at 2+ decimal places throughout all calculations; no rounding errors occur in balance computations or report generation.

- **SC-015**: All four accounting report types can be printed to PDF with professional formatting (headers, columns properly aligned, subtotals, grand totals clearly visible); printed output is readable and suitable for archival.

- **SC-016**: Users can define and manage custom income and expense categories within Settings without requiring database modifications; categories immediately reflect in Finance module transaction categorization.
- **SC-017**: All four accounting reports can be exported to CSV format with column headers as first row; exported files open correctly in spreadsheet applications with proper column alignment and readable data.

- **SC-018**: Base data access layer successfully isolates all MVP module data access through centralized repository contracts; plugin system successfully registers custom entities and creates new database tables via EF Core migrations without modifying core DAL code.

- **SC-019**: Reports menu successfully displays reports from all MVP modules (Members: Member List, Committee Report; Finance: Income Statement, Trial Balance, Account Register, Member Account Summary) and plugin modules. All reports are viewable, printable to PDF, and exportable to CSV through the common report infrastructure with consistent UI/UX.

---

## 8. Implementation Notes

**Constitutional Alignment**: This MVP specification is governed by Spec Kit Constitution v2.1.1. Cross-cutting policies (architecture, error handling, data preservation, testing standards, JavaScript prohibition) are authoritative and referenced in this spec. Any approved deviations must be documented as constitution amendments and referenced here.

**Referenced Core Spec**: This MVP specification is derived from the comprehensive StageFright Community Core Application Specification (core-spec.md). Design decisions documented in that spec's clarification sessions (2026-02-04 through 2026-04-03) remain authoritative for MVP scope and are assumed to carry forward unless explicitly overridden by this MVP spec or future session clarifications.

**Financial Accounting Compliance**: Finance module implementation MUST strictly adhere to double-entry accounting principles with debits equaling credits. All reports MUST be independently verifiable against source transaction data. Decimal precision MUST be maintained throughout all calculations. These are non-negotiable requirements per user mandate.

**Data Access Layer Architecture**: Base DAL MUST be the single authoritative location for all MVP module data access. All entity repositories (Member, Rehearsal, Event, Payment, Category, Transaction, Settings, Audit Trail, CommitteeMembership) MUST be defined in the centralized DAL with consistent repository contracts. Plugin extensibility MUST support custom entity registration and automatic schema migration through `IDataAccessProvider` contract without core DAL modifications. This architectural pattern ensures maintainability, testability, and clean separation of concerns across MVP modules and future plugin additions.

**Reports Infrastructure Architecture**: MVP modules and plugins MUST use shared, common report infrastructure for all viewing, printing, and exporting functionality. Each module is responsible for generating report data (structured rows/columns with headers) and implementing a report provider (`IReportProvider`) contract. The Reports infrastructure module provides the common report viewer component, PDF printing, and CSV export capabilities that all modules use. This pattern eliminates code duplication, ensures consistent UI/UX across all reports, and allows plugins to register custom reports without implementing their own print/export logic. Reports are organized in a root "Reports" menu with submenus by module.

**Release Gate**: This MVP specification serves as the acceptance gate for the initial release. All user stories must be implemented, all acceptance scenarios must pass, and all measurable outcomes must be verified before MVP release to production.

