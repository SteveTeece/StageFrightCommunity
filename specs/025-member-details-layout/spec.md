# Feature Specification: Member Details View — Compact Two-Column Layout

**Feature Branch**: `025-member-details-layout`
**Source**: GitHub issue [#305](https://github.com/SteveTeece/StageFrightCommunity/issues/305) — "[COSMETIC] Change layout of Member Details view"
**Status**: Draft

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Compact two-column basic details (Priority: P1)

A committee member opens a member's detail page to check their info. Currently the basic details (Address, Phone, Email, Join Date, Date of Birth, Age, Status) render as a single stacked column, taking up significant vertical space before the Fee Payment History grid even starts. The basic details block should be reorganized into two columns with related fields grouped together, so the same information takes noticeably less vertical space.

**Why this priority**: This is the layout change the issue explicitly requests, and it is the direct cause of the Fees Paid grid not fitting on screen — fixing this is a prerequisite to the second story.

**Independent Test**: Open any member's detail page and visually confirm the basic details render as two columns instead of one, with contact-related fields grouped separately from membership-related fields, and confirm the block's total height is less than before.

**Acceptance Scenarios**:

1. **Given** a user opens a member's detail page, **When** the basic details section renders, **Then** the fields appear in two side-by-side columns rather than one long stacked list.
2. **Given** the two-column layout, **When** inspecting field placement, **Then** contact fields (Address, Phone, Email) are grouped together in one column and membership fields (Join Date, Date of Birth, Age, Status) are grouped together in the other.
3. **Given** a member with no Phone, Email, or Date of Birth on file, **When** the page renders, **Then** those rows are omitted exactly as they are today (conditional fields still only appear when populated), and the remaining rows still lay out cleanly across the two columns.
4. **Given** the reorganized layout, **When** compared to the current single-column layout, **Then** the basic details block occupies fewer vertical rows on screen.

### User Story 2 - Fee Payment History grid fits without scrolling (Priority: P1)

Once the basic details take up less vertical space, a committee member should be able to see the Fee Payment History ("Fees Paid") grid — including its page of rows and pager — without the browser window needing to scroll vertically, on a typical desktop screen.

**Why this priority**: This is the explicit outcome the issue asks for, and it depends on Story 1 freeing up vertical space; if the grid's page size still causes overflow after the reflow, this story further reduces the grid's rows-per-page.

**Independent Test**: Open a member's detail page for a member with several fee records, on a standard desktop browser window, and confirm the entire page — header, two-column details, and one page of the Fee Payment History grid — is visible without a vertical scrollbar.

**Acceptance Scenarios**:

1. **Given** a member with more fee records than fit in one grid page, **When** their detail page renders on a standard desktop window, **Then** the page displays without a vertical scrollbar.
2. **Given** the grid's page size is reduced to achieve this, **When** a member has more fee records than one page, **Then** the existing grid pager still lets the user page through the remaining records.
3. **Given** a member with committee history entries in addition to fee records, **When** the page renders, **Then** the Fee Payment History grid still targets fitting on screen; additional sections (e.g. a long Committee History list) are outside this issue's scope and may still require scrolling.

## Edge Cases

- A member with zero fee records: the Fee Payment History section is omitted entirely (existing behavior, unchanged) — no grid to fit.
- A member with only one or two conditional detail fields populated (e.g. no Phone/Email/DOB): the two-column layout must not leave one column visually empty or unbalanced-looking.
- Narrow window widths: the two-column layout must not overflow or become illegible when the window is narrower than a typical desktop width (existing responsive column behavior should degrade to single-column, consistent with the rest of the app).
- The change must remain legible and usable in both the light and dark theme.

## Requirements

### Functional Requirements

- **FR-001**: The Member Details view's basic details section MUST render its fields in two columns instead of one.
- **FR-002**: The two columns MUST group related fields together: contact information (Address, Phone, Email) in one column, and membership information (Join Date, Date of Birth, Age, Status) in the other.
- **FR-003**: Fields that are conditionally shown today (Phone, Email, Date of Birth, Age — only when the member has data for them) MUST continue to be conditionally shown, unchanged, in the new layout.
- **FR-004**: The reorganized basic details section MUST occupy fewer vertical rows than the current single-column layout.
- **FR-005**: The Fee Payment History grid MUST render with a page size low enough that, combined with the reduced-height details section, a member's detail page displays without a vertical scrollbar on a standard desktop browser window.
- **FR-006**: The Fee Payment History grid MUST keep its existing paging control so members with more fee records than one page can still view all of their history.
- **FR-007**: The reorganization MUST be presentation-only — no change to what data is loaded, how Edit/Activate/Inactivate/Archive actions behave, or how fee records are computed.
- **FR-008**: The reorganized layout MUST remain legible and usable in both the light and dark theme.

## Success Criteria

### Measurable Outcomes

- **SC-001**: On a member's detail page, the basic details fields render as two visually distinct columns rather than a single stacked column.
- **SC-002**: The basic details block's total height, measured in field rows tall, is reduced by roughly half compared to the current layout (up to 7 rows stacked in one column becomes at most 4 rows tall across two columns).
- **SC-003**: On a standard desktop browser window (1366×768 or larger), a member's detail page with a full page of fee records displays without requiring vertical scrolling to see the Fee Payment History grid and its pager.
- **SC-004**: 100% of existing automated tests covering the Member Details page continue to pass unchanged, confirming field visibility rules and other behavior are unaffected.

## Assumptions

- "Logical field groupings" (from the issue) means splitting the current single list into a contact-information column (Address, Phone, Email) and a membership-information column (Join Date, Date of Birth, Age, Status) — the most natural grouping of the existing seven fields, needing no further clarification.
- The Committee History and Fee Payment History sections below the basic details block are unaffected by this issue except that the Fee Payment History grid's page size may be reduced; Committee History's own layout is out of scope.
- A precedent for a smaller-than-default grid page size already exists in this codebase (`CommitteeSettingsTab`, one grid in `EventTypesTab` use `PageSize="10"` instead of the project-wide default of 15), so reducing this grid's page size below 15 is consistent with existing practice, not a new pattern.
- "Standard desktop browser window" is interpreted as the common minimum desktop resolution (1366×768), consistent with how the app is normally run in the MAUI shell; no minimum window size is currently enforced elsewhere in the app, so this is a reasonable baseline rather than a guarantee for every possible window size.

## Approach

- `src/StageFright.UI/Pages/Members/MemberDetail.razor` — replace the single `col-md-6` `<dl class="row">` block with two side-by-side columns (two `col-md-6` `<dl>` blocks inside the existing `row g-2`): left column for Address/Phone/Email, right column for Join Date/Date of Birth/Age/Status. Keep each field's existing conditional `@if` guard exactly as-is so FR-003 holds.
- Same file — reduce the Fee Payment History `RadzenDataGrid`'s `PageSize` from `15` to a smaller value so the grid's default page of rows plus its pager fits below the now-shorter details block without pushing the page past a standard 1366×768 viewport (FR-005). Exact value to be confirmed by manual visual verification (see tasks), following the existing `PageSize="10"` precedent in `CommitteeSettingsTab`/`EventTypesTab`.
- `CLAUDE.md`'s "Data grid standards" section documents `PageSize="15"` as the reference convention (per the Members grid); since this grid becomes a second precedent for a smaller, space-driven exception, add one sentence there alongside the existing `CommitteeSettingsTab`/`EventTypesTab` mention so the convention doc stays accurate.
- `tests/StageFright.UI.Tests/Pages/Members/MemberDetailTests.cs` — existing tests locate fields by `dt`/`section` content, not by column position, so they should continue to pass unchanged; re-run to confirm (SC-004). No new automated test is planned for "fits without scrolling," since that is a rendered-viewport measurement — verify manually in the running app per the project's MAUI visual-verification approach.
- Manual verification: launch the app, open a member with several fee payments and a full basic-details set, and confirm both columns render correctly and the page fits without scrolling, in both light and dark theme.
