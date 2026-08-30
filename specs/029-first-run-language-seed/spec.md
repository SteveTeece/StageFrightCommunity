# Feature Specification: First-Run Language Selection & Optional Sample-Data Seeding

**Feature Branch**: `029-first-run-language-seed`

**Created**: 2026-08-30

**Status**: Draft

**Input**: GitHub issue #361 ("[BUG] New language doesn't take effect until app restart") plus an extension requested during specification: fold the Debug-only sample-data choice into the same first-run screen and remove both the language and sample-data steps from the setup wizard.

## Background

The application resolves one display language when the process starts and cannot change it again while running (established by spec 027; not revisited here). Today the language is chosen on a tab **inside** the first-run setup wizard, and "load sample data" is a checkbox **inside** the wizard (Debug builds only, and it hides three wizard tabs when ticked). A language change — in the wizard or later in Settings — has no visible effect until the app is restarted; the only cue is a transient inline "restart required" notice on the Settings tab that vanishes the instant the user saves. Users conclude the change did not work.

This feature moves the language choice (and, in Debug builds, the sample-data choice) to a dedicated screen shown **before** the setup wizard, persists the language outside the database so it survives the restart that applies it, and replaces the Settings inline notice with a modal dialog that offers to restart.

Issue #360 (already fixed) restored runtime discovery of the shipped languages; that discovered list feeds every selector described here.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Choose the display language on first launch (Priority: P1)

A first-time user launches the freshly installed application. Before any setup screen appears, a small dedicated screen asks them to pick their language from the languages the app ships, with their operating-system language pre-selected when it is available. They confirm, the application restarts once on its own, and the setup wizard then runs entirely in the language they picked. On every later launch the app stays in that language and this screen never reappears.

**Why this priority**: This is the core of the issue — the language choice must be made once, up front, and actually take effect. Without it the wizard (and the rest of onboarding) runs in the wrong language.

**Independent Test**: Install clean, launch, confirm a non-default language on the first-run screen, observe the automatic restart, and verify the setup wizard and all subsequent screens render in that language; relaunch and verify no re-prompt.

**Acceptance Scenarios**:

1. **Given** a clean install with no database and no stored language preference, **When** the app launches, **Then** the first-run language screen is shown before the setup wizard and before the dashboard.
2. **Given** the first-run language screen, **When** it is displayed, **Then** it lists every shipped language by its own name and pre-selects the operating-system language if the app ships it, otherwise Australian English.
3. **Given** the user selects a language different from the one the current session is running in and confirms, **When** they confirm, **Then** the choice is persisted outside the database and the application restarts itself.
4. **Given** the app has restarted after a first-run language choice, **When** it comes back up, **Then** the setup wizard is shown in the chosen language and the first-run language screen is not shown again.
5. **Given** the user selects the language the current session is already running in and confirms, **When** they confirm, **Then** the choice is persisted and the app continues straight to the setup wizard without a restart.
6. **Given** a later launch where setup is still incomplete but a language preference is already stored, **When** the app launches, **Then** it goes straight to the setup wizard (in the stored language) with no first-run language screen.
7. **Given** the setup wizard is reached after a first-run language choice, **When** the user works through it, **Then** it contains no language step and no sample-data option, and its list of steps is the same regardless of any earlier choice.

---

### User Story 2 - Be told a restart is needed when changing language in Settings (Priority: P2)

An established user opens Settings, changes the display language, and saves. A dialog appears explaining the change takes effect after a restart and offering to restart now or later. Choosing "Restart now" relaunches the app in the new language. Choosing "Later" closes the dialog; the choice is still saved and applies on the next launch.

**Why this priority**: This is the literal bug from issue #361 — the change is saved but nothing tells the user it needs a restart. High value, but it builds on the persistence and restart capability introduced for Story 1.

**Independent Test**: In a set-up app, change the language in Settings, save, and verify the dialog appears; click "Restart now" and verify the app relaunches in the new language; repeat and click "Later" and verify the app stays running but the next manual launch is in the new language.

**Acceptance Scenarios**:

1. **Given** the Settings screen, **When** the user changes the display language and saves successfully, **Then** a modal dialog appears stating a restart is required, with "Restart now" and "Later" actions.
2. **Given** that dialog, **When** the user clicks "Restart now", **Then** the application relaunches and comes back up in the newly saved language.
3. **Given** that dialog, **When** the user clicks "Later", **Then** the dialog closes, the save is retained, and the next manual launch is in the new language.
4. **Given** the Settings screen, **When** the user saves without changing the display language, **Then** no restart dialog appears.
5. **Given** the Settings screen, **When** the display language selection differs from the saved value but has not been saved yet, **Then** no inline "restart required" notice is shown.
6. **Given** a platform that cannot restart itself, **When** the restart dialog appears, **Then** it instructs the user to restart manually and offers no "Restart now" action.

---

### User Story 3 - Load sample data from the first-run screen (Debug builds only) (Priority: P3)

A developer or tester runs a Debug build with a clean install. The first-run screen shows the language selector and, additionally, a "Load sample data" option. They pick a language, tick "Load sample data", and confirm. The app initialises the database, populates it with the full sample dataset while showing progress, then restarts and opens directly on the dashboard — in the chosen language, fully populated — without ever showing the setup wizard.

**Why this priority**: A developer convenience that shortens the clean-install-to-populated-app loop. Valuable but strictly Debug-only and not on the end-user path.

**Independent Test**: In a Debug build with a clean install, on the first-run screen tick "Load sample data", choose a language, confirm, watch the seeding progress, and verify the app restarts into the dashboard in that language with sample members, rehearsals, events, accounts and financial history present, and that the setup wizard was skipped.

**Acceptance Scenarios**:

1. **Given** a Debug build where sample-data seeding is available, **When** the first-run screen is shown, **Then** it includes a "Load sample data" option alongside the language selector.
2. **Given** a Release build (or any build where sample-data seeding is unavailable), **When** the first-run screen is shown, **Then** no "Load sample data" option is present and confirming proceeds straight to the setup wizard in the chosen language.
3. **Given** "Load sample data" is ticked, **When** the user confirms, **Then** the database is created and populated with the sample dataset, progress is shown while it runs, and only then does the application restart.
4. **Given** the app restarts after a sample-data first run, **When** it comes back up, **Then** it opens on the dashboard (not the setup wizard), in the chosen language, with the sample data present.
5. **Given** sample-data seeding fails partway through, **When** the failure occurs, **Then** the error is shown, the application does not restart, and the user is not left believing the app is ready.

---

### Edge Cases

- **Chosen language already active**: if the language chosen on the first-run screen equals the culture the current session is already running in, the app continues without restarting (Story 1, scenario 5).
- **Platform cannot self-restart** (any non-Windows platform): the first-run screen still appears and still persists the choice, but no automatic restart happens — onboarding continues in the current session's language and the choice takes effect on the next manual launch. The Settings dialog shows a "restart manually" message with no restart button.
- **Stored preference names a language no longer shipped**: treated as "no explicit choice" — startup resolution falls through to the operating-system language, then Australian English (consistent with spec 027).
- **Preference store cannot be read or written**: treated as "no stored preference" — the first-run screen may reappear on later launches until setup completes, and the app runs in the fallback language meanwhile. Setup completion still records a language into application settings, which then takes over.
- **Sample-data seeding fails**: the error is surfaced on the first-run screen, no restart occurs, and the app is not presented as ready. Recovering (e.g. wiping the partial database) is a developer action; this path is Debug-only.
- **User closes the app on the first-run screen without confirming**: nothing is persisted; the same screen appears on the next launch.
- **Setup wizard reached with no stored preference** (e.g. non-Windows first run, or a store write that silently failed): the wizard runs in the fallback language and, on completion, records whatever language resolution currently yields.

## Requirements *(mandatory)*

### Functional Requirements

#### First-run language screen

- **FR-001**: On launch, when setup is not complete and no display-language preference has been stored, the system MUST present a dedicated first-run language screen before the setup wizard and before the main application.
- **FR-002**: The first-run language screen MUST list every display language the application ships (using the runtime-discovered list), each shown by its own name (endonym), and MUST pre-select the operating-system display language when the application ships a matching language, otherwise Australian English.
- **FR-003**: When the user confirms their choice, the system MUST persist the chosen language to a store that does not require the application database and survives an application restart.
- **FR-004**: After the choice is persisted, the system MUST restart the application so the chosen language takes effect — UNLESS the current session is already running in the chosen language, in which case it MUST continue directly to the setup wizard without restarting.
- **FR-005**: Once a language preference is stored, the first-run language screen MUST NOT be shown again, whether or not setup has since been completed.
- **FR-006**: On a platform that cannot restart itself, the system MUST proceed without restarting after a first-run choice; the chosen language MUST take effect on the next manual launch and the first-run language screen MUST NOT reappear.

#### Startup language resolution

- **FR-007**: The display language applied when the process starts MUST resolve in this order: (1) an explicit language recorded in application settings, once the database exists; (2) otherwise the stored no-database language preference; (3) otherwise the operating-system display language when the application ships a matching language; (4) otherwise Australian English.
- **FR-008**: The runtime discovery of shipped languages (issue #360) MUST remain the single source of the language list for the first-run screen, the Settings selector, and step (3) of FR-007.

#### Sample data (Debug builds only)

- **FR-009**: In builds where sample-data seeding is available, the first-run language screen MUST also offer an option to load sample data. In builds where it is unavailable (including Release builds), that option MUST NOT be shown.
- **FR-010**: When the user selects "load sample data" and confirms, the system MUST initialise the database and populate it with the sample dataset, showing progress while it runs, and only then restart the application.
- **FR-011**: After a sample-data first run, the application MUST open on the main dashboard with setup already complete, presented in the chosen language, with the sample data present; the setup wizard MUST NOT be shown.
- **FR-012**: Loading sample data MUST be reachable only from the first-run language screen; it MUST NOT be offered anywhere else, including after first run.
- **FR-013**: If sample-data seeding fails, the system MUST display the failure and MUST NOT restart the application.

#### Setup wizard changes

- **FR-014**: The setup wizard MUST NOT contain a display-language selector.
- **FR-015**: The setup wizard MUST NOT contain a sample-data option, and MUST always present its complete set of steps — no step may be skipped, hidden or disabled based on a sample-data choice.
- **FR-016**: On completion, the setup wizard MUST record the stored no-database language preference as the application's saved language.

#### Settings screen

- **FR-017**: The Settings screen MUST keep its display-language selector, populated from the runtime-discovered language list.
- **FR-018**: The Settings screen MUST NOT show a transient inline "restart required" message while the selection differs from the saved value.
- **FR-019**: After the user saves the Settings screen with a changed display language, the system MUST show a modal dialog stating the change takes effect after a restart, offering "Restart now" and "Later"; "Later" MUST dismiss the dialog while keeping the saved change.
- **FR-020**: "Restart now" MUST relaunch the application. On a platform that cannot restart itself, the dialog MUST instead instruct the user to restart manually and MUST NOT offer a "Restart now" action.
- **FR-021**: Saving a changed display language in Settings MUST also update the stored no-database language preference so startup resolution (FR-007) stays consistent.
- **FR-022**: Saving the Settings screen without changing the display language MUST NOT show the restart dialog.

#### Restart capability

- **FR-023**: The system MUST provide a way to relaunch the application — start a fresh instance, then exit the current one — and MUST be able to report whether the running platform supports this.
- **FR-024**: Relaunch MUST be supported on Windows. On other platforms the capability MUST report that it is unavailable and MUST do nothing when invoked.

#### Retained behaviour

- **FR-025**: No in-session (no-restart) language switching is introduced; every language change continues to require a process restart to take visible effect.
- **FR-026**: The existing behaviour of applying one culture process-wide at startup, and formatting every displayed amount, date and number by that culture, MUST be unchanged.

### Out of Scope

- In-session (no-restart) live language switching.
- Any way to load sample data after the first run, or in a Release build.
- Translating user-entered content or the sample dataset.
- Self-restart on non-Windows platforms (those platforms degrade gracefully to "restart manually").
- Any change to the general-ledger model, money handling, or financial behaviour.
- Changing what the sample dataset contains or how it is generated.

### Key Entities *(include if feature involves data)*

- **Stored language preference**: a single language identifier (BCP-47 culture code) held outside the application database, in the platform's per-application preference storage. Written by the first-run language screen and by a Settings save; read during startup language resolution (FR-007, step 2). Overwritten only by a newer choice.
- **Saved application language**: the existing nullable language field on application settings. Retained for backup/export portability; now kept in step with the stored language preference (FR-016, FR-021). Remains the top of the resolution order once the database exists.
- **Sample dataset**: the existing Debug-only synthetic starting dataset (members, committee history, rehearsals with attendance, events, chart of accounts, opening balances, annual fees, AGMs and two financial years of activity). This feature changes only *where it is triggered from*, not what it contains or how it is built.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: On a fresh install, a user can select their language and reach the setup wizard (or the dashboard, if they chose sample data) fully in that language, with no manual restart step beyond the single automatic one.
- **SC-002**: 100% of display-language changes saved in Settings surface a restart prompt, and the user can trigger the restart from that prompt in a single action.
- **SC-003**: After a first-run language choice, that language is in effect on every subsequent launch with no further prompts about language.
- **SC-004**: In a Debug build, a developer can go from a clean install to a fully populated sample database in the chosen language, on the dashboard, in under one minute and without visiting the setup wizard.
- **SC-005**: The setup wizard presents at least one fewer step than before (no language step) and never varies its step list based on a sample-data choice.
- **SC-006**: After any language change and its restart, no user-facing text, amount or date appears in a language other than the one chosen.
- **SC-007**: A user who declines the restart ("Later") in Settings loses nothing — the change is still applied on the next launch.

## Assumptions

- The application already discovers the shipped languages at runtime (issue #360) and already applies exactly one culture process-wide when the process starts; this feature reuses both mechanisms rather than replacing them.
- The first-run language screen is an in-application screen shown before the setup wizard, not a separate operating-system window.
- "A platform that cannot restart itself" means, in practice, any non-Windows platform. The product currently ships and is verified on Windows; the non-Windows paths are graceful-degradation only.
- The stored language preference uses the platform's standard per-application key–value preference storage (survives restarts, needs no database, is local to the device).
- The sample-data seeder already produces a complete, self-consistent dataset and already requires the database to be initialised first. This feature invokes the existing "initialise then seed" sequence from the first-run screen; it does not change the dataset's contents.
- A brief, visible close-and-reopen of the application window during an automatic restart is acceptable to users; the first-run screen tells them it will happen.
- The existing saved-language field on application settings is kept (for backups and future export portability) and mirrored to the stored preference, rather than removed.
- Removing the sample-data option from the wizard also removes the wizard's tab-skipping behaviour that only existed to support it; the wizard becomes a fixed, linear set of steps.

## Dependencies & Relationship to Existing Specs

- **Supersedes** the first-run-language-step parts of **spec 027** (localization support): FR-013 (wizard language step) is replaced by the first-run screen; FR-021 (inline restart notice) is replaced by the Settings restart dialog; FR-023 (startup resolution ladder) gains the stored-preference tier. The language selector itself, the no-in-session-switching constraint, the runtime language catalogue and the money/culture rules from spec 027 are retained.
- **Supersedes** the in-wizard sample-data parts of **spec 022** (seed data placement) and the tab-bypass behaviour in **spec 017** (setup wizard tabs): the "load sample data" choice moves out of the wizard to the first-run screen, and the wizard's step list stops varying.
- **Builds on** issue #360 (runtime discovery of shipped languages), already fixed.
- No new database table or column is introduced. No change to the general-ledger model, money handling, or any financial behaviour.
