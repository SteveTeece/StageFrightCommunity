# Feature Specification: Committee Report Year Summary

**Feature Branch**: `010-committee-report-year-summary`

**Created**: 2026-07-25

**Status**: Draft

**Input**: User description: "create a new spec to update the Committee Report. Report should show a summary row for each year. Then expanded, the row should show who holds the roles of Persident, Secretary, Treasurer and general committee members. This feature is the intended work for issue #234."

## Clarifications

### Session 2026-07-25

- Q: How should the report handle two different members both recorded against the same named role (President/Secretary/Treasurer) in the same year? → A: List all members recorded for that role together (e.g., "President: Alice, Bob")
- Q: How should positions other than President/Secretary/Treasurer (e.g., "Welfare Officer") be displayed? → A: Each distinct position label is shown as its own line item, separate from "General Committee Members"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Year-grouped committee overview (Priority: P1)

As a committee secretary reviewing the Committee Report, I want each year to appear as a single summarized block instead of a flat list of member/year/position rows, so I can quickly see how many committee positions were recorded for each year without manually counting or cross-referencing rows.

**Why this priority**: This is the core structural change requested — without year grouping, none of the other role-breakdown behavior has anywhere to live. It delivers immediate value on its own: a scannable, year-by-year view of committee history.

**Independent Test**: Can be fully tested by generating the Committee Report against seed data spanning multiple years and confirming exactly one summary row appears per year that has committee records, each showing the year and how many committee positions were recorded that year.

**Acceptance Scenarios**:

1. **Given** committee membership records exist across several years, **When** the Committee Report is generated, **Then** the report displays one summary row per year (most recent year first), each showing the year and the count of committee positions recorded for that year.
2. **Given** the existing "Member Status" filter (Active Only / Archived Only / All) is applied, **When** the report is generated, **Then** only committee records belonging to members matching the filter are counted and included in each year's summary.
3. **Given** a year has no committee membership records under the active filter, **When** the report is generated, **Then** that year does not appear in the report.

---

### User Story 2 - Role breakdown within each year (Priority: P2)

As a committee secretary, I want to see who held President, Secretary, and Treasurer for a given year, plus who held any other recorded committee position (e.g., "Welfare Officer") that year, and who served as an unlabeled general committee member, so I can confirm accountability and committee composition for any past or current year without looking anywhere else.

**Why this priority**: This is the specific detail the report is being changed to surface. It depends on the year grouping from User Story 1 but is the actual information value the user is asking for.

**Independent Test**: Can be fully tested by generating the report for a year with a full committee (President, Secretary, Treasurer, a "Welfare Officer", and several unlabeled general members) and confirming every position is shown correctly labeled under that year.

**Acceptance Scenarios**:

1. **Given** a year's summary, **When** the report is generated, **Then** the member holding "President" for that year is shown, and likewise for "Secretary" and "Treasurer".
2. **Given** a named role (President, Secretary, or Treasurer) has no member recorded for a year, **When** the report is generated, **Then** that role is shown as "Vacant" rather than being left out of the report.
3. **Given** members hold a recorded committee position other than President, Secretary, or Treasurer (e.g., "Welfare Officer"), **When** the report is generated, **Then** each distinct position label is shown as its own line listing the members who hold it that year, separate from the President/Secretary/Treasurer lines and from "General Committee Members".
4. **Given** members have a blank or empty committee position value, **When** the report is generated, **Then** they are grouped and listed as "General Committee Members" for that year, sorted alphabetically by name.
5. **Given** two members are both recorded against the same position label in the same year (a data entry anomaly), **When** the report is generated, **Then** both members are listed together for that position rather than one being silently dropped.

---

### User Story 3 - Exportable, consistent output (Priority: P3)

As a committee secretary, I want the redesigned report to still export cleanly to PDF and CSV, so I can print or share it the same way I do today.

**Why this priority**: Lower risk and lower priority than the content changes, but the report is only useful if its existing export paths keep working with the new layout.

**Independent Test**: Can be fully tested by exporting a generated report to both PDF and CSV and confirming the year/role structure is legible and correctly represented in both formats.

**Acceptance Scenarios**:

1. **Given** a report with multiple year summaries and role breakdowns, **When** exported to PDF, **Then** each year's summary and role breakdown are legible and do not split confusingly across pages.
2. **Given** the same report, **When** exported to CSV, **Then** each row identifies the year and the role or member it refers to, preserving the year/role structure without losing information.

---

### Edge Cases

- What happens when no committee records exist at all under the active filter? The report shows the existing "no data" empty state rather than an empty list of years.
- How does the system handle a year where only some named roles are filled? Unfilled named roles show as "Vacant"; filled roles and general members display normally.
- How does the system handle position values that differ only in casing or surrounding whitespace (e.g., "president " vs "President", or "welfare officer" vs "Welfare Officer")? These are matched and grouped together (case-insensitive, trimmed) as the same role or position label rather than appearing as separate lines.
- How does the system handle an empty or blank position value on a committee record? The record is treated as an undefined/general position and grouped under "General Committee Members" rather than causing an error.
- How does the system handle a year with many distinct non-named position labels (e.g., "Welfare Officer", "Publicity Officer")? Each distinct label is shown as its own line for that year; there is no fixed limit on the number of distinct position lines a year can display.

Note: a member can hold only one recorded position per year (existing data model constraint), so a single member appearing under two different roles or position labels in the same year cannot occur.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The Committee Report MUST group committee membership records by year, with the most recent year displayed first.
- **FR-002**: For each year present in the filtered data, the report MUST display a summary row showing the year and the total number of committee positions recorded for that year.
- **FR-003**: Beneath each year's summary, the report MUST display which member holds the "President" role for that year, or "Vacant" if none is recorded.
- **FR-004**: Beneath each year's summary, the report MUST display which member holds the "Secretary" role for that year, or "Vacant" if none is recorded.
- **FR-005**: Beneath each year's summary, the report MUST display which member holds the "Treasurer" role for that year, or "Vacant" if none is recorded.
- **FR-006**: Beneath each year's summary, the report MUST display every other distinct, non-blank position value recorded that year (i.e., not President, Secretary, or Treasurer) as its own line, listing the members who hold it, sorted alphabetically by name within the line, and the lines themselves ordered alphabetically by position label after the President/Secretary/Treasurer lines.
- **FR-006a**: Committee records with a blank or empty position value MUST be grouped together under a single "General Committee Members" line (sorted alphabetically by name) rather than displayed as their own unlabeled line.
- **FR-007**: Matching and grouping of position values — including the "President", "Secretary", and "Treasurer" named roles and any other distinct position label — MUST be case-insensitive and ignore leading/trailing whitespace, so equivalent labels are combined into a single line rather than split across near-duplicate lines.
- **FR-008**: The existing "Member Status" filter (Active Only / Archived Only / All) MUST continue to determine which members' committee records are included in the year groupings and position breakdowns.
- **FR-009**: Years with no committee records under the active filter MUST be omitted from the report rather than shown with all roles vacant.
- **FR-010**: If more than one member is recorded against the same position label (named role or otherwise) in the same year, the report MUST display all of them together under that single label (e.g., "President: Alice, Bob") rather than dropping or overwriting any of them.
- **FR-011**: The report MUST continue to support PDF export and CSV export through the existing report generation pipeline, preserving the year/position-line structure in both formats.
- **FR-012**: When no committee records match the active filter, the report MUST display the existing empty-state message rather than an empty set of year groupings.

### Key Entities *(include if feature involves data)*

- **Committee Membership**: An existing record of a member's committee position for a given calendar year (member, year, position). This feature reshapes how these records are grouped and displayed; no new data is introduced.
- **Member**: An existing person who may hold zero or more committee memberships across years. Only their name and committee position(s) are relevant to this report.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can identify who held President, Secretary, Treasurer, and any other recorded committee position for any given year in the report within 5 seconds, without cross-referencing multiple rows.
- **SC-002**: 100% of years with committee records under the active filter appear as a distinct summary in the generated report.
- **SC-003**: 100% of P1 and P2 acceptance scenarios pass before the feature is considered complete.
- **SC-004**: Users can export the redesigned report to PDF or CSV and find the year and role information fully legible, with no loss of information compared to the on-screen report.

## Assumptions

- "Expanded" is interpreted as each year's summary and its full role breakdown being presented together as one grouped block within the generated report output (matching how this report already renders as a single generated PDF/CSV document), not as an interactive on-screen collapse/expand control.
- Years continue to be ordered most-recent-first, consistent with the current report's behavior.
- The "President", "Secretary", and "Treasurer" role labels are matched against the existing free-text Position field on committee membership records. Any other non-blank value (e.g., "Welfare Officer") is displayed as its own distinct position line rather than being folded into "General Committee Members"; only blank/empty position values fall into that catch-all line.
- The existing "Member Status" report filter (Active Only / Archived Only / All) is reused unchanged; no new filters are introduced by this feature.
- This feature is the intended work for issue #234.
