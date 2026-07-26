# Feature Specification: Split Member Name into First Name and Last Name

**Feature Branch**: `011-member-firstname-lastname`

**Created**: 2026-07-26

**Status**: Draft

**Input**: User description: "Create a spec to separate member name into firstname and lastname per issue #260"

**Source**: GitHub issue #260 — "Separate Member names into First an[d] Last" ("Seperate teh Member Name field into Firstname and Lastname.")

## Clarifications

### Session 2026-07-26

- Q: What should the maximum length be for the new First Name and Last Name fields individually? → A: 100 characters each (First Name max 100, Last Name max 100).
- Q: During the automatic conversion (FR-006), what should happen if a split First Name or Last Name value is longer than the new per-field maximum length? → A: Truncate the overlong value to fit the new max length so the upgrade always completes for every record.
- Q: Should the system prevent two different members from having the same First Name + Last Name combination? → A: No — duplicates are allowed; the system does not enforce uniqueness on full names (matching current behavior, which has no uniqueness constraint on the combined Name field).
- Q: For CSV exports and printed/PDF reports (Member List, Member Account Summary, Committee) that previously showed one combined Name column, should the new output use two separate First Name/Last Name columns or one combined Full Name column? → A: Single combined Full Name column, displayed as "Last Name, First Name" (matching the FR-005 sort order).
- Q: When splitting an existing combined Name value that has irregular spacing (leading/trailing spaces, multiple spaces between words), how should the conversion normalize it? → A: Trim leading/trailing whitespace and collapse multiple internal spaces to one before splitting on the first space.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Enter first and last name separately (Priority: P1)

A committee administrator adding a new member, or editing an existing member's details, enters the member's given (first) name and family (last) name as two separate fields instead of one combined "Name" field.

**Why this priority**: This is the core of the change — without separate entry fields, none of the downstream benefits (accurate sorting, personalization, cleaner reporting) are possible. It is also the smallest independently shippable slice.

**Independent Test**: Can be fully tested by opening the Add Member / Edit Member screen, entering distinct values into First Name and Last Name fields, saving, and confirming both values are persisted and redisplayed correctly.

**Acceptance Scenarios**:

1. **Given** the Add Member screen, **When** the administrator enters a First Name and a Last Name and saves, **Then** a new member record is created with both values stored separately.
2. **Given** an existing member record, **When** the administrator opens Edit Member, **Then** the First Name and Last Name fields are pre-populated with the member's current values, and saving changes updates each field independently.
3. **Given** the Add Member screen, **When** the administrator attempts to save without completing a required name field, **Then** the system displays a validation message and does not save the record.

---

### User Story 2 - Find and browse members by name (Priority: P2)

A committee administrator searches, sorts, and browses member lists, reports, and other screens that reference a member's name (Member List, Member Detail, dashboard, attendance and participation grids, Committee report, Member Account Summary, Member List report), and sees accurate full names and can search or sort using either name part.

**Why this priority**: Once names can be entered separately, every existing screen and report that already displays or sorts by member name must continue to work correctly and take advantage of the new structure — this is what makes the change usable day-to-day, but it depends on User Story 1 existing first.

**Independent Test**: Can be fully tested by searching the Member List using only a last name, only a first name, and a full name, and confirming the correct member(s) appear; and by opening each report/screen that shows member names and confirming names render correctly and lists sort consistently.

**Acceptance Scenarios**:

1. **Given** the Member List screen, **When** the administrator types a member's last name into the search box, **Then** matching members are shown.
2. **Given** the Member List screen, **When** the administrator types a member's first name into the search box, **Then** matching members are shown.
3. **Given** a report or screen that previously showed a single "Name" value (e.g. Committee report, Member Account Summary, Attendance grid, Participation grid, dashboard tiles), **When** the administrator views it, **Then** the member's full name is displayed correctly with no missing or malformed values.

---

### User Story 3 - Existing member records are converted automatically (Priority: P3)

When the application is upgraded to this version, every existing member's current combined "Name" value is automatically split into First Name and Last Name so the administrator does not have to manually re-enter data for every member.

**Why this priority**: This protects existing data and avoids a manual data-entry burden, but the application is still usable (for new members) even before existing records are perfectly split, so it is lower priority than the two stories above.

**Independent Test**: Can be fully tested by taking a copy of the database containing existing member records, running the upgrade, and confirming every member record has non-empty name data and no record is lost, duplicated, or corrupted.

**Acceptance Scenarios**:

1. **Given** an existing member record with a combined Name value of "Jane Smith", **When** the upgrade runs, **Then** the member has a First Name and Last Name populated per the conversion rule, and the member's total record count is unchanged.
2. **Given** the full set of existing member records, **When** the upgrade completes, **Then** no member record is deleted, duplicated, or left with completely empty name data.

---

### Edge Cases

- What happens when an existing member's combined Name value contains only one word (no space), such as a mononym or a data-entry error?
- What happens when an existing member's combined Name value contains more than two words (e.g. a middle name, double-barrelled surname, or suffix like "Jr.")?
- Resolved: An existing combined Name value with irregular spacing (leading/trailing spaces, multiple internal spaces) is trimmed and collapsed to single spaces before being split on the first space (FR-006).
- Resolved: If a First Name or Last Name value produced by conversion exceeds the 100-character per-field maximum (FR-009), it is truncated to 100 characters so the upgrade still completes for that record (FR-006).
- How do archived (inactive/soft-deleted) member records behave during the conversion — are they converted the same way as active members?
- Resolved: Two different members may end up with the same First Name and Last Name combination; the system does not enforce uniqueness on member names, consistent with the current combined Name field having no uniqueness constraint.
- Resolved: Existing exported/printed reports and backups display a single combined Full Name column (formatted "Last Name, First Name"), not two separate First Name/Last Name columns (FR-003).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST capture a member's First Name and Last Name as two distinct pieces of information wherever a member record is created or edited, replacing the single combined Name field.
- **FR-002**: The system MUST require both First Name and Last Name to be provided before a member record can be saved.
- **FR-003**: The system MUST display a member's full name (First Name and Last Name combined) everywhere a single combined name was previously shown, including the Member List, Member Detail, Add/Edit Member confirmation, dashboard tiles, Attendance and Participation grids, and printed/exported reports (Member List, Member Account Summary, Committee). Printed/exported reports and CSV exports MUST retain a single combined Full Name column (formatted "Last Name, First Name," consistent with FR-005) rather than splitting into separate First Name and Last Name columns.
- **FR-004**: The system MUST allow administrators to search for members by First Name, by Last Name, or by full name from the Member List search box.
- **FR-005**: The system MUST sort member listings and reports alphabetically by Last Name, then First Name, and MUST display names in "Last Name, First Name" order in sorted lists and reports; the Add/Edit Member form and Member Detail header MUST display/enter names in "First Name Last Name" order.
- **FR-006**: The system MUST automatically convert every existing member's current combined Name value into separate First Name and Last Name values as part of the upgrade, without requiring manual re-entry by an administrator. The conversion MUST first trim leading/trailing whitespace and collapse multiple internal spaces to a single space, then split the resulting value on its first space: the text before the first space becomes First Name, and the remaining text (if any) becomes Last Name. If a resulting First Name or Last Name value exceeds the 100-character maximum defined in FR-009, the conversion MUST truncate that value to 100 characters so the upgrade completes for every record without failure.
- **FR-007**: The conversion described in FR-006 MUST NOT lose, duplicate, or corrupt any existing member data; every existing member record MUST still exist, in the same status (active/inactive/archived), after conversion.
- **FR-008**: When an existing combined Name value contains no space (a single word), the conversion MUST place the entire value in First Name and leave Last Name blank for administrators to complete later, without blocking the upgrade or hiding the affected member from normal views.
- **FR-009**: The system MUST enforce a maximum length of 100 characters on First Name and 100 characters on Last Name, each validated independently.
- **FR-010**: All existing member-related functionality that referenced the combined name — search, sort, filters, attendance and participation tracking, committee reporting, member balance/financial reporting, audit trail history, and data backup/export — MUST continue to work correctly using the new First Name and Last Name fields.
- **FR-011**: The system MUST record changes to First Name and Last Name in the member's audit history the same way other member field edits are tracked today.

### Key Entities

- **Member**: Represents a person belonging to the performing arts group. The existing single "Name" attribute is replaced by two attributes — **First Name** (given name) and **Last Name** (family name) — with a derived, read-only **Full Name** used for display purposes wherever the combined name was previously shown. All other Member attributes and relationships (address, contact details, join date, status, committee memberships, financial and attendance history) are unchanged by this feature.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of existing member records retain non-empty First Name data and correct record status (active/inactive/archived) after the upgrade, with zero records lost or duplicated.
- **SC-002**: Administrators can locate any member by typing either their first name or their last name into search, with correct results returned every time.
- **SC-003**: All six MVP reports and every screen that previously displayed a member's combined name (Member List, Member Detail, dashboard, Attendance grid, Participation grid, Committee report, Member Account Summary report) display accurate, correctly formatted full names with no missing, truncated, or malformed values.
- **SC-004**: Adding or editing a member with separate First Name and Last Name fields takes no longer than the current single-field process (no added time-on-task for administrators).
- **SC-005**: Member lists and reports sorted by name consistently order entries by Last Name, then First Name, across every screen and report that sorts by name.

## Assumptions

- Existing member records must be preserved exactly as required by the project's data-preservation rules for members; the name-field conversion is additive/structural and does not delete or archive any member.
- First Name and Last Name are each capped at 100 characters (FR-009); real member names are not expected to routinely approach this length, so truncation during conversion (FR-006) is expected to affect a negligible number of legacy records, if any.
- This feature covers the Member entity only. Other entities that store a "Name" (Events, Event Types, Chart of Accounts, Categories, etc.) are out of scope and are not affected.
- Existing reports, exports, and backups that reference member names will be updated to show a single combined Full Name column (derived from First Name/Last Name, formatted "Last Name, First Name") rather than requiring a new "combined name" field to be reintroduced or splitting into two output columns.
- Archived (inactive/soft-deleted) member records are included in the automatic conversion described in User Story 3, consistent with the project's requirement that inactive members retain all historical data.
