# Feature Specification: Report Action Buttons — Separated Pill Style

**Feature Branch**: `024-report-action-pills`
**Source**: GitHub issue [#304](https://github.com/SteveTeece/StageFrightCommunity/issues/304) — "[COSMETIC] Change pills for Print/Export/Refresh on Reports screens"
**Status**: Draft

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Distinct, separated action buttons on every report (Priority: P1)

A committee member opens any report screen (Income Statement, Trial Balance, Member List, and so on) to print, export, or refresh the report. Today the Print / PDF, Export CSV, and Refresh buttons are fused into one bordered strip with no space between them, so they read as a single control rather than three separate actions. The buttons should instead read as clearly separate, individually shaped buttons — spaced apart, without a border boxing them in — matching the look of the tab selector already used on the Finance screen.

**Why this priority**: This is the entire scope of the issue and the only user-facing change requested — there is no smaller independently valuable slice.

**Independent Test**: Open any one report (e.g. Income Statement) and visually confirm the three action buttons are spaced apart, unbordered in their default state, and styled like the Finance screen's tab bar. Can be verified without touching any other report.

**Acceptance Scenarios**:

1. **Given** a user opens any report in the Report Viewer, **When** the report renders, **Then** the Print / PDF, Export CSV, and Refresh buttons are shown with visible spacing between each button, so each reads as a distinct button.
2. **Given** the same report view, **When** inspecting the button group in its default (non-hover, non-focus) state, **Then** no border line is visible around the individual buttons or around the group as a whole.
3. **Given** a user compares the Reports action bar to the Finance screen's tab selector (Outstanding | Record Income | Record Expense | Apply Annual Fees), **When** viewing both, **Then** the pill shape, spacing rhythm, and background/hover treatment visually match.
4. **Given** a user hovers over one of the three buttons, **When** the pointer is over the button, **Then** the button shows a hover treatment consistent with the Finance tab selector's hover state (no border appears on hover).
5. **Given** the change is applied, **When** any of the ten reports (Income Statement, Trial Balance, Account Register, Member Account Summary, Member List, Committee, Balance Sheet, Bank Reconciliation, Tax Summary, General Ledger) is opened, **Then** all show the same restyled action bar, since every report renders through the one shared Report Viewer surface.
6. **Given** a user clicks Print / PDF, Export CSV, or Refresh after the restyle, **When** the click is handled, **Then** the same report-generation, export, and refresh behavior fires as before the change — the restyle is cosmetic only.

## Edge Cases

- Narrow window width: once spacing is added between buttons, the group must not wrap awkwardly or overflow the report header.
- The "Cancel" button shown while a report is generating, and the "Try Again" button shown on a report error, are separate controls rendered in different view states — they are not part of this button group and must not be visually affected by this change.
- The restyle must remain legible and usable in both the light and dark theme (the app has a theme toggle).

## Requirements

### Functional Requirements

- **FR-001**: The report action bar (Print / PDF, Export CSV, Refresh) MUST render each button with visible spacing between it and its neighboring buttons, rather than as a single fused strip.
- **FR-002**: The report action bar's buttons MUST NOT display a border in their default (non-hover, non-focus) state — neither on the individual buttons nor as a border wrapping the group as a whole.
- **FR-003**: The report action bar's overall visual style (pill shape, spacing rhythm, background/hover treatment) MUST match the existing tab-selector style already used on the Finance screen.
- **FR-004**: The restyled action bar MUST apply uniformly to every report screen, since all ten reports render through the one shared Report Viewer surface.
- **FR-005**: The restyle MUST be purely cosmetic — the Print / PDF, Export CSV, and Refresh actions MUST continue to trigger the same behavior as before the change.
- **FR-006**: The restyled buttons MUST remain legible and usable in both the light and dark theme.

## Success Criteria

### Measurable Outcomes

- **SC-001**: On every one of the ten report screens, the Print / PDF, Export CSV, and Refresh buttons render with visible, non-zero spacing between each button.
- **SC-002**: No border renders around the button group or around the individual buttons in their default state, on any report screen.
- **SC-003**: A side-by-side visual comparison of the Reports action bar and the Finance screen's tab selector shows matching pill shape, spacing, and hover treatment.
- **SC-004**: 100% of existing automated tests covering the report action buttons continue to pass unchanged, confirming the click behavior is unaffected.

## Assumptions

- "The tab selector on the Finance screen" refers to the existing `Outstanding | Record Income | Record Expense | Apply Annual Fees` tab bar, which already has a defined pill visual style shared with the Settings page's tabs; this spec asks the report action bar to visually align with that established style rather than introduce a new one.
- Because the action buttons trigger commands (Print, Export, Refresh) rather than switch between panels, they keep their existing button semantics — only their spacing and border look is asked to align with the tab selector, not any active/selected-tab behavior.
- This is a presentation-only change: no click handlers, routes, or report-generation logic change.

## Approach

- `src/StageFright.UI/Shared/ReportViewer.razor` — the report actions container (`Print / PDF`, `Export CSV`, `Refresh`) currently uses the generic `.btn-group` segmented-pill CSS recipe in `app.css`. Swap its styling class to a new, report-scoped class (e.g. `report-actions`); keep `role="group"`/`aria-label="Report actions"` for accessibility. No change to the `PrintReport` / `ExportCsv` / `Regenerate` click bindings (satisfies FR-005).
- `src/StageFright.App/wwwroot/app.css` — add a new rule block for that class, modeled on the existing `.nav-tabs, .nav-pills` glass tab-bar rule (container: padding + gap + rounded background; items: borderless by default, hover/active treatment) so the buttons visually match the Finance tab selector (FR-001, FR-002, FR-003, FR-006). Leave the existing generic `.btn-group` rule untouched, since it is documented as a reusable segmented-pill recipe for other, unrelated screens (e.g. an Active/Inactive filter) and no other screen currently uses `.btn-group` in markup — confirmed by a repo-wide search.
- Since all ten reports render through this one shared `ReportViewer.razor`, the restyle automatically applies everywhere (FR-004) — no per-report changes needed.
- `tests/StageFright.UI.Tests/Shared/ReportViewerTests.cs` locates the buttons by their text content (`"Refresh"`, etc.), not by CSS class, so the existing Print/Export/Refresh behavioral tests need no changes — re-run them to confirm (SC-004).
- Manual verification: open a report in the running app in both light and dark theme and visually compare the action bar against the Finance screen's tab selector (SC-001–SC-003).
