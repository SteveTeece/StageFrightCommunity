# Feature Specification: Outstanding Balances Dashboard Tile

**Feature Branch**: `004-outstanding-balances-tile`

**Created**: 2026-07-11

**Status**: Draft

**Input**: User description: "Add a new dashboard tile to the Finance module (the module may define more than one dashboard tile) that displays outstanding balances for unpaid fees. The tile must show: a count of members with outstanding fees, the total value of outstanding attendance fees, and the total value of outstanding annual fees. The tile must also include a chart plotting outstanding balances across the calendar year."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Committee member checks who still owes fees (Priority: P1)

A committee member responsible for finances opens the dashboard and needs an at-a-glance answer to "how much money is still owed to the organisation, and by how many members" without navigating into reports.

**Why this priority**: This is the core value of the tile — a single glance answers the most common finance question committee members ask. Without this, the tile has no reason to exist.

**Independent Test**: Can be fully tested by seeding several members with unpaid annual and/or attendance fees, loading the dashboard, and confirming the tile shows the correct member count and the correct outstanding totals for each fee type.

**Acceptance Scenarios**:

1. **Given** three members each have an outstanding annual fee and two members each have an outstanding attendance fee, **When** the dashboard loads, **Then** the tile shows a member count that reflects the distinct members with any outstanding balance, the total outstanding attendance fee value, and the total outstanding annual fee value.
2. **Given** no member currently has any outstanding fee, **When** the dashboard loads, **Then** the tile shows a member count of zero and both outstanding totals as zero, rather than hiding the tile or showing an error.
3. **Given** a member has partially paid an annual fee, **When** the dashboard loads, **Then** the tile's outstanding annual fee total reflects only the remaining unpaid balance for that fee, not the original fee amount.

---

### User Story 2 - Committee member spots the trend across the year (Priority: P2)

A committee member wants to understand whether outstanding balances are growing or shrinking over the course of the current calendar year, to judge whether collection efforts are working or membership renewal timing is causing a spike.

**Why this priority**: Adds trend context on top of the point-in-time totals from User Story 1. Valuable, but the tile is still useful without it, so it is secondary.

**Independent Test**: Can be fully tested by seeding fee and payment activity across multiple months of the current calendar year, loading the dashboard, and confirming the chart plots an outstanding-balance data point for each month of the year to date.

**Acceptance Scenarios**:

1. **Given** outstanding balances existed in January and were fully paid off by June of the current year, **When** the dashboard loads, **Then** the chart shows higher outstanding values in the early months and a decline toward June, reflecting the calendar-year trend.
2. **Given** the current date is partway through the calendar year, **When** the dashboard loads, **Then** the chart shows data points only for months from the start of the calendar year up to and including the current month, with no data plotted for future months.

---

### User Story 3 - Committee member drills from the tile into full detail (Priority: P3)

A committee member sees a concerning total on the tile and wants to move directly into the existing outstanding-balance detail (e.g. a member account summary or GL report) to see which specific members owe money.

**Why this priority**: Convenience/navigation shortcut. Nice to have for workflow efficiency, but the tile still delivers its core value (visibility) without this affordance, consistent with how other dashboard tiles in this system behave.

**Independent Test**: Can be tested by clicking/activating the tile and confirming the user is taken to the relevant existing finance detail view.

**Acceptance Scenarios**:

1. **Given** the tile is displayed on the dashboard, **When** the committee member selects the tile, **Then** the system navigates them to the existing detail view where outstanding member balances can be examined further.

---

### Edge Cases

- What happens when there is no fee or payment data at all yet (e.g. immediately after first-run setup)? The tile must render with zero values, not fail to load or show a blank/broken tile.
- How does the tile handle a member who has an outstanding balance that spans both fee types (owes on both their annual fee and an attendance fee)? They must be counted once in the member count while still contributing correctly to each separate fee-type total.
- How does the chart handle the very first month of the calendar year (January) when there is no prior-year data point to compare against? It must plot January using only that month's own data, without requiring a prior data point.
- What happens if a member's fee was fully written off or reversed via a correcting entry rather than a normal payment? The outstanding total must reflect the corrected (net) balance, not the original uncorrected fee amount.
- How does the tile behave for an organisation with a very large number of overdue members? The tile must remain readable (e.g. a count/summary figure rather than an unbounded list of member names).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The Finance module MUST provide a dashboard tile, in addition to any dashboard tiles it already provides, dedicated to outstanding (unpaid) member fee balances.
- **FR-002**: The tile MUST display a count of the distinct members who currently have any outstanding fee balance.
- **FR-003**: The tile MUST display the total value of outstanding attendance fees across all members.
- **FR-004**: The tile MUST display the total value of outstanding annual fees across all members.
- **FR-005**: An "outstanding" balance for a given fee MUST be calculated as the fee's original amount less any payments and corrections applied against it, consistent with how outstanding balances are calculated elsewhere in the system (net of debits and credits, never the raw uncorrected fee amount).
- **FR-006**: The tile MUST include a chart plotting outstanding balances across the current calendar year, showing how the outstanding total has changed over the months of the year to date.
- **FR-007**: The chart MUST only plot months from the start of the current calendar year through the current month; it MUST NOT plot future months.
- **FR-008**: When no members currently have any outstanding balance, the tile MUST display a member count of zero and outstanding totals of zero rather than omitting itself or displaying an error state.
- **FR-009**: The member count (FR-002) MUST count a member once even if that member has outstanding balances on both an annual fee and an attendance fee simultaneously.
- **FR-010**: The tile MUST load and refresh using the same mechanism, look, and placement conventions as other dashboard tiles in the system, so it behaves consistently with existing tiles (e.g. Cash Flow, Finance summary) already on the dashboard.
- **FR-011**: Selecting the tile MUST take the committee member to the existing view where outstanding member balances can be examined in further detail.

### Key Entities

- **Outstanding Fee Balance**: A derived, point-in-time figure representing the unpaid portion of one or more fees owed by a member — not a stored entity itself, but a calculation over existing fee, payment, and correction records, broken down by fee type (attendance vs. annual).
- **Member (with outstanding balance)**: An existing member who has at least one fee (attendance and/or annual) with a nonzero outstanding balance; contributes to the tile's member count exactly once regardless of how many fee types they owe on.
- **Calendar-Year Outstanding Trend Point**: A derived data point representing the total outstanding balance across all members as of a given month within the current calendar year, used to plot the tile's chart.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A committee member can determine the total number of members with outstanding fees and the outstanding value for each fee type within 5 seconds of the dashboard finishing loading, without navigating away from the dashboard.
- **SC-002**: The tile's displayed totals match the organisation's authoritative outstanding-balance figures (as verifiable against existing finance reports) with 100% accuracy for any given point in time.
- **SC-003**: The calendar-year chart correctly reflects known month-by-month outstanding balance changes in 100% of tested scenarios, including the zero-data and partial-year (current month mid-year) cases.
- **SC-004**: The tile renders correctly (zero-state, not an error) for an organisation with no outstanding balances at all, in 100% of tested scenarios.

## Assumptions

- "Outstanding balances" refers only to unpaid **Fee** amounts owed by members (annual and attendance fees), not other GL account balances unrelated to member fees.
- "Calendar year" means the current calendar year (January through December of the current year), not the organisation's membership/fiscal renewal year, since those may differ and the request specifically says "calendar year."
- The chart granularity is monthly, matching the monthly granularity already used elsewhere in this system's reporting (e.g. month-name dropdowns, trend charts).
- The tile is read-only/informational; it does not provide inline actions to record a payment or write off a balance — those remain in the existing Finance workflows the tile links out to (User Story 3).
- The existing "member account summary" or equivalent finance detail view is the appropriate drill-down target for User Story 3; no new detail screen is being introduced by this feature.
- This feature only concerns the dashboard tile's display; it does not change how fees, payments, or corrections are recorded or calculated elsewhere in the system.
