# Feature Specification: Member Account Summary Report Redesign

**Feature Branch**: `005-member-account-summary-redesign`

**Created**: 2026-07-12

**Status**: Draft

**Input**: User description: "Change the Member Activity Report. Add a filter to select if archived members are displayed. List each member's name and current aging by default. Clicking on a member expands that member to list full transactions. Sort all transactions by date with the newest entries first. Migrate the data grid into standard format if it makes sense for the page redesign."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Committee member scans aging at a glance (Priority: P1)

A committee member opens the Member Account Summary report and wants to quickly see which members currently owe money and how overdue it is, without wading through every transaction for every member.

**Why this priority**: This is the core value of the redesign — today the report always renders every transaction for every member, which is slow to scan. A collapsed name + aging view is the primary reason for the change.

**Independent Test**: Seed several members with a mix of paid and unpaid fees at different ages, load the report, and confirm it initially shows one row per member with name and aging bucket totals only — no transaction-level detail visible until a member is expanded.

**Acceptance Scenarios**:

1. **Given** the report has generated, **When** it first renders, **Then** each member appears as a single collapsed row showing their name and their current/30/60/90+ day aging totals, with no transaction rows visible.
2. **Given** a member has no outstanding fees, **When** the report renders, **Then** that member's row still appears with all aging totals at zero rather than being omitted.

---

### User Story 2 - Committee member drills into one member's history (Priority: P1)

A committee member spots a concerning aging total for a member and wants to see the full transaction history that produced it.

**Why this priority**: Equal to User Story 1 — the collapsed view is only useful if full detail remains one click away.

**Independent Test**: Seed a member with several transactions across the report period, load the report, click that member's row, and confirm the full opening balance / transaction list / closing balance / aging breakdown for that member appears, matching what the report showed in full before this redesign.

**Acceptance Scenarios**:

1. **Given** a member's row is collapsed, **When** the committee member clicks/activates that row, **Then** the row expands in place to show that member's opening balance, every transaction in the selected period, the closing balance, and the aging breakdown.
2. **Given** a member's row is expanded, **When** the committee member clicks/activates it again, **Then** it collapses back to the summary row.
3. **Given** multiple members' rows are visible, **When** one member's row is expanded, **Then** other members' rows are unaffected (each expands/collapses independently).

---

### User Story 3 - Committee member reviews only active members by default (Priority: P2)

A committee member normally only cares about current members' balances and wants archived (former) members excluded from the report unless specifically requested.

**Why this priority**: Reduces noise for the common case; still needed for completeness checks (e.g. an archived member who still owes money), hence a filter rather than permanent removal.

**Independent Test**: Seed both active and archived members with balances, load the report with the archived-members filter off, and confirm only active members appear; toggle the filter on and confirm archived members (labeled "(Archived)") also appear.

**Acceptance Scenarios**:

1. **Given** the report loads with default filters, **When** it renders, **Then** archived members are not shown.
2. **Given** the committee member enables "Show Archived Members" and applies the filter, **When** the report regenerates, **Then** archived members appear in the list, labeled "(Archived)" as today.

---

### User Story 4 - Committee member reads a member's history newest-first (Priority: P3)

When reviewing an expanded member's transactions, the committee member wants to see the most recent activity first, matching how they'd expect a statement to read (most relevant information up top).

**Why this priority**: A readability/ordering improvement on top of Stories 1–2; the report is still useful without it, but it's a small, explicitly requested change.

**Independent Test**: Seed a member with transactions on at least three different dates, expand that member, and confirm transactions are listed from newest date to oldest date. Opening Balance remains the first row and Closing Balance remains the last row (before the aging summary), unchanged from today's ordering.

**Acceptance Scenarios**:

1. **Given** a member has transactions dated 2026-01-01, 2026-02-09, and 2026-02-16, **When** that member's row is expanded, **Then** the transactions are listed in the order 2026-02-16, 2026-02-09, 2026-01-01 — Opening Balance still appears above them and Closing Balance still appears below them.

---

### Edge Cases

- What happens when a member has zero transactions in the selected period? Their collapsed row still shows name and aging (opening balance = closing balance); expanding shows just Opening Balance, Closing Balance, and the aging row with no transaction rows in between.
- What happens when every member is fully paid up (no outstanding fees at all)? The report still renders one collapsed row per member with all aging columns at zero — it does not hide itself or show an empty state.
- What happens to Print/PDF and CSV export? They continue to include full opening balance, every transaction, closing balance, and aging detail for every member (active, and archived if the filter was on when generated) regardless of which rows are expanded/collapsed on screen — the collapse/expand behavior is a screen-only convenience, not a change to the exported document.
- How does paging interact with per-member expansion? Paging operates over one row per member (not one row per transaction), so a page always shows a whole number of members; expanding a member does not change how many members are shown per page.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The report MUST provide a filter to include or exclude archived members, defaulting to **excluded**.
- **FR-002**: When the archived-members filter is enabled, archived members MUST continue to be labeled "(Archived)" next to their name, consistent with current behavior.
- **FR-003**: By default, the report MUST render one collapsed row per member showing the member's name and their current/30-day/60-day/90+-day aging totals, with no transaction-level detail visible.
- **FR-004**: A committee member MUST be able to click/activate a member's row to expand it in place, revealing that member's opening balance, every transaction in the selected period, the closing balance, and the aging breakdown — the same information the report shows today, just collapsed by default.
- **FR-005**: Expanding one member's row MUST NOT affect the expand/collapse state of any other member's row.
- **FR-006**: Within an expanded member's detail, transactions MUST be sorted by date newest-first. Opening Balance remains the first row and Closing Balance remains the row immediately preceding the aging summary, unchanged from today.
- **FR-007**: Print/PDF and CSV exports MUST continue to include full transaction detail for every member in scope (per the archived-members filter), regardless of on-screen expand/collapse state.
- **FR-008**: The on-screen member list MUST use the same `RadzenDataGrid` component, paging (`PageSize="15"`), and visual conventions used elsewhere in the system (e.g. the Members grid), to the extent that a dynamic, per-report column layout allows. Native column-header sorting is not required for this grid; the list is ordered alphabetically by member name.
- **FR-009**: This redesign MUST NOT change the underlying GL/aging calculations already used by the report — only how the results are filtered, ordered, and presented.
- **FR-010**: The report generation pipeline (`ReportData`/`ReportSection`) MUST gain this master/detail capability as an optional, backward-compatible extension, such that the other five existing reports (which do not use it) render exactly as they do today.

### Key Entities

- **Member Summary Row**: A derived, collapsed representation of one member — name (with archived suffix if applicable), current/30/60/90+ day aging totals, and closing balance — shown by default in place of that member's full transaction detail.
- **Member Detail (expanded)**: The existing per-member breakdown — opening balance, period transactions (now newest-first), closing balance, aging summary — unchanged in content, only in default visibility and transaction order.
- **Archived-Members Filter**: A report filter, defaulting to off, controlling whether archived (soft-deleted) members are included in the member list at all.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: On first load, a committee member sees one row per in-scope member with name and aging only, with zero transaction rows visible, in 100% of tested scenarios.
- **SC-002**: Clicking any member's row reveals that member's full historical detail — identical in content to what the report showed before this redesign, only reordered (newest-first transactions) — in 100% of tested scenarios.
- **SC-003**: With the archived-members filter off (default), no archived member appears anywhere in the on-screen list; with it on, archived members appear exactly as they did before this redesign.
- **SC-004**: Print/PDF and CSV exports generated after this redesign contain byte-for-byte the same per-member financial detail as they would have before the redesign (aside from the newest-first transaction ordering and the default archived-member exclusion), verified by comparing exported content against the on-screen expanded detail.

## Assumptions

- "Member Activity Report" in the request refers to the existing **Member Account Summary** report (`member-account-summary`); there is no separately named "Member Activity Report" in the system today.
- "Current aging" in the default view means all four existing aging buckets (Current, 30, 60, 90+ days), not just a single "current" figure, consistent with what the report already computes and displays today.
- Only the Member Account Summary report changes. The shared `ReportViewer` component and `ReportData` model gain new **optional** fields so this capability could be reused by a future report, but the other five existing reports (Income Statement, Trial Balance, Account Register, Member List, Committee Report) are unaffected and continue to render via the existing flat-table path.
- "Migrate to standard grid format" is satisfied for the top-level member list (which now has a stable, well-defined shape suited to `RadzenDataGrid`) using Radzen's built-in master-detail row-expand pattern; the expanded per-member detail (which mixes balance rows, transaction rows, and an aging summary row of differing shapes) keeps its existing table rendering inside the expand panel, since it does not fit `RadzenDataGrid`'s typed-column model any better than it does today.
- Sorting on the member list is by name only (ascending), matching the report's existing default order; native Radzen column-header sorting is not implemented for this grid since its columns render pre-formatted text, not sortable typed properties.
