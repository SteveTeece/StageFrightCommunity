# Settings — Living Spec

> [DRAFT] Surface-first draft from existing code — every requirement is observed from the code surface unless tagged otherwise. Review before trusting.

## Purpose

Settings holds the organisation's operating configuration (identity, fees, renewal months, GST treatment, theme) as a single authoritative record, gates first-run setup, and provides the only disaster-recovery path (backup/restore) for the whole database. Without it, the app has no organisation-wide parameters to drive fee calculation, GST posting, or member-age rules, and no way to recover from a bad import or a lost device.

## Requirements

### Settings singleton gates app readiness
The application MUST treat the absence of a persisted Settings record as "first run" and route the user to setup rather than the dashboard; exactly one Settings record exists once setup has completed.

#### Scenario: application starts before setup
- **WHEN** no Settings record exists yet
- **THEN** reads of Settings return null
- **AND** the app treats this as first-run and requires setup before normal use

#### Scenario: application starts after setup
- **WHEN** a Settings record already exists
- **THEN** setup MUST refuse to run again rather than create a second record

### First-run setup creates the singleton with validated inputs and seeded defaults
Setup MUST validate the incoming request (organisation name, ABN, non-negative fees, a renewal month in 1–12) before creating the Settings record, and MUST seed the standard set of default event types alongside it so the app is usable immediately after setup.

#### Scenario: setup submitted with an out-of-range renewal month
- **WHEN** the requested membership renewal month is not between 1 and 12
- **THEN** setup is rejected and no Settings record is created

#### Scenario: setup completes successfully
- **WHEN** a valid setup request is submitted
- **THEN** the Settings singleton is created
- **AND** the standard default event types exist and are available immediately afterward

### ABN, when present, must be a checksum-valid Australian Business Number
Wherever an ABN is stored it MUST satisfy the ATO's weighted-checksum rule if non-empty; an absent ABN is always valid, since only new installs require one at setup while existing organisations may have none on file. User-entered formatting (spaces) MUST be normalized away so the persisted value is always a plain digit string, independent of how it was typed.
[inferred: checksum enforcement is intentionally suspended in Debug builds so developers/testers can use synthetic ABNs; this is a deliberate carve-out, not a bug]

#### Scenario: user types an ABN with grouping spaces
- **WHEN** the user enters an ABN using the "XX XXX XXX XXX" grouping shown by the input
- **THEN** the value persisted and validated is the plain 11-digit string with no spaces

#### Scenario: an ABN fails the checksum
- **WHEN** a non-empty ABN does not satisfy the ATO checksum (in a Release build)
- **THEN** the save is rejected with a validation error

### Settings edits from independent tabs never clobber each other's fields
Because organisation, GST, and other settings fields are edited from separate tabs against a shared singleton, each tab MUST re-fetch the currently persisted values for every field it does not own and merge them into its own save, so a stale in-memory copy from one tab can never overwrite a concurrent change saved from another.

#### Scenario: GST registration changed in one tab, then General tab is saved
- **WHEN** GST registration was toggled and saved from the GST/BAS tab
- **AND** the General tab (holding a stale copy of the settings loaded before that change) is then saved
- **THEN** the GST registration change made in the other tab is preserved, not overwritten

#### Scenario: organisation name changed in General tab, then GST tab is saved
- **WHEN** the organisation name was changed and saved from the General tab
- **AND** the GST/BAS tab is then saved
- **THEN** the updated organisation name is preserved, not reverted to the value the GST tab originally loaded

### Numeric and cross-field business rules guard every Settings save
A save MUST reject a negative minimum member age, a negative maximum age range, and a minimum age that exceeds the maximum, regardless of which tab or caller initiated the save.

#### Scenario: minimum age exceeds maximum age
- **WHEN** a save is attempted with minimum member age greater than the maximum age range
- **THEN** the save is rejected and no data is persisted

### GST registration controls whether GST codes apply to fee accruals
GST treatment codes for the annual and attendance fees are only meaningful while the organisation is GST-registered; setup MUST force both codes to null whenever the organisation is not registered, regardless of what was selected in the UI beforehand. Because toggling registration changes how future fee postings and BAS reporting behave, the UI MUST require an explicit confirmation step before the change takes effect.

#### Scenario: setup request has GST codes selected but registration off
- **WHEN** a setup request is submitted with `IsGstRegistered = false` and GST codes populated
- **THEN** both stored GST codes are null, not the values that were selected in the UI

#### Scenario: user toggles GST registration
- **WHEN** the user changes the "registered for GST" control from its current value
- **THEN** the UI shows a confirmation describing the consequence (GST splitting enabled, or GST fields hidden and future postings uncoded) before the change is applied
- **AND** cancelling leaves the prior registration state in effect [NEEDS CLARIFICATION: on later saves outside initial setup, is anything in this workflow expected to null the GST codes again if the user later re-registers, given the previous codes are never cleared when unregistering?]

### Event types are archivable, not deletable, and system defaults are protected
Event types follow the soft-delete-everywhere rule: retiring one archives it rather than removing it, and it can be restored later. Event types seeded as system defaults MUST NOT be archivable through this UI, preserving the baseline set the app was seeded with.

#### Scenario: user archives a custom event type
- **WHEN** a non-system-default event type is archived
- **THEN** it moves from the active list to the archived list
- **AND** it can later be restored back to active

#### Scenario: user attempts to archive a system-default event type
- **WHEN** the event type is marked as a system default
- **THEN** no archive action is offered for it

### Settings tabs mount lazily to avoid concurrent database access in the MAUI WebView
Only the tab that is active on page load has its content component instantiated; every other core tab's content MUST NOT be created until the user actually activates it, because the MAUI WebView can miss or race the framework's own "tab shown" transition callback, and multiple tabs initializing at once would contend for the same database context.

#### Scenario: Settings page opens on a deep link to a non-default tab
- **WHEN** the page loads with a specific tab requested (e.g. via `?tab=backup`)
- **THEN** only that tab's content is instantiated on load, and the others remain uninstantiated until clicked

#### Scenario: user switches to another core tab
- **WHEN** the user activates a tab whose content has not yet been shown
- **THEN** that tab's content component is created at that point, on the user's click gesture rather than a deferred transition callback

### Plugin tabs extend Settings without modifying core code
Core modules and external plugins MUST be able to contribute additional Settings tabs without changing the Settings page itself; contributed tabs are deep-linkable, ordered after the core tabs, and a failure in one contributed tab's content MUST NOT take down the rest of the page.

#### Scenario: a plugin registers a settings tab
- **WHEN** a plugin implementing the settings-tab extension point is loaded
- **THEN** its tab appears in the Settings tab strip, sorted by its declared order
- **AND** it is reachable via its own deep-link key

#### Scenario: a plugin tab's content throws during render
- **WHEN** a contributed tab's component fails while rendering
- **THEN** only that tab shows a failure message
- **AND** the core tabs and other plugin tabs continue to function normally

### Backup export produces a complete, versioned, soft-delete-inclusive snapshot
Export MUST capture every entity type in the domain, including soft-deleted/archived records, as a single versioned file, so a restore can fully reconstruct the database as it stood at export time — not just the currently-visible data.

#### Scenario: organisation has archived members or event types
- **WHEN** a backup is exported
- **THEN** the archived (soft-deleted) records are included in the file, not just active ones

#### Scenario: a backup file is inspected for its contents
- **WHEN** a user views a backup's summary before restoring
- **THEN** they can see how many records of each kind it contains, when it was generated, and its schema version, without the file having been imported

### Backup import is validated before any data is touched
Import MUST verify the file is a genuine, complete StageFright backup — parseable, on a compatible major schema version, and containing every required entity collection — before making any change to the database; any validation failure aborts with no data modified.

#### Scenario: a corrupted or foreign file is selected for restore
- **WHEN** the selected file cannot be parsed as a StageFright backup
- **THEN** the restore is rejected and the database is left untouched

#### Scenario: a backup is missing an expected entity collection
- **WHEN** the file's declared record counts omit one of the required entity types
- **THEN** the restore is rejected as incomplete before anything is imported

#### Scenario: a backup's major schema version is incompatible
- **WHEN** the backup's schema major version does not match what this application supports
- **THEN** the restore is rejected with guidance to upgrade the application

### Restore is self-checkpointing so a bad import is always recoverable
Before making any change, import MUST first export the database's current state to a checkpoint file, so that even a successful-but-unwanted restore (or one that fails partway) can be reversed by re-importing the checkpoint. The UI MUST require the user to explicitly confirm the restore after reviewing the backup's contents, since it overwrites existing data.

#### Scenario: user selects a backup file to restore
- **WHEN** a valid backup file is selected
- **THEN** the UI shows the backup's details and requires an explicit "confirm" action before the restore itself can be run

#### Scenario: an import is performed
- **WHEN** a restore is executed
- **THEN** a checkpoint capturing the pre-restore state is written before any record is changed
- **AND** that checkpoint remains available afterward regardless of whether the import succeeds

### Restore upserts by identity rather than replacing the database wholesale
Import MUST update or insert each record from the backup by its identity, and MUST leave local records that aren't present in the backup unchanged — restore is additive/overwriting, never a destructive wipe-and-replace — and the whole operation MUST be atomic (all records upserted together, or none).

#### Scenario: the current database has a record created after the backup was taken
- **WHEN** an older backup is restored
- **THEN** that newer local record is left in place, not deleted

#### Scenario: the restore fails partway through
- **WHEN** an error occurs while applying the backup's records
- **THEN** none of the backup's changes are left applied — the database reflects its pre-restore state

### Startup failures are captured for diagnostic display rather than failing silently
When the application encounters an unhandled error during startup (e.g. database initialization), that failure MUST be recorded as retrievable state — including where the database was expected to be — so the UI can present a meaningful diagnostic instead of the app simply failing to open.

#### Scenario: the database fails to initialize at launch
- **WHEN** an exception occurs during startup before the UI is otherwise usable
- **THEN** the error and the expected database location are retained
- **AND** are available for the UI to display to the user

#### Scenario: startup succeeds normally
- **WHEN** no startup error occurs
- **THEN** no error state is recorded

## Uncovered

_None — every file in the area was read._

### First-run setup is one tabbed screen, not a linear multi-step flow
The setup wizard MUST present every setting on one screen with a persistent tab strip (General, Membership & Fees, Sales Tax, Committee, Chart of Accounts, Opening Balances, Review), navigable by clicking any tab header or by Next; Next MUST validate only the current tab's own fields before advancing, while Finish validates every field across every tab.

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
