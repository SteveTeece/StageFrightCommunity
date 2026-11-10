# Feature Specification: Setup Wizard Layout Cleanup

**Feature Branch**: `031-setup-wizard-cleanup`
**Created**: 2026-10-09
**Status**: Draft
**Input**: GitHub issue #359, "Cleanup Startup Wizard layout": the wizard's first screen is too busy; redesign its layout and tabs, group the accounting settings on one tab, put language selection and "load sample data" at the top of the first page, and make "load sample data" need no manual entry at all.

## Background

The setup wizard's first tab ("Organisation Settings") currently stacks everything a coordinator must decide up front: organisation name, currency, financial-year start, inception date, annual and attendance fees, membership renewal month, audit retention, theme, and the whole sales-tax block. Accounting choices (currency, financial year, tax, fee tax treatment) are scattered between that tab and the form fields around them.

Since spec 029, the language choice and the Debug-only "load sample data" option already sit at the top of the very first screen the user sees (`/language-select`), before the wizard. Choosing sample data there creates the database, seeds a complete sample dataset and opens the dashboard, so the wizard is never shown. This spec keeps that arrangement, because the language must be applied before the wizard renders (spec 029) and the restore-from-backup screen follows it (spec 030). The remaining work is the wizard's own layout.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A calmer first wizard tab (Priority: P1)

A coordinator reaches the wizard after choosing a language. The first tab shows only organisation-level choices: the organisation's name, its appearance, and the housekeeping settings that are not money. It fits comfortably on a normal window without scrolling past a wall of fields.

**Why this priority**: This is the complaint in the issue. Everything else depends on the tab split.

**Independent Test**: Open the wizard on a clean install and look at the first tab. It lists only the organisation fields and none of the accounting fields.

**Acceptance Scenarios**:

1. **Given** the wizard is opened, **When** the first tab is shown, **Then** it contains the organisation name, theme, membership renewal month and audit retention period, and contains no currency, financial-year, inception-date, fee, or sales-tax field.
2. **Given** the first tab, **When** the coordinator leaves the organisation name blank and presses Next, **Then** the existing "organisation name is required" message appears and the wizard stays on the first tab.

---

### User Story 2 - All accounting settings on one tab (Priority: P1)

The coordinator moves to a second tab named "Accounting". It holds every money-related decision in one place: currency, financial-year start month and day, inception date, annual and attendance fees, whether sales tax applies, the tax rate, whether entered amounts include or exclude tax, and the tax treatment of each fee. Fee tax treatment sits next to the fees it describes, and the tax detail fields stay hidden until sales tax is switched on.

**Why this priority**: It is the second explicit request in the issue and the main source of today's clutter.

**Independent Test**: Open the second tab and confirm every accounting field is there and none appears on any other tab. Complete setup and confirm the saved settings are identical to what the same entries produced before.

**Acceptance Scenarios**:

1. **Given** the wizard, **When** the coordinator opens the Accounting tab, **Then** it contains currency, financial-year start month, financial-year start day, inception date, annual fee, attendance fee, the sales-tax switch, and, while the switch is on, tax rate, tax entry mode, annual-fee tax treatment and attendance-fee tax treatment.
2. **Given** sales tax is switched off, **When** the Accounting tab is shown, **Then** the tax rate, entry mode and treatment fields are not shown.
3. **Given** sales tax is switched on and the tax rate is left blank, **When** the coordinator presses Next, **Then** the existing "tax rate must be greater than zero" message appears and the wizard stays on the Accounting tab.
4. **Given** the coordinator enters values on both the Organisation and Accounting tabs and finishes setup, **When** setup completes, **Then** the stored settings equal the entered values exactly as they would have before this change.
5. **Given** a required field on the Accounting tab is invalid and the coordinator is on the Review tab, **When** Finish is pressed, **Then** setup is not completed and the existing "something needs attention" message is shown.

---

### User Story 3 - Review and tab order stay coherent (Priority: P2)

The tab strip reads in a natural order: Organisation, Accounting, Chart of Accounts, Opening Balances, Committee, Review. Next and direct tab clicks work across all six, and the Review tab shows each entered value under the same grouping the tabs use.

**Why this priority**: The new tab shifts every later position, and the review summary must not point at fields that moved.

**Independent Test**: Click Next from the first tab to the last and confirm six tabs appear in order; on Review, confirm every value entered appears once.

**Acceptance Scenarios**:

1. **Given** the wizard, **When** the coordinator presses Next repeatedly, **Then** the tabs appear in the order Organisation, Accounting, Chart of Accounts, Opening Balances, Committee, Review, and Finish is offered only on Review.
2. **Given** values entered on all tabs, **When** the Review tab is shown, **Then** it displays every entered value, with the organisation values and the accounting values shown in separate groups matching their tabs.
3. **Given** the coordinator clicks a later tab header directly, **When** that tab opens, **Then** its content loads without touching the database from two tabs at the same time.

---

### User Story 4 - Sample data needs no typing at all (Priority: P2)

A tester on a Debug build ticks "Load sample data" on the first screen, confirms, and lands on the dashboard with a complete, consistent dataset. They are never asked for an organisation name, fees, accounts, opening balances, or committee roles. Anything the sample dataset needs that it does not yet generate is generated by the seeder.

**Why this priority**: Already largely delivered by spec 029, so this story is mostly a guarantee that the new layout does not reintroduce data entry and that no setup-dependent data is missing from the seeder.

**Independent Test**: On a clean Debug install, tick "Load sample data" and confirm. Without any other input the dashboard opens, setup is complete, and every setup-dependent screen (settings, chart of accounts, opening balances, committee, fees) shows populated, valid data.

**Acceptance Scenarios**:

1. **Given** a Debug build on a clean install, **When** the user ticks "Load sample data" and confirms on the first screen, **Then** the wizard is skipped and the dashboard opens with setup marked complete.
2. **Given** sample data was loaded, **When** the user opens the Accounting and Organisation values under Settings, **Then** every field a wizard tab would have asked for holds a valid, sensible value (including currency, financial year, fees, renewal months, audit retention and committee roles).
3. **Given** a Release build, **When** the first screen is shown, **Then** no "Load sample data" option is shown and the user proceeds through the full wizard.
4. **Given** the sample-data run fails partway, **When** the failure occurs, **Then** an error is shown, the dashboard does not open, and pressing Confirm again retries the seeding.

---

### User Story 5 - Language and sample data stay at the top of the first screen (Priority: P3)

The language selector and, in Debug builds, the "load sample data" switch remain the first things on the first screen the user sees, above the confirm button, and are not repeated inside the wizard.

**Why this priority**: The issue asks for this placement and it already holds. This story protects it from regression when the wizard changes.

**Independent Test**: Start on a clean install and confirm the language selector and the Debug-only sample-data switch are the first controls shown; open the wizard and confirm neither appears there.

**Acceptance Scenarios**:

1. **Given** a clean install, **When** the first screen is shown, **Then** the language selector is the first control and, in Debug builds, the sample-data switch is directly below it.
2. **Given** the wizard is open, **When** any of its tabs is shown, **Then** none contains a language selector or a sample-data option.

### Edge Cases

- A coordinator moves back from the Accounting tab to Organisation: entered values on both tabs are preserved.
- Sales tax is switched on, details entered, then switched off: the hidden values do not block Next or Finish, as today.
- The currency is changed on the Accounting tab after fees are entered: fee amounts are kept unchanged.
- A tab fails to load: the existing per-tab error message is shown without breaking the other tabs.
- A very narrow window: the Accounting tab's fields wrap to a single column rather than overflowing.
- Re-pressing Confirm after a partly failed sample-data run retries seeding only, as in spec 029.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The wizard MUST present its tabs in this order: Organisation, Accounting, Chart of Accounts, Opening Balances, Committee, Review.
- **FR-002**: The Organisation tab MUST contain only the organisation name, the theme selector, the membership renewal month, and the audit retention period.
- **FR-003**: The Accounting tab MUST contain currency, financial-year start month, financial-year start day, inception date, annual fee, attendance fee, the sales-tax switch, tax rate, tax entry mode, annual-fee tax treatment and attendance-fee tax treatment.
- **FR-004**: On the Accounting tab, the tax rate, tax entry mode and both fee tax-treatment fields MUST be hidden while the sales-tax switch is off and shown while it is on.
- **FR-005**: The annual fee and attendance fee fields MUST be shown together with their tax-treatment fields on the Accounting tab, in the same group.
- **FR-006**: No accounting field listed in FR-003 MUST appear on any other wizard tab.
- **FR-007**: The wizard MUST NOT change which values setup captures, their defaults, their validation rules, or the values stored on Finish; only their placement changes.
- **FR-008**: Next MUST NOT advance past a tab whose own required fields fail validation, and Finish MUST refuse to complete while any field on any tab is invalid, as today.
- **FR-009**: The Review tab MUST show every entered value, grouped as Organisation and Accounting to match the tabs, and MUST keep its existing lists of queued accounts and committee titles.
- **FR-010**: Tab content MUST continue to load lazily, one tab at a time, so no two tabs use the database at once.
- **FR-011**: The wizard MUST NOT contain a language selector or a sample-data option.
- **FR-012**: The first screen MUST show the language selector first and, in Debug builds only, the sample-data switch directly below it, above the confirm button.
- **FR-013**: When sample data is chosen, the system MUST complete setup and seed a full dataset without any manual entry of organisation, accounting, account, opening-balance or committee data, and MUST open the dashboard.
- **FR-014**: The sample-data seeder MUST generate a valid value for every setting a wizard tab asks for. Where a setting is currently left at a placeholder or default, the seeder MUST set an appropriate sample value.
- **FR-015**: Every new or changed user-facing label introduced by this change MUST be added to the neutral resource file and to every shipped language set with a real translation.
- **FR-016**: Existing specifications and documents that describe the wizard's tab layout MUST be updated in the same change to match the new layout.

### Key Entities

- **Wizard form model**: the in-progress organisation and accounting values held across tabs until Finish. Its fields are unchanged; only the tab that edits each one changes.
- **Setup request**: the single submission made on Finish. Unchanged.
- **Sample dataset**: the Debug-only generated organisation, members, accounts, opening balances, committee and history. May gain generated values for any wizard setting it does not yet set.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The first wizard tab shows at most 4 input fields (down from 14 or more today) and needs no scrolling at a 1280×720 window.
- **SC-002**: 100% of the accounting fields listed in FR-003 appear on the Accounting tab and on no other tab.
- **SC-003**: A setup completed with the same entries before and after this change stores identical settings, in 100% of compared fields.
- **SC-004**: On a Debug build, a clean install reaches a populated dashboard from the first screen with no typed values: the user only picks a language, ticks sample data and confirms.
- **SC-005**: After a sample-data run, 100% of settings the wizard would have asked for hold valid values, and saving Settings without edits succeeds.
- **SC-006**: No shipped language is missing a translation for any new or changed label.

## Assumptions

- The language selector and sample-data switch stay on the existing first screen. Spec 029 placed them there so the wizard renders in the chosen language and so sample data can skip the wizard; moving them into the wizard would undo that. This already satisfies the issue's "top of the first page" request for the first screen the user sees.
- "Accounting settings" means the money-related fields in FR-003. The Chart of Accounts and Opening Balances tabs stay separate, because each is a full-size data-entry surface.
- Membership renewal month and audit retention are organisation housekeeping, not accounting, so they stay on the Organisation tab.
- The new tab is named "Accounting". The first tab keeps its existing "Organisation Settings" label, so no existing translation changes (decided at plan time).
- The theme control remains a Light/Dark dropdown, as spec 017 requires.
- The fee fields' currency symbol behaviour is unchanged by this work.

## MODIFIED Requirements
<!-- capability: app-host -->

### The setup wizard captures required configuration through validated steps before anything else can run

The wizard MUST require organisation identity, fee/renewal configuration, and sales-tax treatment (applicability, rate, and per-fee tax codes) before allowing submission, gating advancement on a per-step validation pass rather than only validating at final submit. Its tabs run in a fixed order: Organisation Settings (name, theme, renewal month, audit retention), Accounting (currency, financial year, inception date, fees, sales tax), Chart of Accounts, Opening Balances, Committee, Review. Every money-related field lives on the Accounting tab only. Completion MUST persist configuration through one setup service call and then route straight into the app.

#### Scenario: user attempts to advance past an incomplete step
- **WHEN** required fields for the current step fail validation
- **THEN** the wizard does not advance to the next step

#### Scenario: user completes all steps
- **WHEN** the final step is submitted successfully
- **THEN** configuration is persisted and the user is navigated to `/dashboard`

#### Scenario: sales tax switched on with no rate
- **WHEN** the coordinator turns sales tax on in the Accounting tab, leaves the rate blank and presses Next
- **THEN** the wizard stays on the Accounting tab and shows the tax-rate validation message

### Optional sample-data seeding is a debug-only, opt-in, non-blocking capability

Sample-data seeding MUST NOT be offered or reachable in release builds, MUST require explicit user opt-in even when available, and MUST run without freezing the UI, reporting incremental progress. It MUST also be safe to invoke against a database that already has data. The opt-in switch lives on the pre-wizard `/language-select` screen, directly below the language selector; choosing it completes setup from valid placeholder defaults, seeds the dataset and opens the dashboard without showing the wizard, so the user types nothing. The seeder stamps its own generated organisation name, fee schedule, renewal months and committee seat target over the placeholders; currency, language and sales-tax treatment keep their defaults.

#### Scenario: release build reaches first run
- **WHEN** no debug seeder is registered in the container
- **THEN** the sample-data switch is not shown and confirming the language proceeds towards the setup wizard

#### Scenario: user opts into sample data in a debug build
- **WHEN** the switch is on and the user confirms the language screen
- **THEN** setup completes from valid placeholder values, seeding runs off the UI thread with progress shown, and the dashboard opens without the wizard ever appearing

#### Scenario: seeding runs against a database that already has member data
- **WHEN** the seeder is invoked and active members already exist
- **THEN** it exits without creating duplicate data

#### Scenario: sample data overrides the organisation identity and fee inputs
- **WHEN** the seeder runs after setup was completed with the placeholder organisation name and fee figures
- **THEN** the persisted settings carry the seeder's generated organisation name, annual fee, attendance fee and renewal configuration instead
- **AND** the currency, language and sales-tax treatment are left unchanged
