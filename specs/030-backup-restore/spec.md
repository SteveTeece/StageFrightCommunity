# Feature Specification: Restore From Backup During First-Run Setup

**Feature Branch**: `030-backup-restore`

**Created**: 2026-09-07

**Status**: Draft

**Input**: GitHub issue #340 ("[FEATURE] Add restore from backup to setup wizard"), including its NOTE that the backup must include **all** data required to restore the current state, and its request for a round-trip backup-to-file test with a default filename built from the organisation name, the word "backup", and the backup date. Plus a refinement requested during specification: after a backup is written, read the file back and check the statistics it would present at restore time against the live data, to prove the file is readable and faithful.

## Background

The application already has a working backup and restore capability. From **Settings → Backup & Restore** a treasurer can create a single portable backup file (a `.sfbak` file holding every entity's data, including archived rows) and can restore one back — the restore validates the file, checks its version, checks it is complete, saves a recovery copy of the current database, then replaces the data in one all-or-nothing operation and writes an audit-trail entry.

That capability is only reachable **after** first-run setup is finished, because an unconfigured install always routes to the display-language screen and then the setup wizard. The real-world handover this feature exists for goes the other way around: an outgoing treasurer creates a backup and hands it to an incoming treasurer, who installs the app fresh and has **nothing to configure from** — they just want to load the previous treasurer's data and carry on.

This feature adds a restore path to the first-run flow so a brand-new install can be populated from a backup instead of being set up by hand, and closes three gaps that undermine a clean handover today:

1. **Reachability** — restore is not offered anywhere a first-time user can get to it.
2. **Completeness** — the backup format has not kept pace with the data model. Several organisation settings added since the format was first written (currency, display language, financial-year start, tax applicability and rate, per-fee tax codes, tax-entry mode) and three whole record types (GL journal entries, bank reconciliations and their lines) are not in the file, so a restore silently loses them.
3. **Portability confidence** — the file is meant to move between a Mac and a Windows PC in either direction, and there is no test that proves a cross-platform round trip preserves the data exactly.

It also adds a self-check to the create-backup step: once the file is written it is immediately read back from disk, and the summary it would present at restore time — the per-record-type counts — is checked against the live data. A treasurer then knows the file they are about to hand over is readable and complete, rather than discovering a problem only when the new treasurer tries to restore it.

## Clarifications

### Session 2026-09-07

- Q: Should the backup file be encrypted or password-protected, given it holds complete member personal data and full financial history and is meant to be emailed between people? → A: No — the file stays unencrypted, matching the application's unencrypted local database; neither backup nor restore prompts for a password. File-level encryption is out of scope for this feature and recorded as a possible future enhancement.
- Q: When a backup was made by a newer build of the same major version (so the file may carry data this older build does not recognise), should the restore reject it or accept it with the unrecognised data dropped? → A: Reject it — any backup whose recorded version is newer than the running application, a newer same-major build included, is refused outright with an "update the application and retry" message and the database is left untouched.
- Q: On a clean first-run install (only the seeded default database), should the restore still write a pre-restore recovery copy, or skip it? → A: Always write it — every restore, first-run included, takes a pre-restore recovery copy first (on first run it is a copy of the freshly-seeded default database); one code path everywhere.
- Q: Should the restore have an explicit wall-clock performance target, or only stay responsive with visible progress? → A: No time target — the requirement is that the restore never blocks the UI thread and shows continuously advancing progress; the cross-platform round-trip test uses a representative multi-year, mid-size-group dataset (a few hundred members, thousands of fee/payment/GL rows) with no timing assertion.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Incoming treasurer restores from a backup on first run (Priority: P1)

A newly-elected treasurer installs the application on their own computer and launches it for the first time. After choosing their display language, they are asked whether they want to restore from a backup instead of setting the organisation up from scratch. They tick that option, pick the backup file the previous treasurer gave them, and are shown a short summary of what the file contains (how many members, how much financial history, when it was taken). They confirm. The application loads the data and tells them it needs to be restarted to use it. They restart, and the app opens fully populated — members, rehearsals, events, committee history, accounts and financial records all present — with no setup wizard and nothing left to configure.

**Why this priority**: This is the core of the issue. Without it, the only way for a new treasurer to get the previous year's data is to finish a full manual setup and then restore from Settings — which requires them to invent placeholder organisation, fee and tax values first, defeating the point of the handover.

**Independent Test**: On a clean install, launch, choose a language, select "restore from backup", choose a valid backup file, confirm the summary, and verify the app reports a restart is needed; restart and verify the app opens on the dashboard (not the wizard), fully populated from the file, with setup treated as complete.

**Acceptance Scenarios**:

1. **Given** a clean install with no database and no completed setup, **When** the first-run flow is shown, **Then** it presents a checkbox to restore from a backup instead of completing setup manually, before any organisation/fee/tax data entry.
2. **Given** the restore option is ticked, **When** the user proceeds, **Then** they are prompted to choose a backup file from a location of their choosing.
3. **Given** a valid backup file has been chosen, **When** it is read, **Then** a summary of its contents is shown (per-record-type counts, the date it was created, the originating application version) with confirm and cancel actions.
4. **Given** the summary is shown, **When** the user confirms, **Then** the local database contents are replaced with the backup's data as a single all-or-nothing operation.
5. **Given** the restore has completed successfully, **When** it finishes, **Then** the user is told the application must be restarted to use the restored data, and the app does not continue into the wizard or dashboard on the pre-restore in-memory state.
6. **Given** a successful first-run restore and a restart, **When** the app launches, **Then** first-run setup is treated as complete, the setup wizard is not shown, and the restored data is in use.
7. **Given** the user cancels at the summary step, or the restore fails, **When** they return to the first-run flow, **Then** the normal setup wizard is available and no data has been changed.

---

### User Story 2 - Outgoing treasurer creates a verified backup to a chosen location and name (Priority: P2)

The outgoing treasurer, still using the fully set-up app, creates a backup to hand over. They choose where to save it and what to call it. The filename is already filled in for them as the organisation's name, the word "backup", and today's date, so the previous treasurer and the new one can both tell at a glance which organisation and which day the file is from. They can accept that name or change it. The file is written to the location they picked. Once the file is written, the app reads it straight back from disk and checks the record counts inside it against the live data, then tells the treasurer the backup is verified — or warns them if it is not — so they are not handing over a file that cannot be opened or is missing data.

**Why this priority**: The handover has two halves; a restore is only as good as the backup it reads. The issue explicitly asks for a user-settable location and filename with this specific default, and for confidence that the file is sound. It builds directly on the existing export capability.

**Independent Test**: In a set-up app, start a backup, verify the suggested filename is "<organisation name> backup <date>", change the location and (optionally) the name, complete the backup, and verify a single file exists at the chosen location containing the full dataset; confirm the app reports the backup as verified — read back from disk with its record counts matching the live data — and that corrupting or truncating the file causes the same operation to report failure.

**Acceptance Scenarios**:

1. **Given** a set-up app, **When** the user starts creating a backup, **Then** they can choose the destination folder and the filename.
2. **Given** the create-backup prompt, **When** the default filename is generated, **Then** it is composed of the organisation name, the word `backup`, and the date the backup is being taken.
3. **Given** an organisation name containing characters not allowed in filenames, **When** the default filename is generated, **Then** those characters are removed or replaced so the suggested name is always valid.
4. **Given** the user has chosen a location and name, **When** the backup completes, **Then** exactly one backup file exists at that location and it contains the complete dataset.
5. **Given** the chosen location cannot be written to (missing permission, disk full), **When** the backup is attempted, **Then** the user is shown a clear error and no partial or empty file is left behind.
6. **Given** the backup file has just been written, **When** the operation finishes, **Then** the system reads the file back from disk and computes the same summary statistics that a restore confirmation would display.
7. **Given** the file has been read back, **When** its per-record-type counts are compared against the live database and against the counts the file records for itself, **Then** the backup is reported as successful only if every count matches.
8. **Given** the just-written file cannot be read back, or its counts do not match the live data, **When** the verification runs, **Then** the backup is reported as failed and the user is told the file must not be relied upon.

---

### User Story 3 - A backup moves between operating systems without data change (Priority: P2)

A treasurer on a Mac creates a backup and emails it to the incoming treasurer, who restores it on a Windows PC. Later the same year the roles and machines are reversed. In both directions the restored application holds exactly the same data as the source — every record, every value, every archived item — with nothing altered by the different operating system, regional format, or time zone of the two computers.

**Why this priority**: The issue calls this out directly ("A backup created on a mac needs to be compatible with restoring to a windows pc and vice versa") and asks for a test. It is a correctness guarantee on the file format rather than a new screen, so it can ship alongside or just after Story 1.

**Independent Test**: Create a backup with a representative multi-year, mid-size-group dataset (a few hundred members and thousands of fee/payment/GL rows), restore it in an environment standing in for the other operating system and for a different regional/time-zone setting, and assert the restored data is field-for-field identical to the source.

**Acceptance Scenarios**:

1. **Given** a backup created on one supported platform, **When** it is restored on another supported platform, **Then** the restore succeeds and the resulting data is identical to the source.
2. **Given** a backup restored on a host whose regional settings, list separators, decimal separators, or calendar differ from the source host, **When** the restore completes, **Then** dates, amounts, and text are unchanged from the source.
3. **Given** a backup restored on a host in a different time zone from the source, **When** the restore completes, **Then** stored dates and timestamps represent the same instants/days as in the source.
4. **Given** an automated round-trip test, **When** it runs on the project's normal test platform, **Then** it exercises a create-then-restore cycle and fails if any record or setting differs.

---

### User Story 4 - The backup captures the complete current state (Priority: P3)

A treasurer whose organisation uses a non-default currency, a non-English display language, a mid-year financial-year start, sales-tax tracking, and bank reconciliations creates a backup and hands over. After the new treasurer restores it and restarts, the app is configured exactly as before — same currency symbol and code, same language, same financial-year start, same tax settings and tax-entry mode — and every GL journal entry and bank reconciliation is present. Nothing about the organisation's configuration or financial records had to be re-entered or was quietly dropped.

**Why this priority**: The issue's NOTE makes completeness a requirement, and the current format is demonstrably lossy for configuration added after it was written and for three record types. It is lower than Stories 1–2 only because those stories deliver visible value first; a lossy restore is still a serious correctness bug this story fixes.

**Independent Test**: Set up an app with non-default currency, language, financial-year start, tax configuration and tax-entry mode, add journal entries and a finalised bank reconciliation, back up, restore into a fresh database, and assert every one of those settings and records is present and identical.

**Acceptance Scenarios**:

1. **Given** an organisation with non-default currency, display language, financial-year start, tax applicability, tax rate, per-fee tax codes, and tax-entry mode, **When** a backup is created and restored, **Then** every one of those settings is reproduced exactly.
2. **Given** an organisation with GL journal entries, bank reconciliations, and reconciliation lines, **When** a backup is created and restored, **Then** all of those records are present and unchanged.
3. **Given** archived (soft-deleted) members, accounts, events, or other records in the source, **When** a backup is created and restored, **Then** those records are present in the restored database and still marked archived.
4. **Given** a restored database, **When** the application starts, **Then** the presence of the restored organisation-settings record causes first-run setup to be treated as complete (routing to the dashboard, not the wizard), and the normal Entity Framework schema migration brings an older restored database up to the current schema before first use.

---

### Edge Cases

- **Wrong file chosen**: the user selects a file that is not a backup, or a backup whose format the app cannot read — it is rejected at validation and the local database is left untouched.
- **Incompatible version**: a backup whose recorded version is newer than the running application — whether a newer major version or a newer build of the same major version that may carry data this build does not recognise — is rejected with a clear "update the application first" message and not partially applied; a backup from an older version is accepted and brought up to date by the normal startup migration.
- **Older backup missing newer data**: a backup that predates a setting or record type simply has nothing for it — the restore succeeds and the absent items take their normal defaults, without the restore failing or discarding data it does understand.
- **Empty or whitespace organisation name** (e.g. a backup taken very early): the default backup filename falls back to a fixed, sensible constant rather than producing an invalid or blank name.
- **Restore interrupted** (app closed, power loss) mid-write: the next launch must not come up on a half-written database, and the pre-restore recovery copy must still be available.
- **Restore while the app is already running its first-run flow**: cached configuration, culture, the first-run-complete flag, and other in-memory state from before the restore must not be trusted — hence the mandatory restart advice.
- **Restore then immediately continue**: if the user does not restart and instead keeps clicking, the app must not present a half-restored mixture of old and new state; the restart advisory must block the normal continue path.
- **Large dataset**: a backup with years of financial history restores without ever blocking the UI thread and with a progress indicator that advances throughout, and the confirmation UI accommodates the wait. There is no maximum-duration requirement — sustained responsiveness and visible progress are the bar, not a wall-clock target.
- **Language recorded outside the database** (the no-database language preference used before setup is finished): after a first-run restore and restart, the language stored in the restored settings is what takes effect.
- **Destination filename already exists**: the user is choosing the name and location via the operating system's save dialog, so overwrite handling follows that dialog's normal behaviour.
- **A backup fails its own post-write check**: the file cannot be read back, or the record counts in it do not match the database it was just made from — the backup is reported as failed and the user is told not to rely on the file, which may remain on disk for diagnosis.
- **Data changes during the very short backup window**: in the single-user desktop app a backup is effectively instantaneous; the post-write check compares the file against the dataset as captured for that backup, so a genuine file-vs-source mismatch fails the backup while it does not chase later unrelated edits.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The first-run flow MUST present a checkbox that lets the user restore from a backup file instead of completing setup manually, shown before any organisation, fee, or tax data entry.
- **FR-002**: When the restore option is selected, the user MUST be able to choose a backup file from a location of their choosing.
- **FR-003**: Before any database change, the system MUST validate the chosen backup file and show the user a summary of its contents — per-record-type counts, the date it was created, and the originating application version — with explicit confirm and cancel actions. The originating application version is informational only (never a compatibility axis — see FR-019) and MUST reflect the build that produced the file rather than a fixed placeholder.
- **FR-004**: The system MUST reject an unreadable, corrupt, or incompatible-version backup file with a clear message and MUST NOT alter the local database when it does so.
- **FR-005**: On a confirmed first-run restore, the system MUST replace the local database contents with the backup's data in a single all-or-nothing operation; a failure part-way through MUST leave the database in its exact pre-restore state.
- **FR-006**: After a successful first-run restore, the system MUST treat first-run setup as complete and MUST NOT require the user to enter organisation, fee, tax, or any other setup information.
- **FR-007**: After a successful restore, the system MUST advise the user that the application needs to be restarted to use the restored data, and MUST NOT proceed into the dashboard or the setup wizard on pre-restore in-memory state.
- **FR-008**: If the user cancels the restore, or the restore fails, the first-run flow MUST make the normal setup wizard available with no data changed.
- **FR-009**: Users MUST be able to create a backup file, choosing both the destination location and the filename.
- **FR-010**: The default filename offered when creating a backup MUST be composed of the organisation name, the word `backup`, and the date the backup is taken, and MUST always be a valid filename (invalid characters removed or replaced; a fixed fallback when the organisation name is empty).
- **FR-011**: A backup MUST contain every piece of data required to reconstruct the application's current state on a fresh install — all member, attendance, rehearsal, event, committee, AGM, account, financial (fees, payments, GL transactions, GL journal entries, bank reconciliations and their lines), and audit-trail data, together with every supporting reference and junction record (event types, participation records, AGM attendance records, committee office-holder types) and the organisation-settings record — including archived (soft-deleted) records. Completeness is enforced entity-by-entity by an automated parity check (see SC-008), not by this enumeration.
- **FR-012**: A backup MUST capture every configured organisation setting, including at least currency, display language, financial-year start, sales-tax applicability and rate, per-fee tax codes, and tax-entry mode, so that a restore reproduces the organisation's configuration exactly.
- **FR-013**: A backup file MUST be operating-system independent: a file created on any supported platform MUST restore successfully on any other supported platform and produce identical data.
- **FR-014**: Creating and reading a backup MUST NOT depend on the host's locale, culture, list/decimal separators, calendar, or time zone; a restored dataset MUST represent the same values and the same instants as the source regardless of the two hosts' regional settings.
- **FR-015**: Before a restore overwrites the local database, the system MUST first retain a recovery copy of the pre-restore database so an accidental or unwanted restore can be recovered from. This applies to every restore, including a first-run restore — where the recovery copy is of the freshly-seeded default database.
- **FR-016**: A restore MUST reproduce the source dataset exactly — same records, same field values, same archived/active state, same counts per record type.
- **FR-017**: Creating a backup and restoring a backup MUST each be recorded in the audit trail (the restore entry recorded against the restored database once it is in use).
- **FR-018**: Wherever backup or restore is invoked (the first-run flow and the existing Settings → Backup & Restore screen), the behaviour MUST be identical — the same code paths for the default-filename rule (FR-010), the post-restore restart advice (FR-007), and the post-backup verification (FR-021–FR-024). Neither entry point may re-implement or diverge from these.
- **FR-019**: A backup created by an older application version MUST restore into a newer version with its data migrated forward on next start. A backup whose recorded version is newer than the running application — including a newer build of the *same* major version, which may carry settings or record types this build does not recognise — MUST be rejected outright with a message telling the user to update the application and retry, and MUST NOT be partially applied.
- **FR-020**: The backup file MUST be unencrypted and MUST NOT be password-protected, consistent with the application's unencrypted local database; neither the create-backup flow nor the restore flow prompts for or requires a password. When a backup is created, the user MUST be shown a brief notice that the file is unencrypted and contains member personal data and financial history. File-level encryption is out of scope for this feature and is recorded as a candidate future enhancement.
- **FR-021**: Immediately after a backup file is written, the system MUST read the file back from disk before the backup is reported as successful.
- **FR-022**: From the read-back file, the system MUST compute the same summary statistics shown to confirm a restore (per-record-type counts, creation date, originating application version) and MUST verify every record-type count against the live database as captured for that backup, archived records included, and against the counts the file records for itself.
- **FR-023**: The system MUST report the backup as failed — and tell the user the file must not be relied upon — if the file cannot be read back, if the statistics it records are internally inconsistent, or if any record-type count does not match the source data.
- **FR-024**: The system MUST report a backup as successful only after the read-back and verification in FR-021–FR-023 have passed.
- **FR-025**: Every user-facing string this feature introduces or rewords MUST ship with a real translation in every language the application distributes — `de-DE`, `en-US`, `es-ES`, `fr-FR`, `it-IT`, `ja-JP`, `pl-PL` — alongside the `en-AU` neutral baseline, delivered in the same change. No added or changed string may be left neutral-only or rely on the English key-by-key fallback to stand in for a missing translation. (Project-wide rule — see `CLAUDE.md` → Localization and `docs/localization/adding-a-language.md` §3.2.)

### Key Entities *(include if feature involves data)*

- **Backup file**: a single self-contained, operating-system-independent file holding a complete point-in-time snapshot of all application data and configuration. Attributes: creation date/time, originating application/schema version, per-record-type counts, and the full data payload (all record types plus the organisation settings, archived rows included).
- **Backup summary**: the human-readable description of a backup file — when it was made, which application version made it, and how many of each kind of record it holds. Shown before a restore for the user to confirm, and also computed straight after a backup and checked against the live data to prove the new file is readable and complete. Derived from the backup file without changing any data.
- **Pre-restore recovery copy**: a backup of the database automatically taken immediately before any restore overwrites it — first-run restores included, where the copy is of the freshly-seeded default database — so an accidental or unwanted restore can be undone.
- **First-run restore choice**: the user's decision, during initial setup, to restore from a backup rather than configure the organisation manually. When taken and completed successfully, it satisfies every first-run configuration requirement and marks setup complete.
- **Organisation settings**: the single configuration record for the organisation (identity, fees, renewal months, currency, display language, financial-year start, sales-tax applicability/rate/codes, tax-entry mode, theme, audit retention, schema/version marker). Must be captured and restored in full.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A new user with only a backup file and a fresh install can reach a fully-populated, ready-to-use application without entering any organisation, fee, or tax details, in under 2 minutes of interaction and with no outside help.
- **SC-002**: Across a backup-then-restore round trip, 100% of records and 100% of configured settings present in the source are present and identical in the result — zero data loss and zero altered values.
- **SC-003**: A backup produced on one supported operating system restores successfully on the other in 100% of round-trip test runs, with the resulting data identical to the source.
- **SC-004**: A restore that is cancelled or that fails leaves the pre-existing database completely unchanged in 100% of cases.
- **SC-005**: An invalid, corrupt, or incompatible backup file is detected and reported before any database change in 100% of cases.
- **SC-006**: For every non-empty organisation name, the default backup filename identifies the organisation and the backup date with no manual typing and is always a valid filename.
- **SC-007**: After a successful restore, the restart advisory is shown 100% of the time and no user reaches the dashboard on pre-restore data.
- **SC-008**: The three record types and the organisation settings currently missing from the backup format are all present in every new backup, verified by an automated completeness check covering every entity and every settings field.
- **SC-009**: Every backup reported as successful has been read back from disk with its per-record-type statistics verified against the source data; a backup whose file is unreadable, internally inconsistent, or whose counts do not match the live data is never reported as successful.
- **SC-010**: For every language the application ships, launching the delivered feature produces no `Missing localization key` warning for any string it added or reworded — each such string is presented in that language, not the English fallback.

## Assumptions

- The existing backup/restore capability (Settings → Backup & Restore; portable `.sfbak` files; version check; completeness check; automatic pre-import recovery copy; atomic primary-key upsert inside one transaction; audit-trail entry) is the foundation. This feature extends that capability to the first-run flow and closes its completeness and portability gaps rather than replacing it.
- The restore option appears as a dedicated choice at the start of the first-run flow — after the display-language screen and before the setup wizard's data-entry steps — consistent with the pre-wizard screen pattern established for the language selection.
- "Restart" means the user is asked to close and reopen the application themselves; the application does not attempt to relaunch itself, because that is not reliably possible across all supported desktop platforms.
- Restore-from-backup in the first-run flow is offered only while first-run setup is incomplete. Restoring over an already-configured database stays a Settings-only action. Both paths take a pre-restore recovery copy first (FR-015); on the first-run path that copy is of the freshly-seeded default database.
- A backup remains a single self-contained file with no side-car files.
- "Supported platforms" for the operating-system-independence guarantee are the platforms the application ships on (Windows and macOS).
- The date in the default filename uses an unambiguous, filename-safe format (for example `yyyy-MM-dd`).
- Forward compatibility for an older backup relies on the application's normal startup database migration to bring the restored data up to the current schema.
- Backups are unencrypted, consistent with the application's unencrypted local database (FR-020); file-level encryption is out of scope for this feature and left as a possible future enhancement.
- Choosing the destination and filename uses the operating system's native save dialog, so overwrite prompts and folder navigation follow that dialog's normal behaviour.
- The post-backup self-check compares the read-back file's record counts against the database as captured for that backup; because the app is single-user and a backup is effectively instantaneous, unrelated edits are not expected to race the check. The counts compared cover every record type the backup carries, archived rows included.

## Verbatim Constraints

From issue #340, values the implementation must match exactly:

- The restore option is a `checkbox` control ("use a checkbox control").
- The default backup filename includes the literal word `backup`, alongside the organisation name and the backup date ("the organisation name, the word \"backup\" and the date the backup is taken").
