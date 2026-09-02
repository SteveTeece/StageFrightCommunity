# Feature Specification: First-Run Language Selection & Optional Sample-Data Seeding

**Feature Branch**: `029-first-run-language-seed`

**Created**: 2026-08-30

**Status**: Draft

**Input**: GitHub issue #361 ("[BUG] New language doesn't take effect until app restart"), plus two refinements requested during specification: (1) fold the Debug-only sample-data choice into the same first-run screen and remove both the language and sample-data steps from the setup wizard; (2) apply a newly chosen language **immediately, in the running session** — no application restart on any language change, and no "restart required" prompt or notice anywhere.

## Background

Today the display language is resolved once when the process starts and cannot change again while the app runs. It is chosen on a tab **inside** the first-run setup wizard, and "load sample data" is a checkbox **inside** the wizard (Debug builds only, and it hides three wizard tabs when ticked). A language change — in the wizard or later in Settings — has no visible effect until the app is restarted; the only cue is a transient inline "restart required" notice on the Settings tab that vanishes the instant the user saves. Users conclude the change did not work.

This feature makes a language change take effect **immediately in the running session** — the whole UI re-renders in the new language with no restart, on first run and in Settings alike. It also moves the language choice (and, in Debug builds, the sample-data choice) to a dedicated screen shown **before** the setup wizard, so onboarding runs in the chosen language from its first screen. The language is still recorded outside the application database as well, so a later launch that happens before setup is finished comes straight up in the chosen language without re-prompting. The Settings screen drops its inline "restart required" notice entirely; there is nothing to replace it with, because the change is visible the moment it is saved.

Issue #360 (already fixed) restored runtime discovery of the shipped languages; that discovered list feeds every selector described here.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Choose the display language on first launch (Priority: P1)

A first-time user launches the freshly installed application. Before any setup screen appears, a small dedicated screen asks them to pick their language from the languages the app ships, with their operating-system language pre-selected when it is available. They confirm, and the setup wizard opens entirely in the language they picked — the switch happens in place, with no restart. On every later launch the app stays in that language and this screen never reappears.

**Why this priority**: This is the core of the issue — the language choice must be made once, up front, and actually take effect straight away. Without it the wizard (and the rest of onboarding) runs in the wrong language.

**Independent Test**: Install clean, launch, confirm a non-default language on the first-run screen, and verify — with no restart — that the setup wizard and all subsequent screens render in that language; relaunch and verify no re-prompt and the same language.

**Acceptance Scenarios**:

1. **Given** a clean install with no database and no recorded language preference, **When** the app launches, **Then** the first-run language screen is shown before the setup wizard and before the dashboard.
2. **Given** the first-run language screen, **When** it is displayed, **Then** it lists every shipped language by its own name and pre-selects the operating-system language if the app ships it, otherwise Australian English.
3. **Given** the user selects a language different from the one the screen is currently displayed in and confirms, **When** they confirm, **Then** the choice is recorded outside the database and the setup wizard opens in the chosen language without the application restarting.
4. **Given** a first-run language choice has been made, **When** the setup wizard is shown, **Then** it renders in the chosen language, and the first-run language screen is not shown again in that session or on any later launch.
5. **Given** the user confirms without changing the pre-selected language, **When** they confirm, **Then** the choice is still recorded and the app continues straight to the setup wizard.
6. **Given** a later launch where setup is still incomplete but a language preference is already recorded, **When** the app launches, **Then** it goes straight to the setup wizard (in the recorded language) with no first-run language screen.
7. **Given** the setup wizard is reached after a first-run language choice, **When** the user works through it, **Then** it contains no language step and no sample-data option, and its list of steps is the same regardless of any earlier choice.

---

### User Story 2 - Change the display language in Settings and see it apply at once (Priority: P2)

An established user opens Settings, changes the display language, and saves. The application re-renders in the new language straight away — menus, labels, buttons, dates and amounts — with no restart and no prompt. The change is persisted and is still in effect on the next launch.

**Why this priority**: This is the literal bug from issue #361 — the change is saved but has no visible effect. High value; it builds on the same in-session culture-switch mechanism as Story 1.

**Independent Test**: In a set-up app, change the language in Settings and save; verify the visible UI switches to the new language immediately without a restart, and that relaunching keeps that language.

**Acceptance Scenarios**:

1. **Given** the Settings screen, **When** the user changes the display language and saves successfully, **Then** the visible application re-renders in the newly selected language without a restart.
2. **Given** the language has just been changed and saved in Settings, **When** the user navigates to any other screen, **Then** that screen is already in the new language.
3. **Given** the language has been changed and saved in Settings, **When** the application is next launched, **Then** it starts in that language.
4. **Given** the Settings screen, **When** the user saves other settings without changing the display language, **Then** the display language and the rendered UI language are unchanged.
5. **Given** the Settings screen with an unsaved change to the display-language selection, **When** the selection differs from the saved value, **Then** no "restart required" notice, dialog or restart prompt is shown at any point — before, during or after saving.

---

### User Story 3 - Load sample data from the first-run screen (Debug builds only) (Priority: P3)

A developer or tester runs a Debug build with a clean install. The first-run screen shows the language selector and, additionally, a "Load sample data" option. They pick a language, tick "Load sample data", and confirm. As part of that pre-wizard step the app initialises the database and populates it with the full sample dataset while showing progress, then opens directly on the dashboard — in the chosen language, fully populated, setup already marked complete — without ever showing the setup wizard and without restarting. In a Release build the "Load sample data" option is absent and confirming the language proceeds into the full setup wizard.

**Why this priority**: A developer convenience that shortens the clean-install-to-populated-app loop. Valuable but strictly Debug-only and not on the end-user path.

**Independent Test**: In a Debug build with a clean install, on the first-run screen tick "Load sample data", choose a language, confirm, watch the seeding progress, and verify the app opens the dashboard — with no restart — in that language, with sample members, rehearsals, events, accounts and financial history present, and that the setup wizard was skipped.

**Acceptance Scenarios**:

1. **Given** a Debug build where sample-data seeding is available, **When** the first-run screen is shown, **Then** it includes a "Load sample data" option alongside the language selector.
2. **Given** a Release build (or any build where sample-data seeding is unavailable), **When** the first-run screen is shown, **Then** no "Load sample data" option is present and confirming the language proceeds into the full setup wizard in the chosen language.
3. **Given** "Load sample data" is ticked, **When** the user confirms, **Then** the database is created and populated with the sample dataset as part of the pre-wizard step, progress is shown while it runs, and only then is the dashboard shown.
4. **Given** sample-data seeding has completed, **When** onboarding hands off, **Then** the dashboard is shown (not the setup wizard), in the chosen language, with the sample data present and setup marked complete — with no restart.
5. **Given** sample-data seeding fails partway through, **When** the failure occurs, **Then** the error is shown, the dashboard is not opened, setup is not marked complete, and the user is not left believing the app is ready.

---

### Edge Cases

- **Chosen language already the one on screen**: if the user confirms without changing the pre-selected language, the choice is still recorded (so the screen does not reappear) and onboarding continues; nothing needs to re-render.
- **Stored preference names a language no longer shipped**: treated as "no explicit choice" — startup resolution falls through to the operating-system language, then Australian English (consistent with spec 027).
- **Preference store cannot be read or written**: treated as "no recorded preference" — the first-run screen may reappear on later launches until setup completes, and the app runs in the fallback language meanwhile. Setup completion still records a language into application settings, which then takes over.
- **Sample-data seeding fails**: the error is surfaced on the first-run screen, the dashboard is not opened, and the app is not presented as ready. Because database initialisation runs once and only while setup is still incomplete, re-pressing Confirm retries the seeding step alone rather than dead-ending on "setup already completed". Fully recovering from a partially-seeded database (wiping it) is still a developer action; this path is Debug-only.
- **User closes the app on the first-run screen without confirming**: nothing is recorded; the same screen appears on the next launch.
- **Setup wizard reached with no recorded preference** (e.g. a store write that silently failed): the wizard runs in the fallback language; the user can still change language later in Settings, and setup completion records whatever language resolution currently yields.
- **Language changed in Settings while another screen holds in-progress input**: the switch re-renders open screens in place; text already typed into an open form is subject to that re-render like any other transient screen state — no separate guarantee is made that it survives (see Out of Scope).

## Requirements *(mandatory)*

### Functional Requirements

#### First-run language screen

- **FR-001**: On launch, when setup is not complete and no display-language preference has been recorded, the system MUST present a dedicated first-run language screen before the setup wizard and before the main application.
- **FR-002**: The first-run language screen MUST list every display language the application ships (using the runtime-discovered list), each shown by its own name (endonym), and MUST pre-select the operating-system display language when the application ships a matching language, otherwise Australian English.
- **FR-003**: When the user confirms their choice, the system MUST record the chosen language in a store that does not require the application database and persists across application launches.
- **FR-004**: On confirmation, the system MUST apply the chosen language to the running session immediately, with no application restart, and continue — to the setup wizard, or for a Debug sample-data run to the dashboard — with every screen rendered in that language.
- **FR-005**: Once a language preference has been recorded, the first-run language screen MUST NOT be shown again, whether or not setup has since been completed.

#### Startup language resolution

- **FR-006**: The display language applied when the process starts MUST resolve in this order: (1) an explicit language recorded in application settings, once the database exists; (2) otherwise the recorded no-database language preference; (3) otherwise the operating-system display language when the application ships a matching language; (4) otherwise Australian English.
- **FR-007**: The runtime discovery of shipped languages (issue #360) MUST remain the single source of the language list for the first-run screen, the Settings selector, and step (3) of FR-006.

#### In-session language switching

- **FR-008**: A display-language change MUST take effect in the running session without an application restart: all app-authored user-facing text, and the culture used to format amounts, dates and numbers, MUST re-render in the newly selected language across the whole application.
- **FR-009**: In-session language switching MUST be driven by the same mechanism from both entry points — confirming the first-run language screen and saving a changed language in Settings.
- **FR-010**: The application MUST NOT present any "restart required" prompt, dialog, inline notice or instruction in connection with a language change, on any screen, at any time.

#### Sample data (Debug builds only)

- **FR-011**: In builds where sample-data seeding is available, the first-run language screen MUST also offer an option to load sample data. In builds where it is unavailable (including Release builds), that option MUST NOT be shown.
- **FR-012**: When the user selects "load sample data" and confirms, the system MUST initialise the database and populate it with the sample dataset as part of the pre-wizard step, showing progress while it runs, and only then hand off to the dashboard.
- **FR-013**: After a sample-data first run, the application MUST open on the main dashboard with setup already complete, rendered in the chosen language, with the sample data present; the setup wizard MUST NOT be shown; no restart occurs.
- **FR-014**: Loading sample data MUST be reachable only from the first-run language screen; it MUST NOT be offered anywhere else, including after first run.
- **FR-015**: If sample-data seeding fails, the system MUST display the failure and MUST NOT open the dashboard or mark setup complete.

#### Setup wizard changes

- **FR-016**: The setup wizard MUST NOT contain a display-language selector.
- **FR-017**: The setup wizard MUST NOT contain a sample-data option, and MUST always present its complete set of steps — no step may be skipped, hidden or disabled based on a sample-data choice.
- **FR-018**: On completion, the setup wizard MUST record the no-database language preference as the application's saved language.

#### Settings screen

- **FR-019**: The Settings screen MUST keep its display-language selector, populated from the runtime-discovered language list.
- **FR-020**: When the user saves the Settings screen with a changed display language, the system MUST apply that language to the running session immediately (per FR-008) and MUST NOT show any inline "restart required" message, modal dialog or other restart prompt at any point.
- **FR-021**: Saving a changed display language in Settings MUST also update the recorded no-database language preference so startup resolution (FR-006) stays consistent.
- **FR-022**: Saving the Settings screen without changing the display language MUST NOT alter the rendered language or the recorded preferences.

#### Retained behaviour

- **FR-023**: The application MUST continue to apply exactly one culture across the whole process, and to format every displayed amount, date and number by that culture; this feature changes only that the active culture can now be replaced in-session, not how it is applied.

### Out of Scope

- Preserving or migrating text a user has already typed into an open form at the moment the language switches — the switch re-renders the UI; it does not carry in-progress input across the re-render.
- Any way to load sample data after the first run, or in a Release build.
- Translating user-entered content or the sample dataset.
- Any self-restart, relaunch or process-recycling mechanism (none is introduced by this feature).
- Any change to the general-ledger model, money handling, or financial behaviour.
- Changing what the sample dataset contains or how it is generated.

### Key Entities *(include if feature involves data)*

- **Recorded language preference**: a single language identifier (BCP-47 culture code) held outside the application database, in the platform's per-application preference storage. Written by the first-run language screen and by a Settings save; read during startup language resolution (FR-006, step 2). It exists so a launch that happens before setup completes (and before the database exists) comes up in the chosen language without re-prompting. Overwritten only by a newer choice.
- **Saved application language**: the existing nullable language field on application settings. Retained for backup/export portability; kept in step with the recorded language preference (FR-018, FR-021). Remains the top of the resolution order once the database exists.
- **Active session culture**: the one culture in force for the running process. Set during startup resolution and — new in this feature — replaced in place when the user confirms a language on the first-run screen or saves a changed language in Settings, driving an immediate re-render. Not persisted in its own right; it is always a reflection of the resolved or chosen language.
- **Sample dataset**: the existing Debug-only synthetic starting dataset (members, committee history, rehearsals with attendance, events, chart of accounts, opening balances, annual fees, AGMs and two financial years of activity). This feature changes only *where it is triggered from*, not what it contains or how it is built.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: On a fresh install, a user can select their language and reach the setup wizard (or the dashboard, if they chose sample data) fully in that language, without ever restarting the application.
- **SC-002**: A display-language change saved in Settings is visible in the UI within the same interaction — no restart, no prompt — for 100% of such changes.
- **SC-003**: After a first-run language choice, that language is in effect on every subsequent launch with no further prompts about language.
- **SC-004**: In a Debug build, a developer can go from a clean install to a fully populated sample database in the chosen language, on the dashboard, in under one minute and without visiting the setup wizard or restarting.
- **SC-005**: The setup wizard presents at least one fewer step than before (no language step) and never varies its step list based on a sample-data choice.
- **SC-006**: After any language change, no app-authored user-facing text, amount, date or number anywhere in the application remains in the previous language.
- **SC-007**: No screen in the application ever tells the user to restart, or offers to restart, in order to apply a language change.

## Assumptions

- The application already discovers the shipped languages at runtime (issue #360) and already applies exactly one culture process-wide when the process starts; this feature reuses both mechanisms and adds the ability to replace that culture while the process runs.
- Re-rendering the visible UI when the active culture changes is achievable without recreating the application process; the codebase already anticipates this as a future extension.
- The first-run language screen is an in-application screen shown before the setup wizard, not a separate operating-system window.
- The recorded language preference uses the platform's standard per-application key–value preference storage (persists across launches, needs no database, is local to the device).
- The sample-data seeder already produces a complete, self-consistent dataset and already requires the database to be initialised first. This feature invokes the existing "initialise then seed" sequence from the first-run screen; it does not change the dataset's contents.
- The existing saved-language field on application settings is kept (for backups and future export portability) and mirrored to the recorded preference, rather than removed.
- Removing the sample-data option from the wizard also removes the wizard's tab-skipping behaviour that only existed to support it; the wizard becomes a fixed, linear set of steps.
- The product currently ships and is verified on Windows; because the language switch is now purely an in-process re-render, it carries no platform-specific behaviour.

## Dependencies & Relationship to Existing Specs

- **Supersedes** parts of **spec 027** (localization support): its FR-013 (wizard language step) is replaced by the first-run screen; its FR-021 (inline restart notice) is removed outright — nothing replaces it, because the change is now visible on save; its constraint that a language change takes effect only on the next launch (no in-session switching) is **reversed** — a language change now applies immediately in the running session; its FR-023 (startup resolution ladder) gains the recorded-preference tier. The language selector itself, the runtime language catalogue and the money/culture formatting rules from spec 027 are retained.
- **Supersedes** the in-wizard sample-data parts of **spec 022** (seed data placement) and the tab-bypass behaviour in **spec 017** (setup wizard tabs): the "load sample data" choice moves out of the wizard to the first-run screen, and the wizard's step list stops varying.
- **Builds on** issue #360 (runtime discovery of shipped languages), already fixed.
- No new database table or column is introduced. No self-restart or process-relaunch mechanism is introduced. No change to the general-ledger model, money handling, or any financial behaviour.
