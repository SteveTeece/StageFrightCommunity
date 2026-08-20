# Feature Specification: Setup Wizard Tabbed Redesign

**Feature Branch**: `017-setup-wizard-tabs`
**Created**: 2026-08-21
**Status**: Draft
**Source**: GitHub issue [#299](https://github.com/SteveTeece/StageFrightCommunity/issues/299) — "[QUALITY] Refactor startup wizard"

## User Scenarios & Testing

### User Story 1 - Complete first-run setup from one tabbed screen (Priority: P1)

A coordinator setting up StageFright Community for the first time no longer clicks through a linear sequence of near-empty screens. Instead they see one setup screen with a row of tabs — each tab holding a logically related group of settings — and can either click a tab directly or use a "Next" button that advances to the following tab. Tabs that would otherwise hold only one or two settings are folded into a related tab so no tab feels sparse.

**Why this priority**: This is the core complaint in the issue ("too many screens") and the structural change every other part of this feature builds on. Without it, none of the other asks (theme dropdown, role widget, accounts tab, review tab) have a home.

**Independent Test**: Launch the app with no Settings record present, walk through every tab of the wizard using both direct tab clicks and the Next button, fill in the required fields, and confirm setup completes and lands on the dashboard — without ever seeing more than one screen shell.

**Acceptance Scenarios**:

1. **Given** the app has never been set up, **When** the setup wizard loads, **Then** it renders as a single screen with a tab strip in the same visual style as the Finance screen's tabs, not a sequence of full-page steps.
2. **Given** the wizard is open on any tab, **When** the user clicks a different tab's header, **Then** that tab's content is shown immediately without losing values already entered on other tabs.
3. **Given** the wizard is open on a tab that is not the last one, **When** the user clicks "Next", **Then** the wizard advances to the following tab in the defined tab order.
4. **Given** the settings from the previous five-step wizard, **When** they are regrouped into tabs, **Then** no tab holds only one or two settings — each tab's settings are grouped by logical relationship (e.g. organisation identity sits with appearance, not alone).
5. **Given** the user has filled in all required fields across every tab, **When** they finish from the last (review) tab, **Then** setup completes exactly as it does today — the Settings record is created and the user is routed to the dashboard.

---

### User Story 2 - Review every setting before finishing setup (Priority: P2)

Before finishing setup, the coordinator can open a final review tab that lists every value entered across all the other tabs in one place, including the committee roles and chart-of-accounts entries added during setup shown as list boxes, so they can catch a mistake before it's persisted.

**Why this priority**: A pre-finish summary already exists today in a simpler form; this story upgrades it to reflect the new tab groupings and the two new list-based entry patterns. It depends on User Story 1's tab structure and on User Story 3/4's list-based widgets being in place to summarize.

**Independent Test**: Fill in values across every tab (including adding at least one committee role and one account), open the review tab, and confirm every entered value — including both added lists — is visible without navigating back to another tab.

**Acceptance Scenarios**:

1. **Given** values have been entered on every other tab, **When** the user opens the review tab, **Then** every setting's current value is displayed read-only, grouped in a way that's easy to scan.
2. **Given** one or more committee roles have been added, **When** the review tab is shown, **Then** the added roles are displayed in a list box, not as a comma-separated string.
3. **Given** one or more accounts have been added on the chart-of-accounts tab, **When** the review tab is shown, **Then** the added accounts are displayed in a list box.
4. **Given** the review tab is the last tab, **When** the user clicks "Finish", **Then** setup is submitted using the values currently shown on the review tab.

---

### User Story 3 - Add committee office-holder roles one at a time (Priority: P2)

Instead of typing a comma-separated list of extra committee role titles into a single text box, the coordinator types one role name into a field, clicks a "+" button to add it, and sees it appear in a list underneath. They can add as many roles as needed and remove one they added by mistake before finishing setup.

**Why this priority**: Directly requested in the issue as a usability improvement over the comma-separated list; independent of the tab restructuring itself but naturally lives inside the committee tab from User Story 1.

**Independent Test**: On the committee tab, add two role titles one at a time via the "+" button, confirm both appear in the list under the entry field, remove one, and confirm only the remaining one is submitted with setup.

**Acceptance Scenarios**:

1. **Given** the committee tab is open, **When** the user types a role title and clicks "+", **Then** the title appears in a list below the entry field and the entry field clears for the next title.
2. **Given** the entry field is empty or only whitespace, **When** the user clicks "+", **Then** no entry is added.
3. **Given** a role title already appears in the list (case-insensitive match), **When** the user tries to add it again, **Then** the duplicate is rejected and the existing entry is not duplicated.
4. **Given** one or more roles have been added, **When** the user removes one from the list, **Then** it no longer appears in the list and is not included when setup is submitted.
5. **Given** no roles have been added, **When** setup is submitted, **Then** setup completes with no additional office-holder titles, exactly as it does today when the field is left blank.

---

### User Story 4 - Add Chart of Accounts entries during setup (Priority: P2)

The coordinator can add general ledger accounts to the Chart of Accounts from a dedicated tab during first-run setup, using the same add-account control (name, type, and bank/cash flag) that's used everywhere else in the app, instead of having to finish setup first and find the Chart of Accounts page separately.

**Why this priority**: A genuinely new capability (setup previously offered no way to add accounts), valuable but not blocking — the app already seeds default system accounts, so a coordinator can still finish setup and add accounts afterward from Finance ▸ Chart of Accounts if they skip this tab.

**Independent Test**: On the chart-of-accounts tab, add a new account with a name, type, and (for an Asset account) the bank/cash flag, confirm it appears in the tab's account list immediately, and confirm it's visible later in Finance ▸ Chart of Accounts after setup completes.

**Acceptance Scenarios**:

1. **Given** the chart-of-accounts tab is open, **When** the user enters a valid account name and type and submits, **Then** the account is created immediately (the same way the standalone Chart of Accounts page creates one) and appears in the tab's list of accounts added so far.
2. **Given** an account name that already exists, **When** the user tries to add it, **Then** the same validation error shown on the standalone Chart of Accounts page is shown here, and no duplicate is created.
3. **Given** the account type selected is Asset, **When** the add-account form is shown, **Then** the bank/cash account checkbox is available, matching the standalone page's behavior.
4. **Given** no accounts are added on this tab, **When** setup is completed, **Then** setup still completes normally with only the system default accounts present.
5. **Given** accounts were added on this tab but the coordinator closes the app before finishing setup, **When** the app is relaunched, **Then** it returns to the setup wizard (setup is still incomplete) and the previously added accounts still exist and are not recreated.

---

### User Story 5 - Use dropdowns and checkboxes consistent with the rest of the wizard (Priority: P3)

The theme is chosen from a dropdown selector instead of a toggle switch, and every other yes/no setting in the wizard (e.g. whether sales tax applies) uses a checkbox rather than a toggle switch.

**Why this priority**: A cosmetic control-style change called out explicitly in the issue. Lowest risk and lowest value on its own — it doesn't change what data is captured, only how it's captured — so it's the first thing that could be deferred if scope needs to shrink.

**Independent Test**: Open the wizard, confirm the theme control is a dropdown offering Light and Dark, select each option and confirm the wizard's own appearance updates accordingly, and confirm every yes/no setting elsewhere in the wizard renders as a checkbox.

**Acceptance Scenarios**:

1. **Given** the wizard is open, **When** the user views the appearance setting, **Then** it is a dropdown selector listing the available themes, not a toggle switch.
2. **Given** the user selects a different theme from the dropdown, **When** the selection changes, **Then** the wizard's own appearance updates to match immediately.
3. **Given** the wizard contains a yes/no setting (e.g. "sales tax applies to this organisation"), **When** that tab is rendered, **Then** the control is a checkbox, not a toggle switch.

---

### Edge Cases

- A required field on a tab other than the one currently open is left invalid: attempting to finish from the review tab MUST be rejected the same way an invalid submission is rejected today, and the user MUST be able to tell which tab needs attention.
- The user clicks "Next" on a tab whose own required fields aren't valid yet: the wizard does not advance past that tab.
- The user clicks directly on a tab far ahead of the one they're currently completing (skipping tabs via the tab strip rather than Next): this is allowed for navigation, but does not bypass the same overall validation performed at Finish.
- The user switches tabs rapidly: because the wizard doesn't persist anything to the database until Finish is clicked (with the sole exception of accounts added on the Chart of Accounts tab, which persist immediately like they do everywhere else in the app), tab switching itself must not trigger any concurrent database access.
- Sales tax is toggled off after a tax rate and fee tax treatments were entered: the same clearing behavior that exists today (tax fields reset) must still apply regardless of which tab layout is in effect.
- The debug-only "load sample data" checkbox: continues to appear only when a debug seeder is available, unaffected by which tab it now lives on.

## Requirements

### Functional Requirements

- **FR-001**: The setup wizard MUST present its settings as a single screen with a tab strip, styled consistently with the tab component already used on the Finance screen, replacing the current sequence of numbered full-page steps.
- **FR-002**: Each tab MUST group settings that are logically related to each other; no tab MUST contain only one or two settings — such settings MUST be relocated to the most closely related tab.
- **FR-003**: The wizard MUST allow moving between tabs both by clicking a tab's header directly and by clicking a "Next" control that advances to the next tab in the defined order.
- **FR-004**: "Next" MUST NOT advance past a tab whose own required fields fail validation.
- **FR-005**: The last tab MUST be a review tab that displays every value entered across all other tabs in read-only form before the user finishes setup.
- **FR-006**: The review tab MUST display added committee office-holder titles and added chart-of-accounts entries as list boxes rather than as comma-separated or plain text.
- **FR-007**: Finishing setup from the review tab MUST validate all required fields across every tab and MUST refuse to complete setup while any are invalid, consistent with today's all-fields-required-before-finish behavior.
- **FR-008**: The wizard MUST let the user add an additional committee office-holder title by typing its name into an entry field and clicking an "add" ("+") control, rather than by editing a single comma-separated text field.
- **FR-009**: Each added committee office-holder title MUST appear in a list displayed beneath the entry field, and the user MUST be able to remove a previously added title from that list before finishing setup.
- **FR-010**: Adding a committee office-holder title that is blank/whitespace-only, or that duplicates (case-insensitively) a title already added in the same setup session, MUST be rejected without adding a duplicate or empty entry.
- **FR-011**: The wizard MUST include a tab for adding entries to the Chart of Accounts during setup, offering the same fields and immediate-create behavior (name, account type, and — for Asset accounts — the bank/cash flag) as the existing standalone Chart of Accounts page.
- **FR-012**: An account added on the Chart of Accounts tab MUST be created immediately, the same way accounts are created from the standalone Chart of Accounts page, and MUST appear in that tab's list of accounts without requiring setup to be finished first.
- **FR-013**: Adding an account with a name that already exists MUST be rejected with the same validation behavior as the standalone Chart of Accounts page.
- **FR-014**: The wizard MUST NOT require any account to be added on the Chart of Accounts tab in order to finish setup.
- **FR-015**: The theme (appearance) setting in the wizard MUST be presented as a dropdown selector rather than a toggle switch, and selecting a value MUST update the wizard's own appearance immediately.
- **FR-016**: Every yes/no (boolean) setting elsewhere in the wizard, other than the theme selector, MUST be presented as a checkbox rather than a toggle switch.
- **FR-017**: Regrouping settings into tabs MUST NOT change what data first-run setup captures or how it's validated — every field, validation rule, and default value that exists in the current wizard MUST still exist and behave the same way in the tabbed wizard.
- **FR-018**: The debug-only "load sample data" option MUST continue to appear only when a debug data seeder is available (never in a release build), regardless of which tab hosts it.

## Key Entities

- **Committee office-holder title (in-session list)**: A role name the coordinator adds during setup, held only in the wizard's own in-progress state until Finish, at which point the full set is submitted together — same underlying data as today's comma-separated field, only the entry/display mechanism changes.
- **Chart of Accounts entry**: A general ledger account (name, account type, and — for Asset accounts — whether it's a bank/cash account) created immediately when added on the new tab, using the same account-creation behavior as the standalone Chart of Accounts page.

## Success Criteria

### Measurable Outcomes

- **SC-001**: A first-time coordinator can complete setup by visiting every tab exactly once and clicking Finish, without the wizard ever showing more than one screen shell (tab strip + content area).
- **SC-002**: No tab in the redesigned wizard contains fewer than three related settings, except the review tab and the Chart of Accounts tab (which are exempt because their content is inherently a summary/list rather than a fixed set of settings).
- **SC-003**: 100% of the fields, validations, and default values present in the current five-step wizard are still present and enforced after the redesign (no silent loss of setup capability).
- **SC-004**: A coordinator can add at least five committee office-holder titles and remove any of them before finishing setup, with no duplicate or blank entries ever appearing in the list.
- **SC-005**: A coordinator can add at least one Chart of Accounts entry during setup and see it appear in the standalone Chart of Accounts page (Finance ▸ Chart of Accounts) after setup completes, with no duplication.

## Assumptions

- "Tabs should look like the ones in the Finance screen" refers to the `Tabs`/`Tab` Blazor component already used by `FinancePage.razor`; the setup wizard's tabs are built with that same component for visual and behavioral consistency.
- The exact tab names and final grouping (e.g. "General", "Membership & Fees", "Sales Tax", "Committee", "Chart of Accounts", "Review") are left to the planning step to finalize, as long as no tab holds only one or two settings and the grouping is logical — the issue asks for a grouping strategy, not exact tab names.
- The theme dropdown offers the two themes the application currently supports (Light and Dark); if more themes are added later, the dropdown grows to match without further spec changes.
- "List boxes" for the review tab's committee-role and account summaries means a simple read-only list display (e.g. a bulleted or bordered list), not an interactive multi-select control — the user isn't selecting from these lists, only reviewing what they already entered.
- Accounts added on the Chart of Accounts tab persist immediately (matching the existing standalone page's behavior) rather than being deferred until Finish like the rest of setup's fields — the issue asks to reuse the existing add-account control as-is, and that control already creates accounts immediately.
- This feature only changes the `/setup` first-run wizard. The Settings page's own General tab (which also currently uses a theme toggle switch) is out of scope — the issue's wording specifically calls out "the setup wizard."
