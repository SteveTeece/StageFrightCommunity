# Feature Specification: Setup Wizard Tabbed Redesign

**Feature Branch**: `017-setup-wizard-tabs`
**Created**: 2026-08-21
**Status**: Draft
**Source**: GitHub issue [#299](https://github.com/SteveTeece/StageFrightCommunity/issues/299) — "[QUALITY] Refactor startup wizard"

## User Scenarios & Testing

### User Story 1 - Complete first-run setup from one tabbed screen (Priority: P1)

A coordinator setting up StageFright Community for the first time no longer clicks through a linear sequence of near-empty screens. Instead they see one setup screen with a row of tabs — each tab holding a logically related group of settings — and can either click a tab directly or use a "Next" button that advances to the following tab. Tabs that would otherwise hold only one or two settings are folded into a related tab so no tab feels sparse.

**Why this priority**: This is the core complaint in the issue ("too many screens") and the structural change every other part of this feature builds on. Without it, none of the other asks (opening balances, theme dropdown, role widget, accounts tab, review tab) have a home.

**Independent Test**: Launch the app with no Settings record present, walk through every tab of the wizard using both direct tab clicks and the Next button, fill in the required fields, and confirm setup completes and lands on the dashboard — without ever seeing more than one screen shell.

**Acceptance Scenarios**:

1. **Given** the app has never been set up, **When** the setup wizard loads, **Then** it renders as a single screen with a tab strip in the same visual style as the Finance screen's tabs, not a sequence of full-page steps.
2. **Given** the wizard is open on any tab, **When** the user clicks a different tab's header, **Then** that tab's content is shown immediately without losing values already entered on other tabs.
3. **Given** the wizard is open on a tab that is not the last one, **When** the user clicks "Next", **Then** the wizard advances to the following tab in the defined tab order.
4. **Given** the settings from the previous five-step wizard, **When** they are regrouped into tabs, **Then** no tab holds only one or two settings — each tab's settings are grouped by logical relationship (e.g. organisation identity sits with appearance, not alone).
5. **Given** the user has filled in all required fields across every tab, **When** they finish from the last (review) tab, **Then** setup completes exactly as it does today — the Settings record is created and the user is routed to the dashboard.

---

### User Story 2 - Enter opening balances before finishing setup (Priority: P1)

Before setup can be finished, the coordinator visits a dedicated tab listing every account that will exist once setup completes and enters each one's starting balance as at a chosen date — the same experience as the existing standalone Opening Balances page. Finishing setup without doing this is blocked, unless the coordinator has instead chosen to load sample data, in which case the seeded data supplies its own realistic opening balances and manual entry isn't required.

**Why this priority**: An organisation's ledger is meaningless without correct starting balances, so this is treated as a hard requirement of a complete setup, not an optional nicety — on the same footing as User Story 1's structural change. It depends on User Story 1's tabs and on User Story 4's Chart of Accounts tab (a queued account needs a row here too), and its "unless sample data is loaded" exception depends on User Story 5's data-seed checkbox.

**Independent Test**: On the Opening Balances tab, enter a non-zero balance for at least one account, finish setup, and confirm the resulting opening-balance ledger entry exists (Finance ▸ Trial Balance / General Ledger) with the entered amounts afterward. Separately, confirm Finish is rejected if this tab is left untouched and "load sample data" is not selected, and confirm Finish succeeds with no manual entry when "load sample data" is selected instead.

**Acceptance Scenarios**:

1. **Given** the Opening Balances tab is open, **When** the coordinator enters a balance for at least one account and finishes setup, **Then** those balances are posted as the initial opening-balance ledger entry once setup completes, using the same posting behavior (paired debits/credits, automatic Opening Balance Equity plug) as the standalone Opening Balances page.
2. **Given** no balance has been entered on this tab and "load sample data" is not selected, **When** the coordinator tries to finish setup, **Then** Finish is rejected and the coordinator can tell the Opening Balances tab needs attention.
3. **Given** "load sample data" is selected on the review tab, **When** the coordinator finishes setup without entering any opening balances, **Then** setup completes normally — the seeded sample data supplies its own opening balances instead.
4. **Given** the coordinator queues a new account on the Chart of Accounts tab, **When** they open the Opening Balances tab, **Then** that account appears as a new row ready for a balance to be entered.
5. **Given** the coordinator removes a queued account from the Chart of Accounts tab after already entering a balance for it, **When** they return to the Opening Balances tab, **Then** that account's row, and any balance entered for it, is gone.

---

### User Story 3 - Review every setting before finishing setup (Priority: P2)

Before finishing setup, the coordinator can open a final review tab that lists every value entered across all the other tabs in one place, including the committee roles and chart-of-accounts entries added during setup shown as bordered list boxes, so they can catch a mistake before it's persisted.

**Why this priority**: A pre-finish summary already exists today in a simpler form; this story upgrades it to reflect the new tab groupings and the new list-based entry patterns. It depends on User Story 1's tab structure and on User Story 4/6's list-based widgets being in place to summarize.

**Independent Test**: Fill in values across every tab (including adding at least one committee role and one account), open the review tab, and confirm every entered value — including both added lists — is visible without navigating back to another tab.

**Acceptance Scenarios**:

1. **Given** values have been entered on every other tab, **When** the user opens the review tab, **Then** every setting's current value is displayed read-only, grouped in a way that's easy to scan.
2. **Given** one or more committee roles have been added, **When** the review tab is shown, **Then** the added roles are displayed as a bordered list box, not as a comma-separated string.
3. **Given** one or more accounts have been added on the chart-of-accounts tab, **When** the review tab is shown, **Then** the added accounts are displayed as a bordered list box.
4. **Given** the review tab is the last tab, **When** the user clicks "Finish", **Then** setup is submitted using the values currently shown on the review tab.

---

### User Story 4 - Queue Chart of Accounts entries during setup (Priority: P2)

The coordinator can add general ledger accounts to the Chart of Accounts from a dedicated tab during first-run setup, using the same add-account experience (name, type, and bank/cash flag) that's used on the standalone Chart of Accounts page — but, unlike that standalone page, an account added here is only queued for creation: it isn't written to the database until the coordinator finishes setup, exactly like the committee roles added on the committee tab. Below the add-account form, the tab also lists every account that will already exist once setup finishes — including the seeded system accounts — alongside the queued list, each in its own half-width column, so the coordinator can see the whole resulting chart of accounts up front, not just what they've added this session.

**Why this priority**: A genuinely new capability (setup previously offered no way to add accounts), valuable but not blocking — the app already seeds default system accounts, so a coordinator can still finish setup and add accounts afterward from Finance ▸ Chart of Accounts if they skip this tab.

**Independent Test**: On the chart-of-accounts tab, add a new account with a name, type, and (for an Asset account) the bank/cash flag; confirm it appears in the tab's queued-accounts list but does not yet exist in Finance ▸ Chart of Accounts; then finish setup and confirm it now exists there.

**Acceptance Scenarios**:

1. **Given** the chart-of-accounts tab is open, **When** the user enters a valid account name and type and submits, **Then** the account is added to the tab's bordered queued-list box (not yet created in the database) and the entry form clears for the next account.
2. **Given** an account name that matches one already queued in this session, or one that already exists in the Chart of Accounts, **When** the user tries to add it, **Then** the same validation error shown on the standalone Chart of Accounts page for a duplicate name is shown here, and no duplicate is queued.
3. **Given** the account type selected is Asset, **When** the add-account form is shown, **Then** the bank/cash account checkbox is available, matching the standalone page's behavior.
4. **Given** no accounts are queued on this tab, **When** setup is completed, **Then** setup still completes normally with only the system default accounts present.
5. **Given** accounts were queued on this tab but the coordinator closes the app before finishing setup, **When** the app is relaunched, **Then** it returns to the setup wizard (setup is still incomplete) and the previously queued accounts are gone — nothing was persisted — so the coordinator must re-add them, the same as they would need to re-enter a committee role or any other unsaved field.

---

### User Story 5 - Add committee office-holder roles one at a time (Priority: P2)

Instead of typing a comma-separated list of extra committee role titles into a single text box, the coordinator types one role name into a field, clicks a "+" button to add it, and sees it appear in a list underneath. They can add as many roles as needed and remove one they added by mistake before finishing setup.

**Why this priority**: Directly requested in the issue as a usability improvement over the comma-separated list; independent of the tab restructuring itself but naturally lives inside the committee tab from User Story 1.

**Independent Test**: On the committee tab, add two role titles one at a time via the "+" button, confirm both appear in the list under the entry field, remove one, and confirm only the remaining one is submitted with setup.

**Acceptance Scenarios**:

1. **Given** the committee tab is open, **When** the user types a role title and clicks "+", **Then** the title appears in a bordered list box below the entry field and the entry field clears for the next title.
2. **Given** the entry field is empty or only whitespace, **When** the user clicks "+", **Then** no entry is added.
3. **Given** a role title already appears in the list (case-insensitive match), **When** the user tries to add it again, **Then** the duplicate is rejected and the existing entry is not duplicated.
4. **Given** one or more roles have been added, **When** the user removes one from the list, **Then** it no longer appears in the list and is not included when setup is submitted.
5. **Given** no roles have been added, **When** setup is submitted, **Then** setup completes with no additional office-holder titles, exactly as it does today when the field is left blank.

---

### User Story 6 - Use dropdowns and checkboxes consistent with the rest of the wizard (Priority: P3)

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
- The user switches tabs rapidly: because the wizard doesn't persist anything to the database until Finish is clicked — including the queued committee roles, the queued Chart of Accounts entries, and the queued opening balances — tab switching itself must not trigger any concurrent database access.
- Finish is submitted but a required field on another tab is invalid: the queued committee roles, queued Chart of Accounts entries, and queued opening balances MUST remain queued and visible, not discarded, so the coordinator can fix the invalid field and retry Finish without re-entering them.
- A list box (committee roles or queued accounts) grows long enough to overflow its allotted space: the bordered list MUST stay contained (e.g. scrolling within its own border) rather than pushing the rest of the tab out of view — this applies to any bordered list box anywhere in the app, not just these two.
- A negative balance is entered for an account on the Opening Balances tab (e.g. an overdrawn bank account): it MUST be accepted and posted to the opposite normal side, exactly as the standalone Opening Balances page already does today.
- Sales tax is toggled off after a tax rate and fee tax treatments were entered: the same clearing behavior that exists today (tax fields reset) must still apply regardless of which tab layout is in effect.
- The debug-only "load sample data" checkbox: continues to appear only when a debug seeder is available; when it is available, it appears specifically on the review tab (see FR-025).

## Requirements

### Functional Requirements

- **FR-001**: The setup wizard MUST present its settings as a single screen with a tab strip, styled consistently with the tab component already used on the Finance screen, replacing the current sequence of numbered full-page steps.
- **FR-002**: Each tab MUST group settings that are logically related to each other; no tab MUST contain only one or two settings — such settings MUST be relocated to the most closely related tab.
- **FR-003**: The wizard MUST allow moving between tabs both by clicking a tab's header directly and by clicking a "Next" control that advances to the next tab in the defined order.
- **FR-004**: "Next" MUST NOT advance past a tab whose own required fields fail validation.
- **FR-005**: The last tab MUST be a review tab that displays every value entered across all other tabs in read-only form before the user finishes setup.
- **FR-006**: The review tab MUST display queued committee office-holder titles and queued chart-of-accounts entries as bordered list boxes (see FR-007) rather than as comma-separated or plain text.
- **FR-007**: Every list box anywhere in the application — whether it lets the user select an entry (interactive) or is purely a read-only display of entries — MUST be rendered as a bordered list. This is an application-wide visual convention, not a setup-wizard-only detail: the committee-role list and the queued-accounts list (both while being built and when summarized on the review tab) are this feature's instances of it, and it is the standard any list box added elsewhere in the app afterward MUST also follow.
- **FR-008**: Finishing setup from the review tab MUST validate all required fields across every tab and MUST refuse to complete setup while any are invalid, consistent with today's all-fields-required-before-finish behavior; on a successful Finish, every queued committee office-holder title, every queued Chart of Accounts entry, and the queued opening balances MUST be created together with the rest of setup, in one submission.
- **FR-009**: The wizard MUST let the user add an additional committee office-holder title by typing its name into an entry field and clicking an "add" ("+") control, rather than by editing a single comma-separated text field.
- **FR-010**: Each added committee office-holder title MUST appear in a bordered list box displayed alongside the entry field (a two-column layout, entry field and list side by side), and the user MUST be able to remove a previously added title from that list before finishing setup.
- **FR-011**: Adding a committee office-holder title that is blank/whitespace-only, or that duplicates (case-insensitively) a title already added in the same setup session, MUST be rejected without adding a duplicate or empty entry.
- **FR-012**: The wizard MUST include a tab for queuing entries to add to the Chart of Accounts during setup, offering the same fields (name, account type, and — for Asset accounts — the bank/cash flag) as the existing standalone Chart of Accounts page's add-account control.
- **FR-013**: An account added on the Chart of Accounts tab MUST be held in the wizard's own in-progress state and MUST NOT be created in the database until the user finishes setup, at which point it MUST be created using the same account-creation behavior the standalone Chart of Accounts page uses. Until then, it MUST appear in that tab's bordered list box of queued accounts.
- **FR-014**: Adding an account whose name matches one already queued in the current setup session (case-insensitive), or one that already exists in the Chart of Accounts, MUST be rejected with the same validation behavior as the standalone Chart of Accounts page, and MUST NOT queue a duplicate.
- **FR-015**: The wizard MUST NOT require any account to be queued on the Chart of Accounts tab in order to finish setup.
- **FR-016**: The add-account fields and validation MUST be a single shared experience used by both the standalone Chart of Accounts page and the setup wizard's tab, rather than two separately built forms; the standalone page's existing behavior of creating an account immediately on submit MUST remain unchanged, while the setup wizard's use of that same experience MUST defer creation until Finish, as described in FR-013.
- **FR-017**: The wizard MUST include a tab for entering opening balances covering every eligible account that will exist once setup completes — every already-existing (system default) account plus every account currently queued on the Chart of Accounts tab — using the same balance-entry experience (per-account amount entry, as-at date, live Opening Balance Equity plug calculation) as the existing standalone Opening Balances page. Every account is eligible except Opening Balance Equity itself (the plug target, not an enterable row); this includes Member Receivable and the tax clearing accounts, so a coordinator migrating existing balances from another system can seed real carried-over figures for them, not just brand-new accounts.
- **FR-018**: Opening balance entries made on this tab MUST be held in the wizard's own in-progress state and MUST NOT be posted until Finish, at which point they MUST be posted together with the rest of setup using the same posting behavior (paired GL debits/credits, Opening Balance Equity plug) as the standalone Opening Balances page.
- **FR-019**: The opening-balance entry fields and validation MUST be a single shared experience used by both the standalone Opening Balances page and the setup wizard's tab, rather than two separately built forms; the standalone page's existing behavior of posting immediately MUST remain unchanged, while the setup wizard's use of that same experience MUST defer posting until Finish, as described in FR-018.
- **FR-020**: The Opening Balances tab's list of accounts MUST stay in sync with the Chart of Accounts tab: queuing an account there MUST add a corresponding row here, and removing a queued account there MUST remove its row (and any balance entered for it) here.
- **FR-021**: Finish MUST be blocked unless either (a) the coordinator has posted at least one non-zero opening balance from the Opening Balances tab — the same minimum the standalone Opening Balances page enforces — or (b) the "load sample data" option is selected, in which case the seeded sample data supplies its own opening balances and manual entry is not required.
- **FR-022**: The theme (appearance) setting in the wizard MUST be presented as a dropdown selector rather than a toggle switch, and selecting a value MUST update the wizard's own appearance immediately.
- **FR-023**: Every yes/no (boolean) setting elsewhere in the wizard, other than the theme selector, MUST be presented as a checkbox rather than a toggle switch.
- **FR-024**: Regrouping settings into tabs MUST NOT change what data first-run setup captures or how it's validated — every field, validation rule, and default value that exists in the current wizard MUST still exist and behave the same way in the tabbed wizard.
- **FR-025**: The "load sample data" option MUST continue to appear only when a debug data seeder is available (never in a release build), and when available it MUST appear specifically on the review (verification) tab.
- **FR-026**: The debug sample-data seeder MUST include opening balances for the accounts it seeds, so a coordinator who opts into sample data starts with realistic account balances without completing the Opening Balances tab manually. This MUST cover every account the seeder touches, including the seeded system accounts, not only the newly-created bank account.
- **FR-027**: The Chart of Accounts tab MUST display, alongside its bordered list box of queued accounts, a second read-only list of every account that will already exist once setup finishes — every seeded system account plus any other already-existing account — clearly indicating which are system accounts, laid out in a two-column row below the add-account form, each list box at half width.

## Key Entities

- **Committee office-holder title (in-session list)**: A role name the coordinator adds during setup, held only in the wizard's own in-progress state until Finish, at which point the full set is submitted together — same underlying data as today's comma-separated field, only the entry/display mechanism changes.
- **Chart of Accounts entry (in-session queue)**: A general ledger account (name, account type, and — for Asset accounts — whether it's a bank/cash account) the coordinator queues during setup, held only in the wizard's own in-progress state until Finish, at which point it's created using the same account-creation behavior as the standalone Chart of Accounts page.
- **Opening balance entry (in-session queue)**: An account's starting balance as at a chosen date, queued during setup the same way committee roles and Chart of Accounts entries are, posted as one ledger entry (with any residual difference plugged to Opening Balance Equity) together with the rest of setup at Finish.

## Success Criteria

### Measurable Outcomes

- **SC-001**: A first-time coordinator can complete setup by visiting every tab exactly once and clicking Finish, without the wizard ever showing more than one screen shell (tab strip + content area).
- **SC-002**: No tab in the redesigned wizard contains fewer than three related settings, except the review tab, the Chart of Accounts tab, and the Opening Balances tab (all exempt because their content is inherently a summary/list/table rather than a fixed set of settings).
- **SC-003**: 100% of the fields, validations, and default values present in the current five-step wizard are still present and enforced after the redesign (no silent loss of setup capability).
- **SC-004**: A coordinator can add at least five committee office-holder titles and remove any of them before finishing setup, with no duplicate or blank entries ever appearing in the list.
- **SC-005**: A coordinator can queue at least one Chart of Accounts entry during setup, confirm it does not yet exist anywhere in the app before Finish is clicked, then see it appear in the standalone Chart of Accounts page (Finance ▸ Chart of Accounts) once setup completes, with no duplication.
- **SC-006**: 100% of attempts to finish setup with no opening balances posted and "load sample data" unselected are rejected; 100% of attempts to finish with "load sample data" selected succeed regardless of whether opening balances were entered manually.
- **SC-007**: After loading sample data during setup, every seeded account shows a non-placeholder (realistic, non-zero where expected) opening balance without the coordinator having entered any manually.

## Assumptions

- "Tabs should look like the ones in the Finance screen" refers to the `Tabs`/`Tab` Blazor component already used by `FinancePage.razor`; the setup wizard's tabs are built with that same component for visual and behavioral consistency.
- The exact tab names and final grouping (e.g. "General", "Membership & Fees", "Sales Tax", "Committee", "Chart of Accounts", "Opening Balances", "Review") are left to the planning step to finalize, as long as no tab holds only one or two settings and the grouping is logical — the issue asks for a grouping strategy, not exact tab names. The Chart of Accounts tab MUST come before the Opening Balances tab in the defined order, since the balances tab needs to know what's been queued there.
- The theme dropdown offers the two themes the application currently supports (Light and Dark); if more themes are added later, the dropdown grows to match without further spec changes.
- A "bordered list box" means a list of entries rendered inside a visible border/box (as opposed to a bare bulleted list, plain text, or borderless rows) — this is the one visual shape "list box" refers to everywhere in this spec, whether the list is purely read-only (the review tab's summaries) or lets the user act on an entry (e.g. remove it, as on the committee tab).
- The bordered-list requirement (FR-007) is deliberately written as an application-wide convention rather than a wizard-only style choice, per explicit direction that this change is in scope project-wide. No other list box exists in the app today — the committee-role list and the queued-accounts list introduced by this feature are its first instances — so no other screen needs retrofitting right now, but the convention itself is recorded here so any list box built afterward, anywhere in the app, follows it too.
- The existing add-account control is modified (not merely reused as-is) so it can operate in two modes: creating immediately, as it does today on the standalone Chart of Accounts page, and queuing locally without persisting, as required by the setup wizard. Changing that shared control is explicitly in scope for this feature (see FR-016). The existing opening-balance entry control is modified the same way, for the same reason (see FR-019).
- "Opening balances must be entered before setup can complete" means the coordinator must reach and post the Opening Balances tab — mirroring the standalone Opening Balances page's own rule that at least one non-zero entry is required to post at all, and that leaving any individual account at zero is a valid, intentional choice, not a validation failure. Finish is blocked only when the tab was never posted at all (and sample data wasn't chosen instead), not because some specific account was left at zero.
- "Unless data seeding is enabled" means the coordinator has checked the "load sample data" option on the review tab. In a release build, where no debug seeder is ever registered and that option is never offered, opening balances are always required to finish setup.
- Because this is always a first-run flow (no Settings record exists yet), the wizard's Opening Balances tab never needs the standalone page's "opening balances have already been posted" warning — that scenario cannot occur during setup.
- The as-at date the Opening Balances tab defaults to is left to the planning step to decide, since the standalone page's own default (derived from the organisation's financial-year-start setting) isn't available yet during first-run setup — no Settings record exists until Finish.
- This feature's tab restructuring, control-style changes (FR-001 through FR-006, FR-008 through FR-026 except FR-007), and the setup-specific list boxes are limited to the `/setup` first-run wizard. The Settings page's own General tab (which also currently uses a theme toggle switch) is out of scope for those — the issue's wording specifically calls out "the setup wizard." The bordered-list convention itself (FR-007) is the one exception: it is application-wide by design, as described above.

## ADDED Requirements
<!-- capability: settings -->

### First-run setup is one tabbed screen, not a linear multi-step flow
The setup wizard MUST present every setting on one screen with a persistent tab strip (Organisation Settings, Chart of Accounts, Opening Balances, Committee, Review), navigable by clicking any tab header or by Next; Next MUST validate only the current tab's own fields before advancing, while Finish validates every field across every tab.

#### Scenario: coordinator jumps directly to a later tab
- **WHEN** the coordinator clicks a tab header ahead of the current tab
- **THEN** the wizard navigates there immediately without validating the skipped tabs
- **AND** any values already entered on earlier tabs remain intact

#### Scenario: Finish is attempted with an earlier tab still invalid
- **WHEN** the coordinator clicks Finish while a required field on a tab other than the current one is invalid
- **THEN** setup is rejected
- **AND** the coordinator is told to check every tab, not just the one currently shown

### Setup can queue new Chart of Accounts entries, created together with the rest of setup
The setup wizard MUST let the coordinator queue zero or more new accounts (name, type, and — for Asset accounts only — a bank/cash flag) during setup; queued accounts are created via the same account-creation rules as the standalone Chart of Accounts page, all together at Finish, and are never required to complete setup.

#### Scenario: coordinator queues an account and finishes setup
- **WHEN** the coordinator queues an account during setup and then finishes
- **THEN** that account exists in the Chart of Accounts once setup completes
- **AND** it did not exist anywhere in the app before Finish was clicked

#### Scenario: coordinator queues a duplicate account name
- **WHEN** the coordinator tries to queue a name matching (case-insensitively) an existing account or one already queued this session
- **THEN** the queue rejects it and nothing is added

### Finishing setup requires a posted opening balance or an explicit sample-data opt-in
Setup MUST refuse to finish unless at least one non-zero opening balance has been queued, or the coordinator has explicitly chosen to load sample data instead; a release build with no sample-data option available always requires a queued opening balance.

#### Scenario: Finish attempted with nothing queued and no sample data
- **WHEN** the coordinator clicks Finish with no opening balance queued and "load sample data" unselected
- **THEN** setup is rejected and no Settings, account, or ledger records are created

#### Scenario: Finish succeeds via sample data instead of a manual balance
- **WHEN** the coordinator selects "load sample data" without queuing any opening balance
- **THEN** setup completes normally

### Committee office-holder titles are added one at a time, not typed as a delimited list
Setup MUST let the coordinator add optional committee office-holder titles one at a time (reject blank/whitespace and case-insensitive duplicates without adding) and remove any previously added title, composing the final list from whatever remains queued at Finish.

#### Scenario: coordinator adds and removes titles before finishing
- **WHEN** the coordinator adds several titles and removes one of them before Finish
- **THEN** the removed title is absent from the finished Settings' office-holder titles
- **AND** every title still queued is present
