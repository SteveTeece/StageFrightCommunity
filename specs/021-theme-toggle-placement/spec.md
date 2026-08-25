# Feature Specification: Theme Toggle Placement

**Feature Branch**: `021-theme-toggle-placement`
**Created**: 2026-08-23
**Status**: Draft
**Source**: [Issue #306](https://github.com/SteveTeece/StageFrightCommunity/issues/306) — "[COSMETIC] Move the Theme Selector switch"

## User Scenarios & Testing

### User Story 1 - Reclaim top-bar screen space by relocating the toggle (Priority: P1)

As a user of the app, the theme toggle currently lives in its own dedicated strip above the page content. I want it removed from that strip and placed at the bottom of the sidebar navigation menu instead, so the page content fills more of the available vertical space.

**Why this priority**: This is the core complaint in the issue ("takes up too much screen room where it is") and delivers the primary space-reclaiming value on its own, independent of any resizing.

**Independent Test**: With the toggle relocated and the dedicated top strip removed, load any main page (e.g. Dashboard) and confirm the content area starts higher on the screen — no empty strip remains above it — while the toggle is still visible, clickable, and functional from its new position at the bottom of the sidebar.

**Acceptance Scenarios**:

1. **Given** the app is showing any main page (not Setup), **When** the page loads, **Then** the theme toggle appears anchored at the bottom of the sidebar menu, not in a separate strip above the page content.
2. **Given** the theme toggle is in the sidebar, **When** the user scrolls a long navigation menu, **Then** the toggle stays fixed at the bottom of the sidebar and is never scrolled out of view.
3. **Given** the user is on the Setup Wizard screen, **When** the page renders, **Then** no theme toggle appears in the sidebar, matching today's behavior (the Setup Wizard keeps its own separate theme control).
4. **Given** the toggle is at its new location, **When** the user clicks it, **Then** the app's theme switches between light and dark exactly as it did before the move.
5. **Given** the toggle has moved into the sidebar, **When** any main page renders, **Then** no empty strip remains reserving space above the page content.

### User Story 2 - Shrink the toggle's footprint (Priority: P2)

As a user, I want the theme toggle control itself to be visibly smaller — at least half its current size — so it takes up less room even in its new location.

**Why this priority**: Addresses the second half of the issue ("reduce the size... by at least 50%"). It stands on its own — a smaller control is less obtrusive regardless of where it sits — but ranks below relocation because relocation is what reclaims the dedicated strip, the larger space win.

**Independent Test**: Compare the rendered footprint (width × height) of the toggle control before and after the change; confirm the new control's footprint is at most half the prior control's, and that clicking it still toggles the theme correctly.

**Acceptance Scenarios**:

1. **Given** the theme toggle previously occupied a pill-shaped control of a known size, **When** the updated control renders, **Then** its rendered footprint is at most 50% of the original.
2. **Given** the smaller control is rendered, **When** the user looks at it, **Then** it's still clear which theme is active and the clickable target remains large enough to activate with a single click.
3. **Given** the smaller control renders in either theme, **When** light or dark mode is active, **Then** the control's own visual state (icon, label, or switch position) still clearly conveys the currently active theme.

### Edge Cases

- Sidebar has many top-level items plus expanded sub-groups (long menu list) — the toggle must remain pinned at the bottom, never overlapping or being pushed off-screen by menu content.
- Very short window heights — the toggle must not get clipped or overlap the last navigation item; the sidebar's own scrollable menu list must yield space to the fixed toggle.
- Accessibility: the accessible name announcing the current theme and the toggle action ("Switch to dark mode" / "Switch to light mode") must still be present and correct at the smaller size and new position.
- The Setup Wizard route continues to hide the sidebar toggle exactly as before — no regression to the existing exception.
- Rapid repeated clicks on the smaller control must still toggle correctly each time — no dead zone from the reduced hit area.

## Requirements

### Functional Requirements

- **FR-001**: The system MUST render the theme toggle control anchored at the bottom of the sidebar navigation menu on every route where it currently appears (every route except Setup).
- **FR-002**: The system MUST remove the separate strip that previously existed solely to host the theme toggle, so no dedicated space above the main content area is reserved for it.
- **FR-003**: The theme toggle MUST remain fixed at the bottom of the sidebar regardless of how many navigation items are present or whether the sidebar's own menu list is scrolled.
- **FR-004**: The system MUST continue to hide the theme toggle entirely on the Setup Wizard route, unchanged from current behavior.
- **FR-005**: Clicking the theme toggle MUST continue to switch the application theme between light and dark, matching its current toggling behavior.
- **FR-006**: The rendered footprint (bounding box) of the theme toggle control MUST be at most 50% of its current size.
- **FR-007**: The theme toggle MUST continue to visually and textually indicate which theme is currently active, at its new size and position.
- **FR-008**: The theme toggle MUST retain an accessible name describing the current theme and the action it performs, at its new size and position.
- **FR-009**: The theme toggle's clickable target MUST remain large enough to reliably activate with a single click despite the reduced visual size.

## Success Criteria

### Measurable Outcomes

- **SC-001**: 100% of main-app routes (every route except Setup) show the theme toggle anchored at the bottom of the sidebar menu instead of in a separate top strip.
- **SC-002**: The vertical screen space previously reserved for the strip that housed only the toggle is fully reclaimed by page content on every main route.
- **SC-003**: The theme toggle's rendered footprint is reduced by at least 50% compared to its pre-change size.
- **SC-004**: Every existing theme-toggle behavior (switching light/dark, showing the current theme, hidden on Setup) continues to work with zero regressions, verified by the automated test suite.
- **SC-005**: A user can identify and successfully activate the theme toggle in a single click, at its new size and location, without hunting for it.

## Assumptions

- "Menu bar" in the issue refers to the app's sidebar navigation menu (the only navigation "menu" in the layout), not the separate top strip the toggle currently occupies — relocating it there is the natural reading of "move to the bottom of the menu bar."
- The 50% size reduction target applies to the whole toggle control's rendered footprint (label text plus switch together), not just the switch element in isolation.
- The clickable/tappable target stays at or above common accessibility minimums even as the visual control shrinks — the hit area may extend slightly beyond the visible control if a literal 50% shrink would otherwise fall below a usable click target.
- Only the toggle's placement and size change; the underlying theme-persistence and light/dark switching behavior is unchanged.

## Approach

Pure layout/CSS relocation inside the existing `ShellLayout` — no new components, services, or data.

- **[src/StageFright.UI/Layout/ShellLayout.razor](../../src/StageFright.UI/Layout/ShellLayout.razor)** — move the `.btn-theme-toggle` block (label + `RadzenSwitch`) out of `<header class="shell-topbar">` and into the bottom of `<nav class="shell-sidebar">`, right after `.sidebar-list`; delete the now-empty `<header class="shell-topbar">` element. The `CurrentPath != "/setup"` guard moves with the markup, so the Setup-route hide behavior is preserved unchanged.
- **[src/StageFright.App/wwwroot/app.css](../../src/StageFright.App/wwwroot/app.css)** — remove the now-unused `.shell-topbar` rule and reclaim its space in `.shell-content`'s top padding; add a sidebar-pinned rule for the relocated toggle (e.g. `margin-top: auto` inside the sidebar's flex column, separate from the scrollable `.sidebar-list`); shrink `.btn-theme-toggle`'s padding/gap/font-size and the `RadzenSwitch` itself so the rendered footprint is ≥50% smaller, while keeping the click target usable (pad the hit area past the visible control if needed).
- **[tests/StageFright.UI.Tests/Layout/ShellLayoutTests.cs](../../tests/StageFright.UI.Tests/Layout/ShellLayoutTests.cs)** — update the three existing theme-toggle tests to locate `.btn-theme-toggle` inside `.shell-sidebar` instead of the removed top bar; behavior assertions (Light/Dark text, click-to-switch, hidden on `/setup`) stay the same.
- Manual verification in the running MAUI app (per the `run` skill's CDP/DPI-aware screenshot workflow) confirms the toggle renders pinned at the sidebar bottom, visibly smaller, and still toggles correctly with both a short and a long navigation menu.
- No dependencies beyond what's already wired (`RadzenSwitch`, `ThemeProvider`); no data-model, migration, or cross-module impact.
