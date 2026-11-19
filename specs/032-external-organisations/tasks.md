# Tasks: External Organisations

**Input**: [spec.md](./spec.md), [plan.md](./plan.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/ui-contract.md](./contracts/ui-contract.md)

> **Scale note**: 85 tasks across about 60 files in Core, Data, UI, App and five test projects. Read these four first. Every resource file is owned by Phase 2. The organisation detail screen is split into three tab pages, one per story, so no story edits another's file. Backup work has its own phase. DI wiring and the menu land last, so a story is proven by its tests before it is reachable in the running app.

**Tests**: required. Constitution §11.0 needs every code path covered, with names in the form `Should_[Expected]_When_[Condition]`.

## Phase 1: Setup

- [x] **T001** Rework the docs to match this task split: split contract §3.3/§3.4 into three tab routes: `/organisations/{Id:guid}` (details, `OrganisationDetail`), `/organisations/{Id:guid}/contacts` (`OrganisationContacts`, which hosts `ContactEditor`) and `/organisations/{Id:guid}/history` (`OrganisationHistoryPage`), plus a shared `OrganisationTabs` strip. Move `GetHistoryAsync` from `IExternalOrganisationService` to a new `IOrganisationHistoryService`. Update plan.md's Screens, Call Flow and Project Structure sections to match · specs/032-external-organisations/contracts/ui-contract.md, specs/032-external-organisations/plan.md

## Phase 2: Foundational (blocks every story)

**Wave 1 — independent (different files):**

- [x] **T002** [P] Create the `ExternalOrganisation` entity as data-model §1 describes (name, address, email, soft-delete and audit fields, `Contacts` and `JournalEntries` navigations) · src/StageFright.Core/Entities/ExternalOrganisation.cs
- [x] **T003** [P] Create the `ExternalOrganisationContact` entity per data-model §2 (name, role, phone, mobile, email, `ExternalOrganisationId`, soft-delete) · src/StageFright.Core/Entities/ExternalOrganisationContact.cs
- [x] **T004** [P] Add a nullable `ExternalOrganisationId` and an `ExternalOrganisation?` navigation to `JournalEntry` per data-model §3 · src/StageFright.Core/Entities/JournalEntry.cs
- [x] **T005** [P] Create `IExternalOrganisationRepository` per contract §4.8, including `GetByIdIncludingArchivedAsync` and the active-name lookup · src/StageFright.Core/Contracts/IExternalOrganisationRepository.cs
- [x] **T006** [P] Create `IExternalOrganisationContactRepository` per contract §4.8 · src/StageFright.Core/Contracts/IExternalOrganisationContactRepository.cs
- [x] **T007** [P] Create `ExternalOrganisationValidationService` per contract §4.4: per-field length and email rules for organisations and contacts, returning a field→message map · src/StageFright.Core/Modules/Organisations/ExternalOrganisationValidationService.cs
- [x] **T008** [P] Create the `OrganisationsResource` area: the marker class, the neutral `.resx` holding every `Organisations_*` key from contract §7, and real translations in de-DE, en-US, es-ES, fr-FR, it-IT, ja-JP and pl-PL · src/StageFright.UI/Resources/Strings/OrganisationsResource.cs, src/StageFright.UI/Resources/Strings/OrganisationsResource{,.de-DE,.en-US,.es-ES,.fr-FR,.it-IT,.ja-JP,.pl-PL}.resx
- [x] **T009** [P] Add `Nav_Organisations_Title` and `Nav_Organisations_ShortLabel` to the neutral resource and all seven cultures · src/StageFright.Core/Modules/Localization/Resources/NavigationResource{,.de-DE,.en-US,.es-ES,.fr-FR,.it-IT,.ja-JP,.pl-PL}.resx
- [x] **T010** [P] Add every `Validation_Organisation_*` and `Validation_OrganisationContact_*` key from contract §4.3/§7 to the neutral resource and all seven cultures · src/StageFright.Core/Modules/Localization/Resources/ValidationResource{,.de-DE,.en-US,.es-ES,.fr-FR,.it-IT,.ja-JP,.pl-PL}.resx
- [x] **T011** [P] Add the Finance picker keys from contract §5.3/§7 to the neutral resource and all seven cultures · src/StageFright.UI/Resources/Strings/FinanceResource{,.de-DE,.en-US,.es-ES,.fr-FR,.it-IT,.ja-JP,.pl-PL}.resx
- [x] **T030** [P] Create `SaveExternalOrganisationRequest` per contract §4.2 (moved here from US1: T007's validation service takes it) · src/StageFright.Core/Modules/Organisations/SaveExternalOrganisationRequest.cs
- [x] **T039** [P] Create `SaveExternalOrganisationContactRequest` per contract §4.2 (moved here from US2: T007's validation service takes it) · src/StageFright.Core/Modules/Organisations/SaveExternalOrganisationContactRequest.cs
- [x] **T074** [P] Add `[ProtoMember(6)] Guid? ExternalOrganisationId` to the journal-entry backup DTO (moved here from Backup: `BackupDtoFieldParityTests` fails once T004 adds the column; T078 still owns the mapping) · src/StageFright.Core/Modules/Settings/Backup/JournalEntryBackupDto.cs

**⟶ Wait for Wave 1 to finish, then:**

**Wave 2 — independent (different files):**

- [x] **T012** [P] Create `ExternalOrganisationConfiguration`: required `Name` with max length 200 and `NOCASE` collation, a unique `Name` index filtered on `[IsDeleted] = 0`, address and email max lengths, and the soft-delete query filter (research §4) · src/StageFright.Data/Configurations/ExternalOrganisationConfiguration.cs
- [x] **T013** [P] Create `ExternalOrganisationContactConfiguration`: field lengths, a required FK to the organisation with Restrict delete, and the query filter · src/StageFright.Data/Configurations/ExternalOrganisationContactConfiguration.cs
- [x] **T014** [P] Configure the optional `JournalEntry` → `ExternalOrganisation` FK with Restrict delete and an index · src/StageFright.Data/Configurations/JournalEntryConfiguration.cs
- [x] **T015** [P] Regenerate the pseudo-locale with `python scripts/generate-pseudo-locale.py`. Never hand-edit it · src/StageFright.UI/Resources/Strings/*.qps-ploc.resx, src/StageFright.Core/Modules/Localization/Resources/*.qps-ploc.resx
- [x] **T016** [P] Add the `Organisations` area to both guard area dictionaries, so FR-016 is enforced for the new area · tests/StageFright.Localization.Tests/Us1LocalizationGuardTests.cs, tests/StageFright.Localization.Tests/Us2LocalizationGuardTests.cs
- [x] **T017** [P] Create the shared `OrganisationTabs` strip, with Details/Contacts/History links built from an `Id` and an active-tab parameter · src/StageFright.UI/Pages/Organisations/OrganisationTabs.razor, src/StageFright.UI/Pages/Organisations/OrganisationTabs.razor.cs

**⟶ Wait for Wave 2 to finish, then:**

- [x] **T018** Add the `ExternalOrganisations` and `ExternalOrganisationContacts` DbSets · src/StageFright.Data/StageFrightDbContext.cs

**⟶ Wait for T018, then:**

**Wave 4 — independent (different files):**

- [x] **T019** [P] Implement `ExternalOrganisationRepository` on top of `SoftDeletableBaseRepository`: contact counts, the active-name lookup and `GetByIdIncludingArchivedAsync` · src/StageFright.Data/Repositories/ExternalOrganisationRepository.cs
- [x] **T020** [P] Implement `ExternalOrganisationContactRepository`, which lists active contacts by organisation · src/StageFright.Data/Repositories/ExternalOrganisationContactRepository.cs
- [x] **T021** [P] Generate the `AddExternalOrganisations` migration with `dotnet ef migrations add AddExternalOrganisations --project src/StageFright.Data/ --startup-project src/StageFright.App/`, then review its Up/Down · src/StageFright.Data/Migrations/

**⟶ Wait for Wave 4 to finish, then:**

**Wave 5 — independent (different files):**

- [x] **T022** [P] Repository tests: archived organisations are hidden by default, `GetByIdIncludingArchivedAsync` finds an archived organisation, and the contact count excludes removed contacts · tests/StageFright.Data.Tests/Repositories/ExternalOrganisationRepositoryTests.cs
- [x] **T023** [P] Contact repository tests: only active contacts of the requested organisation are returned · tests/StageFright.Data.Tests/Repositories/ExternalOrganisationContactRepositoryTests.cs
- [x] **T024** [P] Migration tests: existing journal entries get a null link, and the filtered `NOCASE` unique index refuses "City Council" against an existing "city council" but allows it against an archived one · tests/StageFright.Data.Tests/Migrations/AddExternalOrganisationsMigrationTests.cs
- [x] **T025** [P] FR-004 validation service tests: each over-length field and each invalid email (organisation and contact) yields a message keyed by its field, and blank optional fields pass · tests/StageFright.Core.Tests/Modules/Organisations/ExternalOrganisationValidationServiceTests.cs

**Checkpoint**: entities, schema, repositories, validation, the tab strip and every new string exist. Stories can start.

## Phase 3: User Story 1 - Keep a register of external organisations (Priority: P1) 🎯 MVP

**Goal**: add, edit, search, archive and restore organisations.
**Independent Test**: on an empty database, add three organisations, edit one, search by part of a name, archive and restore one. No contacts or finance involved.

Files: src/StageFright.Core/Contracts/IExternalOrganisationService.cs, src/StageFright.Core/Modules/Organisations/ExternalOrganisationService.cs, src/StageFright.UI/Pages/Organisations/OrganisationList.razor(.cs), src/StageFright.UI/Pages/Organisations/OrganisationForm.razor(.cs), src/StageFright.UI/Pages/Organisations/OrganisationDetail.razor(.cs)

### Tests (write first, see them fail)

**Wave 1 — independent (different files):**

- [ ] **T026** [P] [US1] Service tests through `IExternalOrganisationService`. FR-002: create with name only and with every field. FR-003: refuse "city council " when "City Council" is active, allow it when that one is archived, and refuse a restore once the name is taken. FR-004: refuse an invalid email at the service boundary. FR-005: update persists. FR-006: archive hides the organisation and restore returns it. FR-014: each action writes its Create/Update/Delete/Restore audit entry · tests/StageFright.Core.Tests/Modules/Organisations/ExternalOrganisationServiceTests.cs
- [ ] **T027** [P] [US1] bUnit list tests. FR-001: the grid shows name, email and contact count. Empty register: shows the empty state and an Add action. FR-007: typing part of a name filters rows as typed, and the "show archived" switch reveals archived rows (driven via `[role=switch]`) · tests/StageFright.UI.Tests/Pages/Organisations/OrganisationListTests.cs
- [ ] **T028** [P] [US1] bUnit form tests. FR-002: save on the new route. FR-005: the edit route loads and saves. FR-004: an invalid email or an over-length name shows its message next to that field and the service is not called. FR-003: a duplicate name shows the duplicate message · tests/StageFright.UI.Tests/Pages/Organisations/OrganisationFormTests.cs
- [ ] **T029** [P] [US1] bUnit detail tests. Details render with a multi-line address. FR-006: Archive calls the service and the button becomes Restore, and Restore with a taken name shows the duplicate message · tests/StageFright.UI.Tests/Pages/Organisations/OrganisationDetailTests.cs

### Implementation

**Wave 1 — independent (different files):**

- [ ] **T031** [P] [US1] Create `IExternalOrganisationService` per the revised contract §4.5 (no history method) · src/StageFright.Core/Contracts/IExternalOrganisationService.cs

**⟶ Wait for Wave 1 to finish, then:**

- [ ] **T032** [US1] Implement `ExternalOrganisationService`: trim, run the validation service, check active names case-insensitively, then create, update, archive and restore, auditing each, plus `GetActiveAsync` and `GetArchivedAsync` · src/StageFright.Core/Modules/Organisations/ExternalOrganisationService.cs

**⟶ Wait for T032, then:**

**Wave 3 — independent (different files):**

- [ ] **T033** [P] [US1] `OrganisationList` at `/organisations`: a search box, a `RadzenSwitch` "show archived", an Add button, and a `RadzenDataGrid` (`AllowSorting="true" AllowPaging="true" PageSize="15" class="rz-shadow-0"`) with an empty state · src/StageFright.UI/Pages/Organisations/OrganisationList.razor, src/StageFright.UI/Pages/Organisations/OrganisationList.razor.cs
- [ ] **T034** [P] [US1] `OrganisationForm` at `/organisations/new` and `/organisations/{Id:guid}/edit`, with field-level messages from the validation service · src/StageFright.UI/Pages/Organisations/OrganisationForm.razor, src/StageFright.UI/Pages/Organisations/OrganisationForm.razor.cs
- [ ] **T035** [US1] `OrganisationDetail` at `/organisations/{Id:guid}`: `OrganisationTabs` (Details active), the details card, and Edit and Archive/Restore buttons · src/StageFright.UI/Pages/Organisations/OrganisationDetail.razor, src/StageFright.UI/Pages/Organisations/OrganisationDetail.razor.cs

**Checkpoint**: US1 works and passes T026–T029 on its own.

## Phase 4: User Story 2 - Record contact people for each organisation (Priority: P2)

**Goal**: add, edit and remove contacts on an organisation.
**Independent Test**: on one existing organisation, add two contacts, edit one and remove the other.

Files: src/StageFright.Core/Contracts/IExternalOrganisationContactService.cs, src/StageFright.Core/Modules/Organisations/ExternalOrganisationContactService.cs, src/StageFright.UI/Pages/Organisations/ContactEditor.razor(.cs), src/StageFright.UI/Pages/Organisations/OrganisationContacts.razor(.cs)

### Tests (write first, see them fail)

**Wave 1 — independent (different files):**

- [ ] **T036** [P] [US2] Service tests. FR-008: add with all five fields, a name-only contact saves, a missing name is refused, an invalid contact email is refused, edit persists, and adding to an archived organisation is refused. FR-009: remove soft-deletes the contact, which then drops out of the list but stays in data. FR-014: add, edit and remove are audited · tests/StageFright.Core.Tests/Modules/Organisations/ExternalOrganisationContactServiceTests.cs
- [ ] **T037** [P] [US2] bUnit contacts page tests. FR-008: the grid lists name, role, phone, mobile and email. FR-009: Remove takes the row out of the grid · tests/StageFright.UI.Tests/Pages/Organisations/OrganisationContactsTests.cs
- [ ] **T038** [P] [US2] bUnit editor tests. FR-004/FR-008: a blank name or invalid email shows its field message and does not submit, and a valid contact raises the save callback · tests/StageFright.UI.Tests/Pages/Organisations/ContactEditorTests.cs

### Implementation

**Wave 1 — independent (different files):**

- [ ] **T040** [P] [US2] Create `IExternalOrganisationContactService` per contract §4.6 · src/StageFright.Core/Contracts/IExternalOrganisationContactService.cs

**⟶ Wait for Wave 1 to finish, then:**

- [ ] **T041** [US2] Implement `ExternalOrganisationContactService`: validate, refuse an archived parent, then add, update and soft-remove, auditing each · src/StageFright.Core/Modules/Organisations/ExternalOrganisationContactService.cs

**⟶ Wait for T041, then:**

- [ ] **T042** [US2] Create the inline `ContactEditor` for add/edit, with five fields and field-level messages · src/StageFright.UI/Pages/Organisations/ContactEditor.razor, src/StageFright.UI/Pages/Organisations/ContactEditor.razor.cs

**⟶ Wait for T042, then:**

- [ ] **T043** [US2] `OrganisationContacts` at `/organisations/{Id:guid}/contacts`: `OrganisationTabs` (Contacts active), a contacts `RadzenDataGrid`, and `ContactEditor` · src/StageFright.UI/Pages/Organisations/OrganisationContacts.razor, src/StageFright.UI/Pages/Organisations/OrganisationContacts.razor.cs

**Checkpoint**: US2 works and passes T036–T038 on its own.

## Phase 5: User Story 3 - See the financial history with an organisation (Priority: P2)

**Goal**: optionally link expense and income entries to an organisation, and show its history and totals.
**Independent Test**: record one expense to an organisation and one income entry from it, then confirm both rows and totals on its History tab.
**Depends on**: US1 (`IExternalOrganisationService.GetActiveAsync` feeds the pickers).

Files: src/StageFright.Core/Modules/Finance/RecordExpenseRequest.cs, src/StageFright.Core/Modules/Finance/RecordIncomeRequest.cs, src/StageFright.Core/Modules/Finance/ExpensePaymentService.cs, src/StageFright.Core/Modules/Finance/IncomeEntryService.cs, src/StageFright.Core/Contracts/IJournalEntryRepository.cs, src/StageFright.Data/Repositories/JournalEntryRepository.cs, src/StageFright.Core/Modules/Organisations/OrganisationHistoryEntry.cs, src/StageFright.Core/Modules/Organisations/OrganisationHistory.cs, src/StageFright.Core/Contracts/IOrganisationHistoryService.cs, src/StageFright.Core/Modules/Organisations/OrganisationHistoryService.cs, src/StageFright.UI/Pages/Finance/ExpensePaymentModel.cs, src/StageFright.UI/Pages/Finance/ExpensePaymentPage.razor(.cs), src/StageFright.UI/Pages/Finance/RecordIncomeModel.cs, src/StageFright.UI/Pages/Finance/RecordIncome.razor(.cs), src/StageFright.UI/Pages/Organisations/OrganisationHistoryPage.razor(.cs)

### Tests (write first, see them fail)

**Wave 1 — independent (different files):**

- [ ] **T044** [P] [US3] Expense service tests. FR-010: the link is stamped on the journal entry. SC-004: lines are identical with and without an organisation, for both taxable and exempt entries. An archived organisation raises `ValidationException`. An unknown ID raises `EntityNotFoundException` · tests/StageFright.Core.Tests/Modules/Finance/ExpensePaymentServiceTests.cs
- [ ] **T045** [P] [US3] Income service tests, the same four cases as T044 for `RecordIncomeAsync` · tests/StageFright.Core.Tests/Modules/Finance/IncomeEntryServiceTests.cs
- [ ] **T046** [P] [US3] Repository tests: `GetByExternalOrganisationAsync` returns only that organisation's entries, with lines and accounts loaded, newest first, including when the organisation is archived · tests/StageFright.Data.Tests/Repositories/JournalEntryRepositoryTests.cs
- [ ] **T047** [P] [US3] History service tests. FR-011: entries are newest first, with total paid and total received. FR-012: a taxable three-line expense counts its gross once, not every line. An organisation with no entries gets zero totals · tests/StageFright.Core.Tests/Modules/Organisations/OrganisationHistoryServiceTests.cs
- [ ] **T048** [P] [US3] bUnit history tab tests. FR-011: the grid shows date, description, account and amount, plus "Total paid"/"Total received". FR-013: amounts follow `MoneyFormatter` configured to a non-AUD currency. Archived organisation: the history still renders · tests/StageFright.UI.Tests/Pages/Organisations/OrganisationHistoryPageTests.cs
- [ ] **T049** [P] [US3] bUnit expense page tests. FR-010: the picker lists only active organisations. Choosing one fills an empty payee. A typed payee is not overwritten. The filled payee stays editable and stays put when the choice is cleared. The chosen ID reaches the request · tests/StageFright.UI.Tests/Pages/Finance/ExpensePaymentPageTests.cs
- [ ] **T050** [P] [US3] bUnit income page tests. FR-010: the picker lists only active organisations and the chosen ID reaches the request. No choice sends null · tests/StageFright.UI.Tests/Pages/Finance/RecordIncomeTests.cs
- [ ] **T051** [P] [US3] Integration journey. SC-003: record a $500 expense to "Town Hall" and a $200 income entry from it, then the history totals are exactly $500 paid and $200 received. An unlinked expense appears in no history · tests/StageFright.Integration.Tests/Scenarios/OrganisationFinancialHistoryJourneyTests.cs

### Implementation

**Wave 1 — independent (different files):**

- [ ] **T052** [P] [US3] Add an optional `ExternalOrganisationId` to `RecordExpenseRequest` · src/StageFright.Core/Modules/Finance/RecordExpenseRequest.cs
- [ ] **T053** [P] [US3] Add an optional `ExternalOrganisationId` to `RecordIncomeRequest` · src/StageFright.Core/Modules/Finance/RecordIncomeRequest.cs
- [ ] **T054** [P] [US3] Declare `GetByExternalOrganisationAsync` · src/StageFright.Core/Contracts/IJournalEntryRepository.cs
- [ ] **T055** [P] [US3] Create `OrganisationHistoryEntry` per data-model §4 · src/StageFright.Core/Modules/Organisations/OrganisationHistoryEntry.cs
- [ ] **T056** [P] [US3] Create `OrganisationHistory` (entries plus totals) per data-model §4 · src/StageFright.Core/Modules/Organisations/OrganisationHistory.cs
- [ ] **T057** [P] [US3] Add `ExternalOrganisationId` to the expense form model · src/StageFright.UI/Pages/Finance/ExpensePaymentModel.cs
- [ ] **T058** [P] [US3] Add `ExternalOrganisationId` to the income form model · src/StageFright.UI/Pages/Finance/RecordIncomeModel.cs

**⟶ Wait for Wave 1 to finish, then:**

**Wave 2 — independent (different files):**

- [ ] **T059** [P] [US3] `ExpensePaymentService`: validate the organisation before the transaction (research §6), then set `JournalEntry.ExternalOrganisationId`, with no other posting change · src/StageFright.Core/Modules/Finance/ExpensePaymentService.cs
- [ ] **T060** [P] [US3] `IncomeEntryService`: the same as T059 · src/StageFright.Core/Modules/Finance/IncomeEntryService.cs
- [ ] **T061** [P] [US3] Implement `GetByExternalOrganisationAsync`, with lines and their accounts included and ordered by date descending · src/StageFright.Data/Repositories/JournalEntryRepository.cs
- [ ] **T062** [P] [US3] Create `IOrganisationHistoryService` with `GetHistoryAsync(Guid organisationId, CancellationToken ct = default)` · src/StageFright.Core/Contracts/IOrganisationHistoryService.cs

**⟶ Wait for Wave 2 to finish, then:**

**Wave 3 — independent (different files):**

- [ ] **T063** [P] [US3] Implement `OrganisationHistoryService`. Each entry's amount is the sum of its `DebitAmount` (research §2) and its direction comes from `JournalEntryType`. Its account is the entry's non-bank, non-system line · src/StageFright.Core/Modules/Organisations/OrganisationHistoryService.cs
- [ ] **T064** [P] [US3] Add the organisation `InputSelect` (active only) to the expense page, filling the payee only while it is blank (FR-010) · src/StageFright.UI/Pages/Finance/ExpensePaymentPage.razor, src/StageFright.UI/Pages/Finance/ExpensePaymentPage.razor.cs
- [ ] **T065** [P] [US3] Add the organisation `InputSelect` (active only) to the income page · src/StageFright.UI/Pages/Finance/RecordIncome.razor, src/StageFright.UI/Pages/Finance/RecordIncome.razor.cs

**⟶ Wait for T063, then:**

- [ ] **T066** [US3] `OrganisationHistoryPage` at `/organisations/{Id:guid}/history`: `OrganisationTabs` (History active), a `RadzenDataGrid` with a `MoneyFormatter.Format` amount template, and the two totals · src/StageFright.UI/Pages/Organisations/OrganisationHistoryPage.razor, src/StageFright.UI/Pages/Organisations/OrganisationHistoryPage.razor.cs

**Checkpoint**: US3 works and passes T044–T051 on its own.

## Phase 6: Backup and restore (FR-015, cross-cutting)

Files: src/StageFright.Core/BackupSchema.cs, src/StageFright.Core/Modules/Settings/Backup/ExternalOrganisationBackupDto.cs, src/StageFright.Core/Modules/Settings/Backup/ExternalOrganisationContactBackupDto.cs, src/StageFright.Core/Modules/Settings/Backup/BackupSnapshot.cs, src/StageFright.Core/Modules/Settings/Backup/BackupEnvelope.cs, src/StageFright.Data/Repositories/BackupRepository.cs, src/StageFright.Core/Modules/Settings/BackupService.cs

### Tests (write first, see them fail)

**Wave 1 — independent (different files):**

- [ ] **T067** [P] FR-015 schema tests: the current version is `1.3.0`, `1.2.0` and `1.3.0` files are restorable, and `1.4.0` is not · tests/StageFright.Core.Tests/Modules/Settings/BackupSchemaTests.cs
- [ ] **T068** [P] FR-015 service tests: the envelope carries organisations, contacts and journal-entry links, with entity counts. `SchemaVersion` comes from `BackupSchema.CurrentSchemaVersion`. A `1.2.0` envelope with null lists maps to an empty register · tests/StageFright.Core.Tests/Modules/Settings/BackupServiceTests.cs
- [ ] **T069** [P] FR-015 repository tests: the snapshot includes archived organisations and removed contacts, and a restore upserts organisations then contacts before journal entries, with no FK failure · tests/StageFright.Data.Tests/Repositories/BackupRepositoryTests.cs
- [ ] **T070** [P] SC-005 integration round trip: back up, wipe, restore, and every organisation, contact and link is unchanged · tests/StageFright.Integration.Tests/Scenarios/OrganisationBackupRoundTripTests.cs

### Implementation

**Wave 1 — independent (different files):**

- [ ] **T071** [P] Bump `CurrentSchemaVersion` to `1.3.0` · src/StageFright.Core/BackupSchema.cs
- [ ] **T072** [P] Create `ExternalOrganisationBackupDto` per data-model §5 · src/StageFright.Core/Modules/Settings/Backup/ExternalOrganisationBackupDto.cs
- [ ] **T073** [P] Create `ExternalOrganisationContactBackupDto` per data-model §5 · src/StageFright.Core/Modules/Settings/Backup/ExternalOrganisationContactBackupDto.cs
- [ ] **T075** [P] Add the `ExternalOrganisations` and `ExternalOrganisationContacts` lists · src/StageFright.Core/Modules/Settings/Backup/BackupSnapshot.cs

**⟶ Wait for Wave 1 to finish, then:**

**Wave 2 — independent (different files):**

- [ ] **T076** [P] Add envelope members 27 `ExternalOrganisations` and 28 `ExternalOrganisationContacts` · src/StageFright.Core/Modules/Settings/Backup/BackupEnvelope.cs
- [ ] **T077** [P] Read both sets with `IgnoreQueryFilters()`, and upsert organisations then contacts ahead of `JournalEntries` · src/StageFright.Data/Repositories/BackupRepository.cs

**⟶ Wait for Wave 2 to finish, then:**

- [ ] **T078** Map both sets and the link in each direction, add the entity counts, default null lists, and replace the `"1.2.0"` literal with `BackupSchema.CurrentSchemaVersion` · src/StageFright.Core/Modules/Settings/BackupService.cs

**Checkpoint**: backup and restore carry the register, and pre-feature backups still restore.

## Phase 7: Polish and integration

Files: src/StageFright.Core/Modules/Organisations/OrganisationsMenuItemProvider.cs, src/StageFright.Core/Modules/Rehearsals/RehearsalMenuItemProvider.cs, src/StageFright.Core/Modules/Events/EventsMenuItemProvider.cs, src/StageFright.Core/Modules/Finance/FinanceMenuItemProvider.cs, src/StageFright.UI/Modules/Reports/ReportMenuItemProvider.cs, src/StageFright.App/MauiProgram.cs, CLAUDE.md, .specify/memory/constitution.md, docs/localization/adding-a-language.md

### Tests (write first, see them fail)

- [ ] **T079** [P] FR-001 menu tests: one item titled from `Nav_Organisations_Title` with route `/organisations`, and provider orders run Dashboard 0, Members 1, Organisations 2, Rehearsals 3, Events 4, Finance 5, Reports 6, Settings 999 · tests/StageFright.Core.Tests/Modules/Organisations/OrganisationsMenuItemProviderTests.cs

### Implementation

**Wave 1 — independent (different files):**

- [ ] **T080** [P] Create `OrganisationsMenuItemProvider` with `DisplayOrder => 2` · src/StageFright.Core/Modules/Organisations/OrganisationsMenuItemProvider.cs
- [ ] **T081** [P] Shift `DisplayOrder` up by one: Rehearsals to 3, Events to 4, Finance to 5, Reports to 6 · src/StageFright.Core/Modules/Rehearsals/RehearsalMenuItemProvider.cs, src/StageFright.Core/Modules/Events/EventsMenuItemProvider.cs, src/StageFright.Core/Modules/Finance/FinanceMenuItemProvider.cs, src/StageFright.UI/Modules/Reports/ReportMenuItemProvider.cs
- [ ] **T082** [P] Register both repositories, the validation, organisation, contact and history services, and the menu provider per contract §8 · src/StageFright.App/MauiProgram.cs
- [ ] **T083** [P] Update docs. CLAUDE.md: add `Organisations` to the modules list, raise the entity count from 20 to 22 and name the two new entities, add the soft-delete note, and count the new resource area. Constitution §4.1: the current-modules list. The adding-a-language guide: its area list · CLAUDE.md, .specify/memory/constitution.md, docs/localization/adding-a-language.md

**⟶ Wait for Wave 1 to finish, then:**

- [ ] **T084** Verify in the live app over CDP (CLAUDE.md gotcha). SC-001: add an organisation with an address, email and one contact in under 2 minutes. SC-002: search a seeded register of 200 by part of a name. Confirm the payee fill on the expense page and the menu position · (no file)
- [ ] **T085** Validate against the Success Criteria: run a full `dotnet build -t:Rebuild` with no new warnings, then `dotnet test`. SC-003, SC-004 and SC-005 come from T051, T044/T045 and T070. SC-006 and FR-016 come from the localization guard suite · (no file)

## Dependencies & Execution Order

- **Phase order**: Setup → Foundational → US1 → (US2 ∥ US3) → Backup → Polish. US2 needs only Foundational. US3 needs US1's `IExternalOrganisationService`. Backup needs only Foundational and can run beside the stories. Polish needs everything.
- **Phase 2**: Wave 1 (T002–T011, T030, T039, T074) → Wave 2 (T012–T017) → T018 → Wave 4 (T019–T021) → Wave 5 tests (T022–T025).
- **US1**: tests T026–T029 → T031 → T032 → Wave 3 (T033–T035).
- **US2**: tests T036–T038 → T040 → T041 → T042 → T043.
- **US3**: tests T044–T051 → Wave 1 (T052–T058) → Wave 2 (T059–T062) → Wave 3 (T063–T065) → T066.
- **Backup**: tests T067–T070 → Wave 1 (T071–T073, T075) → Wave 2 (T076, T077) → T078.
- **Polish**: T079 → Wave 1 (T080–T083) → T084 → T085.
- **File ownership**: every path appears under exactly one phase. Phase 2 owns all resource files, entities, configurations, the DbContext, repositories (except the journal-entry history query, which US3 owns) and `OrganisationTabs`. Polish owns `MauiProgram` and every menu provider.
