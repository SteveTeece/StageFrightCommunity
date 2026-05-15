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

A new user launches StageFright Community for the first time. The system presents a setup wizard allowing the user to configure their organization (name, annual membership fee, attendance fee per rehearsal, and membership renewal month). Upon completion, the system initializes the database schema and Settings, and presents an empty dashboard ready for use. No fees are automatically created during setup.

**Why this priority**: Without first-run setup, users cannot get started. This is the on-ramp that enables all downstream functionality.

**Independent Test**: Can be fully tested by launching the application with an empty database, completing the setup wizard, verifying organization data is persisted, and confirming no fees are present in the database after setup completion.

**Acceptance Scenarios**:

1. **Given** a fresh installation, **When** the application launches, **Then** a setup wizard is displayed with fields for organization name, annual fee, attendance fee, and renewal month
2. **Given** the setup wizard is displayed, **When** the user enters valid data and clicks Save, **Then** organization settings are persisted, database schema is initialized, and the dashboard is displayed with no fees present
3. **Given** the dashboard is displayed after setup, **When** the user navigates to Settings, **Then** the previously entered organization settings are shown
4. **Given** setup has been completed, **When** I check the database for any Fee records, **Then** no fees exist (fees are created only via manual "Apply Annual Fees" or attendance recording)

---

### User Story 2 - Member Registration and Management (Priority: P1)

A group coordinator registers new members into the system. The system records member name, contact information (street address, optional phone, optional email), join date, and optional date of birth. Members can be marked as Active or Inactive. The coordinator can view a list of all members, filter by status, and edit member details. The system displays the member's calculated age if date of birth is provided. Additionally, the coordinator can track which members serve on the committee each year and record their position/role on the committee. Committee membership history is preserved across years.

**Why this priority**: Member management is foundational—all other features (rehearsals, events, fees) depend on having registered members. Committee tracking is essential for governance and organizational transparency.

**Independent Test**: Can be fully tested by creating members, listing them, editing details, toggling inactive status, marking committee membership, viewing committee history, and verifying age calculation independently of other modules.

**Acceptance Scenarios**:

1. **Given** the Members module is open, **When** the user clicks "Add Member", **Then** a form is displayed with fields for name (required), street address (required), phone (optional), email (optional), join date (required), and date of birth (optional)
2. **Given** a member form is displayed with only required fields, **When** the user enters valid name, address, and join date (no phone, email, or date of birth) and clicks Save, **Then** the member is created and listed in the active members view without phone/email/age visible
3. **Given** a member form is displayed, **When** the user enters a valid date of birth and saves, **Then** the member's age is calculated and displayed on the member profile; if no date of birth is entered, no age field is visible
4. **Given** a member profile shows a calculated age, **When** the user views the profile on their next birthday, **Then** the age increments by 1
5. **Given** an invalid email format is entered, **When** the user attempts to save, **Then** a validation error is displayed and the form is not submitted
6. **Given** an invalid phone format is entered, **When** the user attempts to save, **Then** a validation error is displayed and the form is not submitted
7. **Given** a future date is entered for date of birth, **When** the user attempts to save, **Then** a validation error is displayed
8. **Given** an organization has a Minimum Member Age of 18 years configured in Settings, **When** the user attempts to register a member with calculated age 15 years, **Then** a validation error is displayed: "Member age (15 years) must be at least 18 years old"
9. **Given** an active member is listed, **When** the user clicks to mark them Inactive, **Then** the member is hidden from the default active list but remains in the database
9. **Given** an inactive member exists, **When** the user reactivates them, **Then** the member is returned to the active list and prior year unpaid fee status is cleared (fresh-start behavior)
10. **Given** a member is listed, **When** the user clicks Edit, **Then** all fields are editable and changes are persisted
11. **Given** a member edit form is displayed, **When** the user checks "Committee Member" checkbox, **Then** a position field becomes required and editable
12. **Given** a member is marked as committee member with position entered, **When** the user saves, **Then** the member is recorded as committee member for the current year
13. **Given** a member detail screen is displayed, **When** the member has committee history, **Then** a "Committee History" section shows all years of service with positions, with current year visually distinct from historical records
14. **Given** a member detail screen for someone with no committee history, **When** I view the page, **Then** no committee section is displayed or shows "No committee history"
15. **Given** calendar year advances to a new year, **When** I view a member who was on committee previous year, **Then** that historical record is preserved and new committee status can be assigned for the current year independently

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

At the configured renewal month, a group coordinator applies annual membership fees to all active members. The system checks for unpaid fees from the current year, skips inactive members, and skips active members who already have an outstanding annual fee. A batch processing dialog shows progress and allows the user to confirm before applying.

**Why this priority**: Automated fee application reduces manual administrative work and ensures consistent billing practices.

**Independent Test**: Can be fully tested by setting the renewal month, adding active and inactive members, and executing the fee application batch process.

**Acceptance Scenarios**:

1. **Given** active members exist, inactive members exist, and the renewal month arrives, **When** the user clicks "Apply Annual Fees", **Then** a confirmation dialog is displayed showing the number of active members to be charged (excluding inactive members)
2. **Given** the confirmation dialog is displayed, **When** the user confirms, **Then** annual fee records are created for all eligible active members (excluding inactive members and those with existing unpaid fees for the current year)
3. **Given** annual fees have been applied, **When** the user views the Finance tile, **Then** outstanding annual fees are visible in the total outstanding balance
4. **Given** an inactive member exists, **When** the annual fee application is executed, **Then** no fee is applied to the inactive member

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
4. **Given** a Trial Balance report is displayed, **When** reviewing the data, **Then** accounts are organized in three sections (Assets, Income, Expenses) with Account Name | Debit Amount | Credit Amount columns; each account shows its balance in the appropriate column and zero in the other
5. **Given** a Trial Balance report is generated, **When** the report data has been calculated, **Then** Total Debits row and Total Credits row are displayed at the bottom; both totals MUST be equal within 0.01 (fundamental accounting principle) or report generation fails with error message "GL Balance Verification Failed: Total Debits ($X.XX) ≠ Total Credits ($Y.YY). Please review and correct GL entries."
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

The user can manually trigger a backup of all application data (members, rehearsals, events, financial records, settings, categories, committee history, audit trails). The system exports data in a versioned schema format. The user can restore from a previous backup. Before any import, the system requires an explicit pre-import backup checkpoint and user confirmation. Import strictly validates that all required entity types are present; incomplete backups are rejected.

**Why this priority**: Data protection and disaster recovery are essential. Backup enables non-destructive import workflows. Strict validation prevents corrupted partial restores.

**Independent Test**: Can be fully tested by creating data, backing up, clearing the database, and restoring independently. Can be tested by attempting to restore an incomplete backup file (missing entity types) and verifying rejection.

**Acceptance Scenarios**:

1. **Given** the Settings module is open, **When** the user clicks "Backup", **Then** a backup file is created with a timestamp, schema version metadata, and all operational entities included
2. **Given** a backup file exists, **When** the user clicks "Restore", **Then** a confirmation dialog is displayed with pre-import backup creation, entity type validation, and user acknowledgment required
3. **Given** the user confirms restore, **When** the restore completes with valid complete backup data, **Then** all data is restored and the system displays a success message
4. **Given** a backup file is missing required entity types (e.g., Categories missing), **When** the user attempts to restore, **Then** import validation fails and displays error: "Import file incomplete: missing Categories. Restore from complete backup."

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

**FR-001**: System MUST display a first-run setup wizard that captures organization name, annual membership fee, attendance fee per rehearsal, and membership renewal month (1-12) and persists these to the Settings database record. Setup wizard MUST NOT automatically apply any fees; it initializes the database schema and Settings only. Fees are created only when: (1) annual fee application is manually triggered by coordinator, or (2) attendance is recorded for members.

**FR-002**: System MUST provide a Members module enabling user to create, edit, list, and filter members by Active/Inactive status; members MUST include: name (required), street address (required), phone (optional), email (optional), join date (required), and optional date of birth fields. System MUST validate email and phone formats when provided. When a new member is created via the "Add Member" form, the default Status is Active with ActivateDate set to today. Coordinator may immediately inactivate a member if needed.

**FR-002a**: System MUST calculate and display member age in years based on date of birth. Age MUST only display when date of birth is provided; the age field MUST not be visible if date of birth is empty/null. Age calculation MUST be performed server-side to ensure consistency using formula: `floor((today - DOB) / 365.25)`. System MUST validate: (1) date of birth MUST be in the past (before today; today's date rejected), (2) date of birth MUST be within 150 years of today (configurable in Settings), (3) calculated age MUST be >= configured Minimum Member Age in Settings (default 0, no minimum). Validation error messages MUST be specific: "Date of birth cannot be today or in the future", "Date of birth must be within 150 years", "Member age ({age} years) must be at least {minimum_age} years old".

**FR-003**: System MUST support member lifecycle through two orthogonal mechanisms: (1) **Status field** with two values: Active (member participates in events; fees apply) and Inactive (member exists but does not participate; no fees accrue). Marking a member Inactive MUST NOT set soft-delete fields; Status is a participation indicator only. (2) **Soft-Delete (Archival)** via IsDeleted/DeletedAt/DeletedBy fields: when a member is archived (soft-deleted), IsDeleted is set to true and the member is hidden from default views but remains in database for historical reporting. Both Active and Inactive members can be archived. This separates participation status (Status field) from data visibility/deletion (IsDeleted flag) per Constitution §3.5. Queries filtering non-archived members use: `WHERE IsDeleted=false`; queries filtering active participants use: `WHERE Status='Active' AND IsDeleted=false`.

**FR-004**: System MUST automatically apply annual membership fees to all active members (Status='Active') at the configured renewal month, skipping: (1) inactive members (Status='Inactive'), and (2) active members with existing unpaid annual fees for the current year. Display a batch processing confirmation dialog showing the number of members to be charged.

**FR-005**: System MUST provide a Rehearsals module enabling the user to schedule rehearsals (date, time, optional notes) and record attendance. Recording attendance for active members (Status='Active') MUST automatically create attendance fee records with payment status defaulting to **PAID** (coordinator may override to unpaid if needed). Recording attendance for inactive members (Status='Inactive') is allowed for historical tracking but MUST NOT create fee records. If attendance flag is subsequently cleared for a member on a specific rehearsal, the corresponding attendance fee for that rehearsal MUST be automatically removed (soft-deleted with GL reversing entries). Once attendance is recorded, historical data is preserved and immutable until explicitly cleared.

**FR-006**: System MUST provide an Events module enabling the user to schedule performances/events (date, event type, optional notes) and record participation. Event types MUST be configurable in Settings with defaults: Performance, Eisteddfod, Fund raiser, Promotional.

**FR-007**: System MUST track and display historical attendance rate (members present / members active on that date × 100%) for the most recent past rehearsal and participation rate for the most recent past event. "Members active on that date" is calculated using the member's historical Status as of the event date using effective dates: `WHERE Status='Active' AND ActivateDate <= event_date AND (InactivateDate IS NULL OR InactivateDate > event_date) AND IsDeleted=false`. For historical rate calculations, use the member's actual status **as of the event date**, not their current status. If a member was active on rehearsal date X but later archived, they are still counted in the denominator for that historical rehearsal (event-date-based calculation). Archival only affects future rate calculations (for events after archival date). Archived members are excluded from the participation rate denominator only for events occurring after their archival date. This ensures historical accuracy for reporting and audit trail integrity.

**FR-008**: System MUST track outstanding balances combining annual membership fees and per-rehearsal attendance fees, displaying total outstanding balance on the Finance tile with muted Green for positive balance (income > expenses) and muted Red for negative balance (expenses > income).

**FR-009**: System MUST support custom income/expense categories in the Settings module with create/edit/archive/restore/reorder operations. Archiving MUST be prevented if any transaction (including soft-deleted transactions) references the category.

**FR-010**: System MUST implement a dashboard with four core tiles (Members, Rehearsals, Events, Finance) and support plugin-driven dashboard tile registration without core modifications.

**FR-011**: System MUST display dashboard tiles progressively and degrade gracefully if a tile provider fails or is slow; failed providers MUST log structured errors and not block dashboard render.

**FR-012**: System MUST provide Backup functionality exporting all application data (members, rehearsals, events, financial records (fees, payments, GL transactions, categories), settings (organization config, theme preference), committee membership history, and audit trail logs) in Protocol Buffers (protobuf) binary format with metadata including `schemaVersion`, generation timestamp, and complete entities map. Protobuf provides compact serialization, fast deserialization performance, and forward-compatible schema versioning for long-term data preservation.

**FR-013**: System MUST require a pre-import backup checkpoint and explicit user confirmation before any import writes data; import MUST be atomic (validate full payload first, then commit all changes in one transaction).

**FR-014**: System MUST validate import schema version and entity completeness. Import MUST reject unsupported major versions with clear upgrade guidance. Import MUST ALSO verify all required entity types are present in the import source (Members, Rehearsals, Events, Fees, Payments, Transactions, Categories, Settings, CommitteeMembership, AuditTrail). If any required entity type is missing, reject the import with error: "Import file incomplete: missing {entity_type}. Restore from complete backup."

**FR-015**: System MUST use non-destructive import mode (upsert): existing records are updated by primary key/natural key matching, missing records are inserted, and local records not present in the source remain unchanged. However, ALL required entity types MUST be present in the import source (per FR-014); selective data exports are not supported. Upon successful validation, import MUST be atomic (all-or-nothing within a single transaction).

**FR-016**: System MUST support payment recording that creates GL transaction pairs for accounting integrity. When payment is recorded with date, amount, payment method (Cash, Check, Card, etc.), payment type (Annual/Attendance/Other), and optional notes, the system MUST: (1) Create Payment record for audit trail with fields: date, amount, payment method, payment type, category, member reference, notes; (2) Create two GL Transaction records: Debit=$amount on CashReceived account + Credit=$amount on MemberReceivable account, linked to Payment record. Payments MUST reduce outstanding balances using FIFO (First-In-First-Out) allocation: oldest unpaid fees satisfied first (e.g., 2024 annual fee before 2025 annual fee before 2025 attendance fees). System MUST automatically calculate GL transaction dates and link Payment to created Transactions for audit trail. PaymentType field is metadata and distinct from GL Category (used for reporting/filtering).

**FR-017**: System MUST allow Notes field editing on Payment records with audit trail logging (who changed what when); Amount, Date, PaymentMethod, PaymentType, and Category fields MUST remain locked after creation. Payment entity includes `UpdatedAt` timestamp that updates ONLY when Notes changes; if `UpdatedAt` differs from `CreatedAt`, only Notes was modified. Database constraint or application validation enforces field-level immutability on Amount, Date, PaymentMethod, PaymentType, Category (reject update attempts on these fields).

**FR-018**: System MUST provide a Settings module with tabs: General Settings (organization name, annual fee, attendance fee, membership renewal month, committee renewal month, date of birth maximum age range, minimum member age), Categories, Event Types, Backup, and Restore. Plugin modules may contribute additional Settings tabs via tab provider contract. General Settings MUST include fields: (1) Organization Name, (2) Annual Membership Fee, (3) Attendance Fee per Rehearsal, (4) Membership Renewal Month (1-12, for annual fee application), (5) Committee Renewal Month (1-12, default 1/January, for annual committee status reset), (6) Maximum Age Range (in years, default 150), (7) Minimum Member Age (in years, default 0, optional enforcement).

**FR-019**: System MUST support dark and light theme toggle with persisted preference in the Settings database record; all UI surfaces MUST comply with WCAG AA contrast requirements in both themes.

**FR-020**: System MUST use Bootstrap 5 card styling with rounded corners and compact spacing; pastel/muted color palette (HSL lightness 60–80%, saturation <50%) for UI surfaces and accents; dark-theme variants may adjust absolute lightness while maintaining WCAG contrast compliance.

**FR-021**: System MUST auto-create the `Plugins` directory at application root on startup if missing; plugin discovery MUST scan the `Plugins` directory for assemblies; gracefully handle read-only filesystem errors.

**FR-022**: System MUST implement audit trail logging (who, what, when) for all data modifications and retain logs for 12 months; purge expired logs on application startup only. If startup purge fails, log structured error and continue startup.

**FR-023**: System MUST maintain member activation/inactivation effective dates to enable historical active-member count computation based on the rehearsal/event date (not current date).

**FR-024**: System MUST implement automatic member reactivation debt forgiveness using GL write-offs with audit trail. When a coordinator marks an Inactive member as Active (reactivation), the system MUST automatically (no coordinator choice): (1) Identify ALL outstanding fees from prior years; (2) Create GL reversing Transaction pairs for each outstanding fee (debit MemberReceivable, credit BadDebtExpense/WriteOff category) to zero-out prior balances; (3) Log audit trail entries for debt forgiveness; (4) Reset member's payable balance to $0.00. Reactivated member has clean financial slate. Current-year membership fee remains payable (will be applied per annual fee application process). Prior unpaid fees are permanently forgiven with full GL audit trails preserving complete history. Original Fee records remain in database immutable (no soft-delete); GL reversing transactions provide audit trail of forgiveness. **NOTE**: Reactivation affects **only financial balances (fees)**; committee history is unaffected and remains independent (see FR-028/FR-029).

**FR-025**: System MUST store payment method as required field on Payment records (enum: Cash, Check, Card, Electronic Transfer, Other), defaulting to `Cash` when not explicitly selected. System MUST store payment type as required field on Payment records (enum: Annual, Attendance, Other). PaymentType is metadata used for reporting and filtering; it is separate from GL Category (which is used for accounting categorization). Both PaymentMethod and PaymentType fields are immutable after payment creation (for audit integrity).

**FR-026**: System MUST define and use custom exceptions for domain/application/infrastructure failures; raw framework exceptions MUST be translated before crossing boundaries.

**FR-027**: System MUST track committee membership for each member on a per-calendar-year basis. Member edit form MUST include a "Committee Member" checkbox and a "Position" text field (max 100 characters). If checkbox is marked, Position field MUST be required. If checkbox is unchecked, no position is recorded.

**FR-028**: System MUST preserve committee membership history across calendar years. Each year's committee assignment is independent; members can have different positions in different years or no position in some years.

**FR-029**: System MUST display committee history on the member detail screen showing all years in which the member served on committee with their corresponding positions. Current year MUST be visually distinct from historical records using the following mechanism: Current year entry is rendered in bold with a small "Current" badge (e.g., "**2026 (Current) - Treasurer**"), while historical years display in normal text weight (e.g., "2025 - Secretary"). This provides clear visual distinction with accessibility compliance (text + visual indicator readable by screen readers). Members with no committee history MUST have no committee section displayed.

**FR-030**: System MUST include automated tests proving coverage of all reachable code paths for committee membership operations (add/update/remove/query); all committee-related workflows MUST have corresponding UI integration tests.

**FR-031**: System MUST require current year committee membership to be reassigned each year based on a configurable "Committee Renewal Month" setting (distinct from membership renewal month). Committee renewal month is user-configurable in Settings (range 1-12, default January=1). On application startup, system uses system local time (DateTime.Now) to compare current calendar month/year against `Settings.LastCommitteeResetYear`. If (CurrentMonth >= CommitteeRenewalMonth AND LastResetYear < CurrentYear), system automatically invokes CommitteeAnnualResetService synchronously before dashboard displays, clearing all members' current-year committee status (set to not-committee), preserving prior years' records as read-only history, and updating `LastCommitteeResetYear = CurrentYear`. Idempotency is guaranteed by the LastResetYear field: if the application is restarted multiple times on the same calendar date after the first reset, the condition (LastResetYear < CurrentYear) evaluates to false, preventing duplicate resets. This ensures annual governance review happens once per committee year on startup, and prevents stale privilege carryover. Coordinators MUST explicitly re-enter committee members and positions for the new year through the member edit form.

**FR-032**: System MUST implement accounting compliance using a **General Ledger (GL) paired transaction model** with simple account structure: (1) **Asset Accounts**: Fixed GL#0100 (Cash), GL#0101 (MemberReceivable); (2) **Revenue Accounts**: Income categories assigned GL#10xx range (GL#1000 for first income category, GL#1001 for second, etc.); (3) **Expense Accounts**: Expense categories assigned GL#20xx range (GL#2000 for first expense category, GL#2001 for second, etc.); (4) **Writeoff Account**: Fixed GL#9900 (BadDebtExpense for reactivation debt forgiveness). All financial events MUST be recorded as exactly TWO Transaction records (one debit, one credit) with equal amounts. GL account is **derived deterministically from Category type** — each Category has an auto-assigned GL account number based on its type (type is set at category creation): Income categories → GL#10xx sequential; Expense categories → GL#20xx sequential. GL accounts are assigned in **creation order (by CreatedAt timestamp, ascending)**; the first income category created receives GL#1000, the second GL#1001, etc. This ordering is deterministic and stable across backups and restores. Each Transaction MUST include: transaction date (required), debit or credit amount (decimal, 2+ places), category (required, FK to Category, which implies GL account), member reference (when applicable), and description/notes (optional). All transactions MUST be categorized as either income or expense per user-defined categories. When coordinator creates a new category, system auto-assigns a unique GL account number without user input using GLAccountAssignmentService (sequential numbering within type range, assigned in creation order).

**FR-033**: System MUST generate an Income Statement report showing revenue (income categories with amounts) and expenses (expense categories with amounts) organized by category with subtotals for each section and net income/loss calculation. Report MUST allow date range filtering. Default date range MUST be current calendar year (January 1 - December 31); coordinator can select different date ranges via report filters.

**FR-034**: System MUST generate a Trial Balance report displaying all accounts organized in three sections: (1) Asset Accounts (Cash, MemberReceivable); (2) Income Accounts (user-defined revenue categories); (3) Expense Accounts (user-defined expense categories). Each account MUST display: Account Name | Debit Amount | Credit Amount (each account row shows its balance in Debit or Credit column as appropriate, zero in the other). Subtotals MUST be shown after each section. Grand total row at end MUST show: Total Debits | Total Credits. The report MUST verify that Total Debits = Total Credits (within 0.01 for decimal precision); if totals do not equal, the system MUST reject report generation and display error: "GL Balance Verification Failed: Total Debits ($X.XX) ≠ Total Credits ($Y.YY). Please review and correct GL entries." Report MUST include date as of which the trial balance is calculated. Default date range MUST be current calendar year (January 1 - December 31); coordinator can select different date ranges via report filters.

**FR-035**: System MUST generate an Account Register report showing all transactions in chronological order by date within selected categories. Each transaction MUST display: date, description, category, debit amount (for expenses), credit amount (for income), and running balance. Running balance MUST update correctly after each transaction. Default date range MUST be current calendar year (January 1 - December 31); coordinator can select different date ranges via report filters.

**FR-036**: System MUST generate a Member Account Summary report showing each member (including archived members for historical completeness) with opening balance (beginning of period), all transactions affecting that member during the period (fees, payments, adjustments), and closing balance (end of period). Outstanding fees MUST be aged showing current, 30-day, 60-day, and 90+ day categories. Archived members' historical transaction data MUST be included to enable complete financial analysis and accurate aging calculations. Default date range MUST be current calendar year (January 1 - December 31); coordinator can select different date ranges via report filters. Aging calculation uses current date (today) as reference point for determining days past due.

**FR-037**: System MUST provide print capability for all financial reports allowing users to print to PDF or physical printer. Printed reports MUST include: title, date range, generation date, all column headers, all data rows with proper alignment, subtotals, and grand totals. Printed format MUST be professional and clearly readable.

**FR-038**: System MUST ensure all financial transaction amounts are stored with proper precision (minimum 2 decimal places); all calculations MUST maintain precision without rounding errors throughout arithmetic operations.

**FR-039**: System MUST enforce **accounting transaction integrity using paired GL entries**: every debit MUST have exactly one corresponding credit entry, and vice versa. All Transactions on any given date MUST sum to zero (total debits = total credits). GL accounts are derived from Category type at transaction record creation time; no runtime lookup needed. When a payment is recorded, the system MUST create two Transaction records: (1) Debit=$amount on Cash account (Category='CashReceived' or similar Asset category, GL effect: organization receives cash); (2) Credit=$amount on MemberReceivable account (Category='MemberReceivable' or similar Asset category, GL effect: member's outstanding balance decreases). When a fee is assigned to a member, the system MUST create two Transaction records: (1) Debit=$amount on MemberReceivable (Category='MemberReceivable', GL effect: member owes organization); (2) Credit=$amount on the applicable Income category (Category FK to user-defined Income category, GL effect: organization recognizes income). System MUST validate GL balance (sum of all debits = sum of all credits) before generating reports or ending transactions.

**FR-040**: System MUST include automated tests proving coverage of all reachable code paths for Finance module operations (payments, reporting, categorization); all financial workflows MUST have corresponding UI integration tests verifying report accuracy.

**FR-041**: System MUST provide CSV export capability for all financial reports (Income Statement, Trial Balance, Account Register, Member Account Summary). Exported CSV files MUST include all column headers as the first row and all data rows with proper CSV formatting (comma-separated values, quote-escaping for special characters, comma-escaping for field values containing commas).

**FR-042**: System MUST implement a centralized, extensible base data access layer (DAL) that owns all MVP module data access (Members, Rehearsals, Events, Finance, Settings, Categories, Audit Trail). The DAL MUST use Entity Framework Core with SQLite and provide repository contracts for each entity type. DAL MUST support schema migration-based extensibility allowing plugins to define their own entities and create corresponding database tables through code-first migrations without modifying core DAL code.

**FR-043**: System MUST provide a plugin data access contract allowing plugins to register custom entity types and repository implementations with the base DAL. Plugins MUST be able to define new database entities, create tables, and provide repository implementations for their own data without modifying core MVP module data access. Plugin data MUST be persisted to the same SQLite database with automatic schema migration support.

**FR-044**: System MUST include automated tests proving coverage of all reachable code paths for data access layer operations (CRUD, transactions, migrations); all data persistence workflows MUST have corresponding integration tests verifying data integrity and schema correctness.

**FR-045**: System MUST implement a Reports root menu item in the main navigation that aggregates all available reports from all modules (MVP and plugins). Reports MUST be organized by module with submenus for each module's reports. Reports menu structure: (1) MVP module sections (Members section, Finance section) with reports nested under each; (2) Plugin sections (alphabetically by plugin module name) with each plugin module as a section header and its reports nested under it. Example structure: "Members" > "Member List", "Committee Report"; "Finance" > "Income Statement"; "Attendance Analytics" > "Monthly Summary" (plugin). Reports menu organization follows module order: Members, Finance, then plugins alphabetically.

**FR-046**: System MUST provide a report provider contract (`IReportProvider`) allowing MVP modules and plugins to register custom reports. Each report provider MUST specify: report name, report ID, module name, display order within module, and a method to generate report data. Each module (MVP or plugin) is responsible for: (1) specifying its module name (e.g., "Members", "Finance", "Attendance Analytics"), (2) providing all its reports with unique report IDs and display orders. The Reports infrastructure auto-discovers providers, organizes reports into module sections in the Reports menu, and renders each module's section as a submenu. Module providers MUST generate report data; the report infrastructure MUST handle display, printing, and exporting.

**FR-047**: System MUST implement a shared common report viewer component that all modules use for displaying reports. Report generation MUST be synchronous; when user selects a report, the system blocks UI while report data is generated, then displays the report in the common viewer. The report viewer MUST support: displaying report data on screen, printing to PDF through a print dialog, exporting to CSV, and displaying a "Generating report..." loading message while report data is being prepared. Report data is regenerated fresh each time the user selects the report, prints, or exports (no caching between actions). If report generation exceeds 5 seconds, the loading message MUST include an option to cancel and return to the Reports menu. The viewer MUST have consistent UI/UX across all modules.

**FR-048**: System MUST support report data generation abstraction where each module provides the report data (structured as rows/columns with headers) and the common viewer handles rendering, print-to-PDF, and CSV export. Modules MUST NOT implement their own print or export logic; all modules MUST use the common infrastructure.

**FR-049**: System MUST include error handling for report providers: if a report provider fails to register or fails to generate report data, the system MUST log a structured error, skip the failed report, continue rendering other reports in the Reports menu, and display a user-friendly error message in the report viewer if the user attempts to view the failed report.

**FR-050**: System MUST support the following MVP module reports: Member module reports (Member List, Committee Report) and Finance module reports (Income Statement, Trial Balance, Account Register, Member Account Summary). All reports MUST be accessible through the Reports menu.

**FR-051**: System MUST implement the Member List report displaying member details: Name, Street Address, Phone, Email, Join Date, Calculated Age (if date of birth provided), and Status. The Member List report MUST include a configurable member-status filter allowing coordinators to select: (1) "Active" (default) - currently-active members only; (2) "Inactive" - inactive members only; (3) "Archived" - archived members only; (4) "All" - all members regardless of status. Filter state MUST be persistent within a report-viewing session (same filter applied if user prints or exports). Filter resets to "Active" when user closes and reopens the report.

**FR-052**: System MUST implement the Committee Report with a configurable member-status filter. The Committee Report MUST display committee members and their assignments organized by year (most recent first). The report MUST include a filter control allowing coordinators to select: (1) "Active Only" (default) - displays currently-active members with their committee assignments; (2) "Archived Only" - displays archived members with their historical committee assignments; (3) "All" - displays combined view of both active and archived members with all committee history. Filter state MUST be persistent within a report-viewing session (same filter applied if user prints or exports). Filter reset to "Active Only" when user closes and reopens the report. Each committee entry MUST display: Member Name | Year | Position.

**FR-053**: System MUST include automated tests proving coverage of all reachable code paths for report provider registration, report data generation, common report viewer rendering, print-to-PDF, and CSV export functionality; all report workflows MUST have corresponding UI integration tests.

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

**NFR-019**: **Report Generation Performance**: Report generation MUST be synchronous and blocking; user selects report, waits for data generation and display within common report viewer. No concurrent report generation. No caching between report selection, print, or export actions; each action triggers fresh data generation. Performance target: reports MUST generate and display within 5 seconds for typical organizations (≤500 members, ≤3 years of historical data). For longer-running reports, UI MUST display "Generating report..." message with optional cancel button to allow user to return to menu.

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
- Unique identifier, name, street address (required), phone (optional), email (optional), join date, optional date of birth
- **Age**: Calculated property derived from DateOfBirth; only displayed if DateOfBirth is provided; not persisted separately; calculated server-side for consistency
- **Status**: Enum {Active, Inactive} — independent from archive state; inactivation changes Status only
- **Soft-Delete Fields**: IsDeleted (boolean), DeletedAt (DateTime?), DeletedBy (string?) — set only on explicit archival, NOT on inactivation
- **Effective Dates**: ActivateDate (DateTime, nullable) set when member transitions Inactive→Active; InactivateDate (DateTime, nullable) set when member transitions Active→Inactive. Both recorded as system date (today) when status change occurs. Effective dates are immutable (not editable). Used for historical active-member computation in attendance/participation rate calculations.
- Relationships: payments, GL transactions (via member reference), attendance records, committee memberships, fees
- **Validation**: Email format validated if provided; phone format validated if provided; DateOfBirth validated to not be in future and within reasonable range (≤150 years ago)
- **Query Pattern**: Active members = `Status='Active' AND IsDeleted=false`; Inactive members = `Status='Inactive' AND IsDeleted=false`; Archived members = `IsDeleted=true`. Historical active-member status AS OF event date = `Status='Active' AND ActivateDate <= event_date AND (InactivateDate IS NULL OR InactivateDate > event_date) AND IsDeleted=false` (archived members always excluded)

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
- **Relationships**: Links to GL Transaction pairs (one Payment may create multiple Transaction pairs via FIFO allocation); audit trail entries
- **Purpose**: Metadata and audit trail for WHO paid WHAT. Actual GL entries (debit/credit pairs) are recorded in Transaction table. Payment.Amount must equal sum of related Transaction.CreditAmount values.

**Fee** (Immutable Financial Obligation Record):
- Unique identifier, member reference (FK), fee type enum {Annual, Attendance, Other}
- Amount (decimal, 2+ places)
- Fee Date (date when fee was generated/applies to) — for annual fees = Jan 1 of year; for attendance fees = rehearsal/event date
- DueDate (date by which fee should be paid) — for annual fees defaults to Dec 31 of year; for attendance fees = event date; configurable per Settings
- CreatedAt timestamp (creation timestamp for FIFO tiebreaker ordering)
- IsDeleted soft-delete flag (NEVER set to true per Constitution §3.4; fees are immutable and permanent)
- **Immutability**: Fee Amount, Date, Type, DueDate locked after creation; no edits permitted in UI. New GL reversals and adjustments create new GL transactions; original fees remain unchanged.
- Relationships: member, GL transaction(s) representing the fee entry and any subsequent reversals/write-offs
- **Purpose**: Immutable, permanent record of financial obligations. Fees are created when earned (annual fee application, attendance recording) and remain unchanged. Payments reduce member liability via GL transactions, not by modifying Fee records. Write-offs also use GL reversals, not Fee deletion.
- **Query Pattern**: Outstanding fees for member X = `SELECT * FROM Fee WHERE Member=X AND IsDeleted=false AND Amount > sum(GL Credits for this Fee)` (fees less paid amounts)

**Transaction** (General Ledger Entry — Immutable Paired Entries):
- Unique identifier, transaction date (required), category (required, FK to Category)
- **Debit Amount** (decimal, 2+ places) — for expenses/receivables
- **Credit Amount** (decimal, 2+ places) — for income/cash
- **GL Account** (derived deterministically from Category type at record creation) — Income categories → Revenue GL; Expense categories → Expense GL; Special categories (Cash, MemberReceivable) → Asset GL. No runtime lookup needed; GL account is implied.
- Member reference (FK, when applicable) — e.g., annual fee transaction linked to member
- Payment reference (FK, when applicable) — links payment to GL entries it created
- Description/notes
- Created and modified timestamps
- **Soft-delete flag (IsDeleted)**: NEVER TRUE for financial records per Constitution §3.4 (Financial Transactions exempt). Transaction is immutable and permanent.
- Relationships: category, member, payment (metadata link), audit trail entries
- **Paired Entry Constraint**: Every financial event creates exactly TWO Transaction records: (1) Debit entry + (2) Credit entry. Debit.Amount = Credit.Amount. Total debits must equal total credits on any query date.
- **Example (Member pays $50 cash toward membership fee)**:
  - Transaction 1: Date=2026-05-15, Debit=$50, Credit=$0, Category='CashReceived' (Asset GL), Member=John, Payment.id=123
  - Transaction 2: Date=2026-05-15, Debit=$0, Credit=$50, Category='MemberReceivable' (Asset GL), Member=John, Payment.id=123

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
- User identifier — In MVP, recorded as fixed value "system" (represents the implicit single coordinator user); when multi-user authentication is added in Phase 2+, this field will contain the authenticated user ID
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

- Q: Should report generation be synchronous (UI blocks while report generates) or asynchronous (background generation with polling/notification)? → A: Synchronous, blocking report generation. User clicks report menu item, waits for data generation and display, then can print or export. No concurrent report generation. Simpler implementation aligned with MVP scope. Performance target: reports generate within 5 seconds for typical organizations (≤500 members, ≤3 years of data). If reports exceed 5 seconds, show "Generating report..." message with cancel option.

- Q: How should archived members affect historical rate calculations for attendance and participation? When a member is archived, should historical rates retroactively exclude them? → A: No retroactive exclusion. Use member's actual status **as of the event date** for all historical calculations. For a rehearsal on 2025-06-15, if member X was active on that date, they count in the denominator even if later archived in 2026. Archival only affects future rate calculations (events after archival date). This preserves historical accuracy for reporting (Member Account Summary aging must include archived members' full transaction history), maintains audit trail integrity, and aligns with constitutional principles of member/financial data preservation.

- Q: When categories are assigned GL account numbers, in what order are they assigned within their type range (e.g., GL#1000, GL#1001 for income)? → A: GL accounts are assigned in **creation order by CreatedAt timestamp (ascending)**. The first income category created gets GL#1000, the second gets GL#1001, etc. This ensures deterministic, reproducible assignments that remain stable across backups, restores, and plugin discovery cycles. GL account numbers are immutable once assigned.

- Q: What timezone should the committee annual reset check use on application startup? Should UTC or local time be used, and what prevents the reset from running multiple times on the same calendar date? → A: Use **system local time (DateTime.Now)**. On app startup, check if (CurrentMonth ≥ CommitteeRenewalMonth) AND (LastResetYear < CurrentYear). If true, invoke CommitteeAnnualResetService synchronously before dashboard displays. Idempotency is guaranteed by the LastResetYear field: if the app is restarted multiple times on the same calendar date, LastResetYear is already equal to CurrentYear (after first reset), so the condition fails and reset does not repeat. This approach is simple for MVP scope and aligns with user expectations (local midnight = their local midnight, not UTC).

- Q: How should Member Status be formally defined? Should "Archived" be a Status value, or should Archived be determined by the soft-delete flag (IsDeleted)? → A: **Status enum has 2 values only: Active | Inactive**. Do NOT create a Status=Archived value. Archived is a separate concept determined purely by the IsDeleted flag. Active members participate in events and incur fees. Inactive members exist but do not participate; no fees accrue. Both Active and Inactive members can have IsDeleted=false (normal) or IsDeleted=true (archived/soft-deleted). This separates concerns per Constitution §3.5: Status field manages participation lifecycle, IsDeleted flag manages data deletion/visibility. Queries use: `WHERE Status='Active' AND IsDeleted=false` for active members, or `WHERE IsDeleted=false` for all non-archived members (Active or Inactive).

- Q: When a member is reactivated (from Inactive to Active), should their committee history be affected? Does reactivation clear committee records, or are they independent? → A: **Committee history is completely independent of reactivation**. Reactivation affects **only financial balances** (fees are forgiven via GL write-offs per FR-024); committee history is unaffected. Prior-year committee records are preserved. Current-year committee status is reset per annual reset logic (FR-031) on the next app startup after the committee renewal month arrives, **regardless of whether the member was reactivated**. This preserves audit trails and separates concerns: financial lifecycle (fees/reactivation) vs. governance lifecycle (committee). Reactivation timing does not change committee reset timing.

- Q: When a member has multiple outstanding fees (2024 annual, 2025 annual, 2025 attendance) and makes a single payment, which fees should be reduced first? → A: FIFO (First-In-First-Out) allocation. Oldest unpaid fees are satisfied first (chronological fee creation order: 2024 annual → 2025 annual → 2025 attendance). This is standard accounting practice, enables proper aging analysis in Member Account Summary reports, and ensures consistent reduction of oldest debt first.

- Q: Should archived members appear in current metrics (attendance/participation rates) and historical reports? → A: Exclude from current, include in historical. Archived members are excluded from current attendance/participation rate calculations (dashboard tiles) to show only active group metrics. However, archived members' historical transaction data MUST be included in aged reports (Member Account Summary) to preserve complete financial history, enable accurate aging analysis, and comply with data preservation requirements.

- Q: How should the system represent member lifecycle states (Active/Inactive/Archived) in the database? → A: **Dual Model**: Member has two independent attributes: (1) `Status` enum field = {Active, Inactive} representing participation state; (2) `IsDeleted` boolean field (+ `DeletedAt`, `DeletedBy` timestamps) representing archival/soft-delete state. When user inactivates a member, set `Status='Inactive'` but keep `IsDeleted=false`. When user archives a member, set `IsDeleted=true` (do NOT change Status). Reactivating an archived member restores `IsDeleted=false`. This design separates administrative state (Active/Inactive) from data preservation state (archived), enabling proper filtering and audit trails. Query patterns: Active members = `WHERE Status='Active' AND IsDeleted=false`; Inactive members = `WHERE Status='Inactive' AND IsDeleted=false`; Archived members = `WHERE IsDeleted=true` (status ignored).

- Q: How should the system implement double-entry accounting for financial transactions? → A: **Paired GL Entries**: Every financial event creates exactly TWO `Transaction` records: one debit entry and one credit entry with equal amounts. The `Transaction` table is the general ledger; this is the source-of-truth for all financial data. Example: When a member pays $50 toward their membership fee, the system creates: (1) Transaction with Debit=$50 on GLAccount='CashReceived'; (2) Transaction with Credit=$50 on GLAccount='MemberReceivable'. Both linked to the same Payment.id for audit trail. The `Payment` entity is metadata only—it records WHO paid WHAT and WHEN, but the actual GL is in the `Transaction` table. Member balances are calculated by querying Transactions: `sum(debits where member=X) - sum(credits where member=X)`. Trial Balance reports verify `sum(all debits) = sum(all credits)`. Income Statement sums by category. This ensures accounting integrity and enables proper aging analysis via GL date ordering.

- Q: How should payment allocation work when a member has multiple outstanding fees and makes a partial payment? → A: **GL-Centric Allocation (FIFO via GL)**: When payment recorded, system creates GL Transaction pairs in chronological order of fees (FIFO). Allocation emerges naturally from GL structure—no separate PaymentAllocation table needed. Example: Member owes $100 (2024 annual) + $100 (2025 annual) + $50 (attendance) = $250 total, and pays $200. System creates GL Transaction pairs for the $200 payment in chronological GL order. When Member Account Summary report generates, it reconstructs member balance by walking GL Transactions in date order (FIFO), showing: (1) 2024 annual $100 paid; (2) 2025 annual $100 paid; (3) 2025 attendance $0 paid (payment exhausted). Aging calculation uses GL transaction dates. This model is immutable per Constitution §3.4 (GL is permanent; never deleted). To reverse/correct payments, create reversing GL entries, not delete.

- Q: When a member has multiple unpaid fees with the same date field (e.g., two 2024 annual fees both with date 2024-01-01 but created on different days), what FIFO tiebreaker should apply? → A: Use transaction creation timestamp as tiebreaker (Fee.CreatedAt). When two fees share the same date, the fee created first (earlier CreatedAt timestamp) is satisfied first in payment allocation. This ensures deterministic, non-arbitrary FIFO ordering aligned with standard accounting practice of GL entry sequence.

- Q: Should annual membership fee application be automatic (system auto-applies on day 1 of renewal month) or manual (user clicks "Apply Annual Fees" button)? → A: Manual trigger via "Apply Annual Fees" button in Finance module. System does NOT automatically apply fees on month arrival. User can click the button any time during or after the configured renewal month. Button is enabled only if the renewal month has arrived (current month >= configured renewal month) OR if user is re-applying after a prior application. This gives coordinators explicit control, reduces charging risks, and aligns with MVP single-user design (no background jobs).

- Q: What are the exact HSL color values for the muted Green and muted Red used to display positive and negative outstanding balances on the Finance tile? → A: **Muted Green** (positive balance): HSL(120°, 35%, 70%). **Muted Red** (negative balance): HSL(0°, 35%, 70%). Balance = $0.00 displays in neutral gray: HSL(0°, 0%, 60%). These values satisfy the "muted" requirement (saturation <50%, lightness 60–80%) and provide consistent, non-arbitrary color choices across all deployments. If balance is exactly $0.00, display in neutral gray instead of green or red.

- Q: When calculating attendance/participation rates for past rehearsals/events, should the system use member's current Status or their Status AS OF the rehearsal/event date (based on effective dates)? → A: Use member Status AS OF the rehearsal/event date using effective dates (Option B). For a rehearsal on 2026-02-01, include a member in the denominator if Status=Active on that date, even if the member is now Inactive. Query logic: `WHERE Status='Active' AND ActivateDate <= rehearsal_date AND (InactivateDate IS NULL OR InactivateDate > rehearsal_date)`. This ensures historical attendance rates reflect the actual membership on the date the event occurred, not current status. Archived members (IsDeleted=true) are ALWAYS excluded from the denominator, regardless of effective dates.

- Q: What are the exact date of birth validation rules, particularly for edge cases like today's date and the "reasonable range" upper bound? → A: **Option C with modification**: DOB MUST be < today (reject today's date; age 0 not allowed). Range is configurable in Settings (default 150 years). Age calculation: `floor((today - DOB) / 365.25)` using current date, updates daily at midnight. **New requirement**: Add optional "Minimum Member Age" field in Settings > General Settings tab (default: 0, no minimum). If set, system rejects DOB entries that would result in age < minimum (e.g., if Minimum Age = 18 and today is 2026-05-15, reject DOBs after 2008-05-15). Validation error message: "Member age ({calculated_age}) must be at least {minimum_age} years old."

- Q: What is the GL account structure for double-entry accounting? → A: **Simple GL Account Structure** with three account categories: (1) **Asset Accounts**: Cash, MemberReceivable; (2) **Revenue Accounts**: mapped from user-defined income categories; (3) **Expense Accounts**: mapped from user-defined expense categories. Each `Transaction` record implies its GL Account based on (Category + TransactionType). Example: When a member pays $50 in cash, create Debit=$50 on CashReceived (Asset) and Credit=$50 on MemberReceivable (Asset). When annual fee is applied, create Debit=$50 on MemberReceivable (Asset) and Credit=$50 on the applicable Income category (Revenue). When an expense is recorded, create Debit=$50 on the expense category (Expense) and Credit=$50 on Cash (Asset). This minimalist approach maintains accounting integrity while MVP scope stays simple. Chart-of-accounts complexity deferred to Phase 2+.

- Q: How should committee membership transition when the calendar year advances? → A: **Clear and Re-enter Model** (Option A). When the calendar year advances (Jan 1 midnight), the system: (1) Preserves all prior-year committee records as read-only history in the Committee History section; (2) Clears current-year committee flags (set Committee Status = "Not Committee" for all members for new year); (3) Coordinators must explicitly re-enter committee members and positions for the new year by editing member profiles. This ensures conscious, intentional assignments annually, prevents stale privilege carryover, and guarantees governance review each year.

- Q: Should inactive members be charged annual membership fees and attendance fees? → A: **Option A: Exclude Inactive from All Fee Charging**. When applying annual membership fees, skip all members where Status='Inactive'. When recording rehearsal attendance, allow attendance entry for inactive members (for historical completeness and potential reactivation scenarios), but do NOT automatically create unpaid attendance fee records for inactive members. Inactive members retain existing outstanding fees from when they were active and may pay to clear them, but they incur NO new charges. This aligns with the semantic meaning of "Inactive" (temporarily not participating), prevents unintended billing, and keeps financial ledger clean.

- Q: What entities and data should be included in backup/restore? What import validation rules apply? → A: **Option A: Comprehensive Backup with Strict Import Validation**. Backup MUST include ALL operational entities: (1) Members + all attributes; (2) Rehearsals + attendance records; (3) Events + participation records; (4) Financial records (Fees, Payments, GL Transactions, Categories); (5) Settings (organization config, theme preference); (6) Committee membership history; (7) Audit trail logs (all retained records). Import validation MUST verify that ALL entity types are present in the import source. If any required entity type is completely missing, reject the import with error message: "Import file incomplete: missing {entity_type}. Restore from complete backup." This ensures data integrity and prevents partial/corrupted restores. Selective exports are not supported in MVP; users must backup/restore complete datasets.

- Q: Should member reactivation automatically forgive all prior outstanding fees or give coordinator choice? → A: **Option A: Automatic Debt Forgiveness on Reactivation**. When a coordinator marks an Inactive member as Active (reactivation), the system automatically and unconditionally: (1) Forgives ALL outstanding fees from prior years; (2) Creates GL write-off Transaction pairs for each forgiven fee with BadDebtExpense category; (3) Soft-deletes original Fee records for audit trail; (4) Resets member's payable balance to $0 (fresh start). Coordinator has NO choice—reactivation always triggers automatic forgiveness. Current-year annual fee will be applied per normal fee application process if renewal month arrives after reactivation. This prevents debt from becoming barrier to reactivation and ensures clean financial state for reactivated members.

- Q: When a member is reactivated (Inactive→Active), which unpaid fees should be cleared and what record should be maintained? → A: **Write-Off with Audit Trail**: Upon reactivation, system MUST: (1) Write off (forgive) all prior-period unpaid fees by creating GL reversing entries (debit on MemberReceivable, credit on BadDebtExpense/WriteOff category) to reverse previous fee transactions; (2) Soft-delete the original Fee records (IsDeleted=true, DeletedAt=now, DeletedBy=userId); (3) Member's payable balance resets to $0 and previous period outstanding balances are forgiven; (4) Current-year membership fee is payable - the annual fee application process will generate a fresh current-year fee when applicable. Prior fees are NOT deleted but marked as written-off via GL reversals and soft-delete flags, ensuring: historical record preserved, member starts fresh with clean slate, complete governance audit trail maintained showing write-offs and reasons.

- Q: Where should PaymentType (Annual/Attendance/Other) be stored, and how does it relate to GL Category? → A: **Option A - Explicit Payment Record Field**: PaymentType is stored as an explicit enum field on the Payment record (Payment.PaymentType = {Annual, Attendance, Other}). PaymentType is metadata used for reporting, filtering, and audit trails. GL Category (on Transaction records) is separate and used for accounting categorization/classification. When a payment is recorded: Payment record stores PaymentType explicitly; GL Transaction entries store Category (e.g., "MembershipPayment", "AttendanceFee"). Both fields are immutable after payment creation. This enables: clear reporting by payment type, operational filtering in Finance UI, distinct accounting categories, and full audit trail without reverse-engineering GL entries.

- Q: For backup/restore upsert operations, how should the system match existing records to determine whether to insert or update? → A: **Option A - Primary Key Only**: All entity types match by database Primary Key (ID field) during import. If source PK exists in target database, UPDATE all non-key fields with source values. If source PK does NOT exist in target database, INSERT the record. This is standard database upsert behavior, preserves backup integrity (exact IDs maintained), avoids complex business-key matching, and aligns with schema versioning. Users needing to modify business keys should use full export/re-import workflows after key changes are made locally.

- Q: When a member is archived, what happens to their committee role assignments (e.g., Treasurer, Secretary)? → A: **Option B - Soft-Delete Memberships**: When a member is archived (member status → Inactive), all active committee assignments held by that member are automatically soft-deleted in parallel (IsDeleted=true, DeletedAt=archive date, DeletedBy=user performing archive). Soft-delete flags and audit trail preserved enable historical reconstruction. Archived members no longer appear in active committee lists or permission grants. This ensures: consistent archival pattern (member and roles both soft-deleted), prevents archived inactive members from retaining active permissions, maintains full audit trail via soft-delete metadata, and aligns with Constitution §6.7 (data preservation via soft-delete).

- Q: For the Aging & Collections Report (FR-012), should fee aging be calculated from invoice date or due date? → A: **Option B - Due Date Field**: Fee entity MUST include a `DueDate` field (separate from CreatedAt). Aging calculation for reports uses `today - Fee.DueDate` to determine aging bucket (current, 30, 60, 90+ days). For annual fees: DueDate defaults to Dec 31 of the year they cover (or configurable via Settings). For attendance fees: DueDate equals event date. For other fees: DueDate defaults to 30 days from CreatedAt unless manually specified. This aligns with standard accounting practices for aging reports, enables accurate collections workflows, and provides meaningful "days past due" semantics. Fee.DueDate is immutable after fee creation (like other fee metadata).

- Q: What should the Fee entity structure include, and are fees mutable after creation? → A: **Option A: Formal Fee Entity with DueDate; Immutable After Creation**. Fee entity MUST include: (1) Unique ID; (2) Member FK; (3) Fee Type enum {Annual, Attendance, Other}; (4) Amount (decimal, 2+ places); (5) Fee Date (when fee applies: Jan 1 for annual fees, event date for attendance); (6) DueDate field (separate from CreatedAt; defaults per type); (7) CreatedAt timestamp (for FIFO tiebreaker); (8) IsDeleted soft-delete flag (NEVER set to true per Constitution §3.4). Fees are IMMUTABLE after creation—Amount, Date, Type, DueDate locked in UI, no edits allowed. Payments reduce member liability via GL Transaction pairs, not by modifying Fee records. Write-offs and adjustments also use GL reversals, not Fee deletion. This makes financial obligations explicit, immutable, and auditable.

- Q: Should first-run setup automatically apply annual membership fees or only initialize settings? → A: **Option A: Settings Initialization Only; No Automatic Fee Application**. Setup wizard ONLY initializes Settings record (organization name, annual fee amount, attendance fee amount, renewal month) and creates empty database schema. Upon setup completion, system presents empty dashboard with no fees created. Coordinator must: (1) Register members first via Members module; (2) Manually trigger "Apply Annual Fees" button in Finance module to create fees. This gives coordinators explicit control over billing start date, prevents premature charging before roster is verified, and aligns with manual fee application principle.

- Q: How should member activation/inactivation effective dates be recorded? Can coordinators backdate status changes? → A: **Option A: System-Assigned Effective Dates; No Backdating**. Member status transitions (Active↔Inactive) are recorded with TODAY'S DATE as the effective date at the moment coordinator clicks the status toggle button. Member entity includes: (1) `ActivateDate` (DateTime, nullable) - set when member transitions Inactive→Active; (2) `InactivateDate` (DateTime, nullable) - set when member transitions Active→Inactive. Effective dates are immutable after status change (not editable in UI). Status changes are immediate (no future/scheduled status changes). This ensures: transparent audit trail (action date = effective date), prevents historical manipulation, simplifies implementation. Query logic for historical attendance rates uses these dates: `WHERE Status='Active' AND ActivateDate <= event_date AND (InactivateDate IS NULL OR InactivateDate > event_date)`.

- Q: What is the default member status when a coordinator creates a new member? → A: **Option A: Default to Active Status**. When coordinator creates a new member via "Add Member" form and saves, if no explicit Status is selected, member defaults to Status='Active' with ActivateDate=today. Newly-created members are presumed to be joining to participate; coordinator can immediately inactivate if needed. This reduces friction for the common case (most new registrants are active participants) and aligns with optimistic UI design.

- Q: How should the Trial Balance report format accounts and organize debit/credit columns? → A: **Option A: Standard 3-Section GL Format with Separate Debit/Credit Columns**. Trial Balance report MUST display: (1) **Asset Accounts section** (Cash, MemberReceivable) with columns: Account | Debit | Credit | Balance (each account row shows its amount in Debit or Credit column as appropriate, zero in the other); (2) **Income Accounts section** (user-defined revenue categories) with same three columns; (3) **Expense Accounts section** (user-defined expense categories) with same three columns. Subtotals after each section. Grand total row at end: Total Debits | Total Credits (MUST be equal; if not equal, error prevents report generation). This follows standard accounting GL conventions, is immediately familiar to accounting users, and enables easy verification of GL balance.

- Q: How does the system determine which GL Account to use when recording transactions? → A: **Option A - Derived from Category Type**: GL accounts are derived deterministically from the Category type (Income vs Expense). When recording a transaction with a Category marked as "Income", system creates: Debit on MemberReceivable (Asset) → Credit on that Income category (Revenue GL). When Category is marked as "Expense": Debit on that Expense category (Expense GL) → Credit on Cash (Asset). For member payments: Debit on Cash (Asset) → Credit on MemberReceivable (Asset). No explicit GL Account lookup, user field, or lookup table needed during transaction recording. Mapping is implicit and deterministic based solely on Category type. This simplifies implementation, eliminates configuration errors, and ensures consistent GL structure across all transactions.

- Q: When a user displays a report, prints it, and then exports it to CSV within the same viewing session, should the system regenerate report data each time or cache it? → A: **Option A - No Caching**: Report data is generated fresh each time the user selects a report, and regenerated if the user prints or exports after display. Each action (select, print, export) triggers fresh data generation. This ensures: (1) Always-current data (no stale report values), (2) Simple implementation (no cache invalidation logic), (3) Transparent behavior (user sees "Generating report..." message each time), (4) Aligned with MVP scope. No cross-action caching; no session-level or view-level caches for report data.

- Q: When a third-party plugin registers a custom report, where should the plugin's report appear in the Reports menu hierarchy? → A: **Option B - Plugin Module Sections**: Each plugin module gets its own submenu section with the plugin module name as the section header. Plugin reports are nested under their plugin module name (e.g., "Attendance Analytics" plugin contributes reports under "Attendance Analytics" > "Monthly Summary"). This provides: (1) Clear ownership (users see which module provides each report), (2) Scalability (unlimited plugins, each with its own namespace), (3) Consistency (mirrors dashboard tile organization and Settings tab provider pattern), (4) Extensibility (plugins control their reports without modifying core menu structure). Reports menu structure: Members section, Finance section, then plugin sections (alphabetically by plugin module name).

- Q: When calculating historical attendance/participation rates for past rehearsals/events, how should the system identify "members active on that date"? → A: **Option A - Status + Effective Dates (AS OF calculation)**: For any historical event (rehearsal/event on date D), include a member in the denominator if: (1) Status='Active' on date D (not current), (2) ActivateDate <= D, (3) InactivateDate IS NULL OR InactivateDate > D, (4) IsDeleted=false (archived members always excluded). Query logic: `WHERE Status='Active' AND ActivateDate <= event_date AND (InactivateDate IS NULL OR InactivateDate > event_date) AND IsDeleted=false`. This reconstructs the historical active membership as it existed on that date, produces accurate historical metrics, and aligns with the effective dates model. Archived members are ALWAYS excluded from the denominator regardless of effective dates. Historical attendance rates thus represent the actual participation percentage of members who were active on that date.

- Q: How should the system visually distinguish the current year committee assignment from historical committee assignments on the member detail screen? → A: **Option A - Bold Current Year + "Current" Badge**: Current year committee entry displays in bold text with a small Bootstrap badge labeled "Current" (e.g., "**2026 (Current) - Treasurer**"). Historical years display in normal text weight without badge (e.g., "2025 - Secretary", "2024 - Treasurer"). This provides: (1) Immediate visual scanning (bold draws attention), (2) Clear accessibility (text + visual; screen readers read both bold tag and "Current" label), (3) Bootstrap styling consistency (uses badge component), (4) Sufficient contrast per WCAG AA. Implementation: Render current year's CommitteeMembership with `<strong>` tag wrapping year and position, plus `<span class="badge bg-light">Current</span>` for the badge label.

- Q: In what format should backup/restore data be serialized? → A: **Option B - Binary/Protobuf**: Backup exports data as Protocol Buffers (protobuf) binary format. Protobuf provides: (1) Compact wire format (smaller file sizes than JSON); (2) Fast serialization/deserialization via protobuf library; (3) Strong schema versioning and forward compatibility; (4) Platform-neutral binary format. Backup manifest includes `schemaVersion` metadata. Import validates schema version compatibility before deserialization. This approach balances file size efficiency, serialization performance, and schema versioning rigor aligned with MVP's accounting requirements and data preservation principles.

- Q: How should the GL account structure map user-defined categories to GL accounts? → A: **Option A - One GL Account Per Category**: Each user-defined income or expense category automatically maps to one unique GL account. When a coordinator creates a new category (e.g., "Membership Dues"), the system: (1) Assigns a unique GL account number (auto-generated sequentially); (2) Stores the mapping (Category → GL Account) implicitly in the Category entity; (3) Uses this mapping when creating GL Transactions. GL Account is deterministic from Category type. Example: Category "Membership Dues" (Income type) → GL Account #4100 (Revenue); Category "Hall Rental" (Expense type) → GL Account #5000 (Expense). This ensures: (1) Clear 1:1 traceability between categories and GL accounts; (2) Flexible future reporting by GL account; (3) Simple implementation without configuration tables; (4) Compliance with standard GL structure where each account has its own ledger.

- Q: In a single-user MVP with no authentication, what value should represent the acting user in audit trail logs? → A: **Option A - Fixed System User**: All audit trail entries record a fixed system identifier "system" (or alternatively "admin") as the acting user. When an audit trail entry is created for any data modification, the `DeletedBy` field (and similarly CreatedBy/ModifiedBy if present) is set to "system". This satisfies the audit trail requirement (who, what, when) while acknowledging MVP single-user scope. When multi-user support is added in Phase 2+, the audit field structure will already be in place for proper user tracking by authentication user ID. For MVP, "system" represents the implicit single user (the coordinator running the application).

- Q: Should the Committee Report include archived members' historical committee assignments or only active committee members? → A: **Option C - Configurable Filter**: The Committee Report should include a user-selectable filter allowing coordinators to view: (1) "Active Only" - currently-active members with current committee assignments; (2) "Archived Only" - archived members with their historical committee assignments; (3) "All" - combined view of both active and archived committee history. This provides: (1) Flexibility for different audit/reporting needs; (2) Transparency into historical committee service; (3) Data preservation alignment (archived members' history not hidden); (4) Coordinator control over report scope. Default filter = "Active Only" for common case; coordinator can change filter as needed.

- Q: What information should the Member List report display, and should it support filtering by member status? → A: **Option A - Status-Filtered Member List**: The Member List report displays member details (Name, Street Address, Phone, Email, Join Date, Calculated Age if provided, Status) with a configurable status filter. Coordinators can select: (1) "Active" (default) - active members only; (2) "Inactive" - inactive members only; (3) "Archived" - archived members only; (4) "All" - all members regardless of status. This ensures: (1) Flexible reporting for different administrative needs; (2) Data preservation transparency (archived members queryable but not shown by default); (3) Consistency with Committee Report filtering pattern; (4) No ambiguity about report scope or hidden data.

- Q: Should dashboard tiles load in parallel or sequentially when the dashboard first displays? → A: **Option A - Parallel Loading**: All four core dashboard tiles (Members, Rehearsals, Events, Finance) initiate data requests simultaneously when the dashboard loads. Each tile manages its own async data loading and displays independently as data becomes available. This ensures: (1) Minimum total load time (all tiles work concurrently); (2) Responsive UI (faster overall dashboard display); (3) Better user experience (tiles populate as ready, not blocked by slowest tile); (4) Graceful degradation (slow or failed tiles don't prevent other tiles from rendering). Dashboard layout remains visible and responsive while tiles load in parallel. Plugin tiles also load in parallel with core tiles.

- Q: Should PaymentMethod have a default value if the coordinator doesn't explicitly select one? → A: **Option C - Default to "Cash"**: When a payment is recorded and PaymentMethod is not explicitly selected, the system defaults to "Cash" automatically. This ensures: (1) Most common case handled without friction (cash is typical for performing arts groups); (2) Data always populated (no missing/null fields); (3) Coordinator can override if needed; (4) Payment records complete and immediately usable for financial reporting. This aligns with assuming the most likely method rather than blocking payment entry. PaymentMethod remains immutable after payment creation per FR-025.

- Q: Should dashboard tiles load in parallel or sequentially when the dashboard first displays? → A: **Option A - Parallel Loading**: All four core dashboard tiles (Members, Rehearsals, Events, Finance) initiate data requests simultaneously when the dashboard loads. Each tile manages its own async data loading and displays independently as data becomes available. This ensures: (1) Minimum total load time (all tiles work concurrently); (2) Responsive UI (faster overall dashboard display); (3) Better user experience (tiles populate as ready, not blocked by slowest tile); (4) Graceful degradation (slow or failed tiles don't prevent other tiles from rendering). Dashboard layout remains visible and responsive while tiles load in parallel. Plugin tiles also load in parallel with core tiles.

- Q: Should dashboard plugin tiles appear in a fixed location relative to core tiles, or be intermixed? → A: **Option A - Layered Organization**: Core tiles (Members, Rehearsals, Events, Finance) always display in the top section of the dashboard in a fixed 2-column grid layout. Plugin tiles appear in a separate, visually-distinct section below core tiles, also in 2-column grid layout. Each section header identifies the section (e.g., "Core Metrics" for core tiles, "Extensions" or plugin module name for plugin sections). This ensures: (1) Predictable UX (core tiles always in known location); (2) Clear visual hierarchy (MVP functions prominent, extensions secondary); (3) Plugin area clearly labeled and visually distinct; (4) Extensibility doesn't disrupt primary workflow; (5) Users immediately understand which tiles are MVP vs plugin-provided.

- Q: When a member is reactivated and prior fees are written off via GL transactions, should the write-off category be pre-defined or user-configured? → A: **Option A - Pre-Defined System Category**: "BadDebtExpense" is a built-in, non-editable system category created automatically during first-run setup. It cannot be archived, edited, or deleted by coordinators. When reactivation debt forgiveness occurs, GL write-off transactions use this fixed category. This ensures: (1) Write-off transactions always have a consistent, predictable GL account; (2) No risk of category deletion breaking reactivation logic during future use; (3) Simple implementation (no configuration required); (4) Aligns with standard accounting convention (bad debt expense is a recognized GL account); (5) Transparent to coordinators (pre-configured, no setup needed).

- Q: What should the default date range be for financial reports when first opened? → A: **Option A - Year-to-Date (Calendar Year)**: Financial reports default to the current calendar year (January 1 - December 31) when first opened. This ensures: (1) Most common business reporting period (standard calendar year); (2) Alignment with annual membership fee cycle (renewal month, YTD accounting); (3) Matches most organizations' fiscal year (Jan-Dec); (4) Intuitive for group coordinators (matches membership year and typical reporting needs); (5) Coordinator can select different date ranges via report date-range filters to view other periods as needed. System updates the YTD date range at midnight on Jan 1 each calendar year.

### Session 2026-05-15 (Clarification Pass #3)

This session clarified 6 CRITICAL and HIGH ambiguities identified by artifact consistency analysis:

- **Q1: Attendance Fee Creation & Payment Status (HIGH)** → **A: Option A with payment status default**
  - **Decision**: Attendance fees are automatically created when attendance is recorded (Option A).
  - **Payment Status Default**: Attendance fees default to **PAID** (PaymentStatus='Paid' or similar) when attendance is recorded
  - **Operator Override**: Coordinator can override default to mark as unpaid if needed (e.g., if attendance was recorded in error)
  - **Fee Removal on Attendance Clear**: If attendance flag is cleared for a member on a rehearsal, the corresponding attendance fee for that rehearsal is automatically removed
  - **Rationale**: Aligns with typical performing arts practice (attendance = payment received); simplifies workflow (coordinator doesn't need to record both); reduces manual data entry; still allows corrections if needed
  - **Implementation Details**:
    - When AttendanceService.RecordAttendance(member, rehearsal) is called:
      1. Check if Fee already exists for this member+rehearsal; if yes, skip creation (idempotent)
      2. Create Fee with Amount=Settings.AttendanceFee, FeeDate=rehearsal.Date, Status='Paid'
    - When attendance is cleared: AttendanceService.ClearAttendance(member, rehearsal)
      1. Find Fee(MemberId=member, FeeDate=rehearsal.Date, FeeType='Attendance')
      2. Soft-delete the Fee (set IsDeleted=true, DeletedAt=now, DeletedBy='system')
      3. Create GL reversing transaction pair to zero out GL entries
  - **Impact on Functional Requirements**:
    - FR-005: Updated to specify automatic creation AND default-paid status
    - FR-008: Outstanding balance reports exclude soft-deleted fees per soft-delete query filters
    - FR-016: GL Transactions created for both paid AND unpaid fees; paid fees create GL pairs immediately; unpaid fees also create GL pairs (status is metadata, not GL driver)
  - **Schema Updates Needed**: Fee entity includes `PaidAtCreation` boolean field (immutable after creation); AttendanceService passes payment status at Fee creation
  - **Task Updates Needed**: T-054 (AttendanceService) implements fee creation with payment status default and removal logic; T-076 (test AttendanceService) includes test cases for fee creation/removal and payment status defaults

- **Q2: Fee Payment Status Field Design (MEDIUM)** → **A: Option D - Single immutable status at creation**
  - **Decision**: Fee entity includes `PaidAtCreation` boolean field (immutable after Fee creation):
    - When Fee is created, `PaidAtCreation` is set based on payment status at creation time
    - For attendance fees: `PaidAtCreation` defaults to **true** (paid by default per Q1)
    - For annual fees: `PaidAtCreation` defaults to **false** (unpaid, awaiting payment)
    - For other fees: coordinator specifies when creating fee
    - After Fee creation, `PaidAtCreation` value is immutable (no updates allowed)
  - **Payment Tracking Mechanism**: GL transactions (not Fee status updates) provide audit trail of actual payments and GL reversals. Member balance calculated from GL, not from Fee status. Fee.PaidAtCreation is metadata only, indicating financial obligation at creation; GL provides transaction history.
  - **Rationale**: Fee is immutable financial record per Constitution §3.4; status at creation is metadata for reporting/aging. Subsequent GL transactions provide full audit trail without modifying Fee. Aging calculations can use Fee.PaidAtCreation + GL query for accurate aged balances. Simpler than PartiallyPaid enum or derived GL queries.
  - **Example Flow**: 
    1. Attendance recorded → Fee created with Amount=$50, FeeDate=today, PaidAtCreation=true
    2. Later, coordinator learns attendance was error, clears attendance → Fee soft-deleted, GL reversing entries created
    3. Or: Fee created with PaidAtCreation=true, but 30 days later no actual payment received → Member Account Summary shows fee with aging "overdue" based on GL transactions, regardless of PaidAtCreation value
  - **Impact on Outstanding Balance Calculation**: 
    - `Outstanding = sum(GL debits for member) - sum(GL credits for member)`
    - GL transactions are the source-of-truth, not Fee.PaidAtCreation
    - Fee.PaidAtCreation is informational (for historical "was this fee expected to be paid at creation?" queries only)
  - **Spec Updates Needed**: FR-028 (Fee entity definition) adds `PaidAtCreation` field as immutable boolean; updated schema documentation to clarify GL as payment truth.
  - **Task Updates Needed**: T-033 (Fee entity) includes `PaidAtCreation` field with immutability constraint; T-054 (AttendanceService) passes PaidAtCreation=true; T-106 (PaymentService) does not modify Fee, only creates GL pairs.

- **Q3: Financial Record Soft-Delete Field Design (CRITICAL)** → **A: Option B - Remove soft-delete fields entirely**
  - **Decision**: Transaction, Payment, and Fee entities will have NO soft-delete fields (`IsDeleted`, `DeletedAt`, `DeletedBy`)
  - **Rationale**: Constitution §3.4 states financial records are "EXEMPT from soft-delete pattern"; interpreted as the pattern does not apply at all. Removing fields entirely prevents accidental misuse and simplifies schema.
  - **Impact on FR-024**: Reactivation logic uses GL reversing transactions (already specified in FR-24) without soft-deleting original Fee records. Fee records remain immutable once created; GL write-offs provide full audit trail of debt forgiveness.
  - **Schema Impact**: Transaction, Payment, Fee entities lack soft-delete fields entirely. No `IsDeleted`, `DeletedAt`, `DeletedBy` fields in these entities.
  - **Spec Updates Needed**: FR-024 reworded to remove soft-delete references; updated schema documentation.
  - **Task Updates Needed**: T-032, T-033 (entity definitions) corrected to exclude soft-delete fields from financial entities.

- **Q4: GL Account Assignment Algorithm (HIGH)** → **A: Type-Prefixed Numbering Scheme**
  - **Decision**: GL accounts use type-prefixed scheme:
    - **Asset Accounts**: GL#01xx (GL#0100=Cash, GL#0101=MemberReceivable)
    - **Income Categories**: GL#10xx (GL#1000 for first income category, GL#1001 for second, etc.)
    - **Expense Categories**: GL#20xx (GL#2000 for first expense category, GL#2001 for second, etc.)
    - **Write-off Account**: GL#9900 (BadDebtExpense for reactivation debt forgiveness)
  - **Mechanism**: When a coordinator creates a new category, system invokes GLAccountAssignmentService which:
    1. Determines category type (Income or Expense)
    2. Queries existing categories of that type to find next available sequence number
    3. Auto-assigns GL account: Income category #2 → GL#1001; Expense category #5 → GL#2004
    4. Stores assignment in Category.glAccount field (read-only after creation)
  - **Rationale**: Provides clear visual type distinction (first digit indicates category), room for 100 categories per type, follows standard ERP accounting practice, enables future type-based GL filtering.
  - **Spec Updates Needed**: FR-032 updated with specific GL account ranges and numbering algorithm; Category schema documents glAccount auto-assignment.
  - **Task Updates Needed**: New task T-034b "Implement GLAccountAssignmentService with sequential numbering per category type"; T-034 updated to reference this service.

- **Q5: Committee Annual Reset Trigger Mechanism (HIGH)** → **A: Configurable Committee Renewal Month with Startup Check**
  - **Decision**: Add user-configurable "Committee Renewal Month" setting (distinct from Membership Renewal Month):
    - **Setting Name**: CommitteeRenewalMonth (integer 1-12, default 1 for January)
    - **Trigger**: On application startup, system compares current month/year against `Settings.LastCommitteeResetYear`
    - **Logic**: If `(CurrentMonth >= CommitteeRenewalMonth AND LastCommitteeResetYear < CurrentYear)`, then:
      1. Invoke CommitteeAnnualResetService synchronously before dashboard displays
      2. Clear all members' current-year committee status (set to not-committee)
      3. Preserve all prior-year records as read-only history
      4. Update `Settings.LastCommitteeResetYear = CurrentYear`
    - **Timing**: Reset runs exactly once per committee year, on first app launch on or after the configured month
    - **User Experience**: Coordinators manually re-enter committee assignments for the new year via member edit form
  - **Rationale**: Aligns reset with user's governance cycle (independent from fee renewal month), ensures exactly one reset per year, simple startup-based logic (no background tasks required), acceptable single-user MVP behavior (reset tied to app restart), transparent (no hidden resets at midnight).
  - **Spec Updates Needed**: FR-031 reworded to specify configurable month and startup-based reset; FR-018 (Settings module) adds CommitteeRenewalMonth field; User Story 2 updated to mention Committee Renewal Month configuration.
  - **Schema Updates Needed**: Settings entity adds: (1) `CommitteeRenewalMonth` field (int 1-12, default 1); (2) `LastCommitteeResetYear` field (int, default current year - 1).
  - **Task Updates Needed**: T-030 (Settings entity definition) adds two fields; T-167 (Committee annual reset) documents startup check logic with month comparison and guard against duplicate resets.

- **Q6: Payment Field Immutability Specification (HIGH)** → **A: Single UpdatedAt Timestamp**
  - **Decision**: Payment entity includes both `CreatedAt` and `UpdatedAt` timestamps:
    - `UpdatedAt` field updates ONLY when Notes field changes
    - Amount, Date, PaymentMethod, PaymentType, Category fields remain strictly immutable after payment creation
    - Database constraint or application validation enforces immutability on locked fields
    - If UpdatedAt ≠ CreatedAt, it indicates Notes were edited; audit trail logs detailed Notes changes separately
  - **Rationale**: Standard ORM pattern (CreatedAt/UpdatedAt on most entities), enables queries like "payments modified after X date", audit trail provides detailed change logging separately, field-level immutability is explicit and verifiable in schema.
  - **Implementation Details**:
    - PaymentRepository Update method rejects any attempts to modify Amount, Date, PaymentMethod, PaymentType, Category with exception: "Payment fields are immutable after creation; only Notes may be edited"
    - Audit trail logs each Notes modification with old and new values
    - UI prevents coordinator from editing locked fields (greyed out or removed from edit form)
  - **Spec Updates Needed**: FR-017 clarified that ONLY Notes may be modified; FR-025 clarified that PaymentMethod and PaymentType are immutable; Payment schema documentation includes immutability rules.
  - **Task Updates Needed**: T-106 (PaymentRepository implementation) includes field-level immutability validation; T-114 (Payment UI edit form) disables/hides immutable fields.

---

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
- [ ] Member lifecycle model documented (Status enum independent from IsDeleted flag; query patterns defined)
- [ ] GL transaction paired entry model documented (every financial event creates debit + credit pair)
- [ ] Member balance calculation formula documented (sum of GL debits where member=X minus sum of GL credits where member=X)
- [ ] Payment allocation model documented (GL-centric, FIFO ordering via GL date)
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
- [ ] GL transaction pair creation and GL balance validation have unit and integration test coverage
- [ ] Member lifecycle state transitions (Active ↔ Inactive, Active ↔ Archived) have test coverage with correct Status + IsDeleted combinations
- [ ] Payment allocation FIFO ordering and member balance calculation from GL have test coverage
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

