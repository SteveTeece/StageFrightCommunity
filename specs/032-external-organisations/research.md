# Research: External Organisations

The spec has no open `NEEDS CLARIFICATION` items. The five clarifications of 2026-11-20 are settled. These are the design choices the codebase did not already make.

## 1. Where the financial link lives

- **Decision**: a nullable `ExternalOrganisationId` on `JournalEntry`, set when the entry is created.
- **Rationale**: `ExpensePaymentService` and `IncomeEntryService` each create exactly one `JournalEntry` (types `ExpensePayment` and `Income`) with two or three balanced lines. One link per entry is the natural grain. The ledger lines, `Transaction`, stay exactly as they are posted today, so SC-004 holds by construction.
- **Alternatives considered**: a column on every `Transaction` line, rejected because it repeats the same value two or three times and invites drift. A separate link table, rejected because it exists only to allow linking after the fact, which clarification 2 ruled out.

## 2. How a history entry's amount is computed (FR-012)

- **Decision**: amount = sum of `DebitAmount` over the entry's lines. Direction comes from `JournalEntryType`: `ExpensePayment` counts as paid, `Income` as received.
- **Rationale**: every entry is balanced. An expense debits expense (net) and tax receivable and credits the bank with the gross. An income entry debits the deposit account with the gross and credits income (net) and tax collected. Either way the debit total equals the money that crossed the bank account, counted once. This needs no "which line is the bank" lookup.
- **Alternatives considered**: summing all lines' debits and credits, rejected because it double-counts. Picking the bank line by `Account.IsBankAccount`, which works but is an extra rule for the same number.

## 3. Naming: `ExternalOrganisation`, not `Organisation`

- **Decision**: entity and service types are `ExternalOrganisation` and `ExternalOrganisationContact`. The module folder is `Organisations`, the route is `/organisations`, and the UI says "Organisations".
- **Rationale**: `Settings.OrganizationName` (US spelling) already means the society itself. A bare `Organisation` type would be one letter away from that concept and easy to confuse in code review. The en-AU spelling matches the issue and the neutral resx baseline.
- **Alternatives considered**: `Organisation` (clashes with the settings concept, as above). `Supplier` or `Vendor` (too narrow, since councils and sponsors are not suppliers).

## 4. Duplicate-name rule (FR-003)

- **Decision**: the service checks active names with `StringComparison.OrdinalIgnoreCase` after trimming, and raises a localized `ValidationException`. The database adds a filtered unique index on `Name` with `NOCASE` collation, `[IsDeleted] = 0`, as a backstop. The same check runs on restore.
- **Rationale**: this mirrors `EventTypeConfiguration`'s filtered unique index, and the service check gives the friendly message. SQLite `NOCASE` folds ASCII only. The C# check is the stricter rule, so the index can never refuse something the service allowed.
- **Alternatives considered**: index only (the user gets a raw constraint error, which has to be translated), or service only (no protection if two saves race. That is unlikely in a single-user app, but the index costs one line).

## 5. Archiving an organisation does not cascade to contacts

- **Decision**: archiving hides the organisation. Its contacts keep their own state, and the archived organisation's detail still shows them.
- **Rationale**: constitution §3.4 asks for cascade only "if appropriate". Restore must bring the organisation back whole, and a cascade would force restore to guess which contacts were removed by hand before the archive.
- **Alternatives considered**: cascading soft delete with a matching cascading restore, rejected for the ambiguity above.

## 6. Validating the organisation at posting time

- **Decision**: when `ExternalOrganisationId` is set, `ExpensePaymentService` and `IncomeEntryService` load it through `IExternalOrganisationRepository`. A missing ID raises `EntityNotFoundException`. An archived organisation raises `ValidationException`. Both checks run before the transaction opens, beside the existing account checks.
- **Rationale**: the UI offers only active organisations, but the service is the trust boundary. Using the repository contract, not `ExternalOrganisationService`, keeps Finance free of a sibling module's concrete class (§4.1).
- **Alternatives considered**: relying on the FK alone. That misses archived organisations and surfaces a raw constraint failure.

## 7. Menu placement next to Members

- **Decision**: `OrganisationsMenuItemProvider.DisplayOrder = 2`. Rehearsals, Events, Finance and Reports each move up by one (3, 4, 5, 6). Dashboard (0), Members (1) and Settings (999) stay put.
- **Rationale**: the shell sorts providers by `DisplayOrder`. A tie would depend on DI registration order, which is invisible and fragile. No test pins the shifted values, and first-party orders are not a plugin contract.
- **Alternatives considered**: tying with Members at 1 and relying on stable sort order (fragile), or `1.5` (the property is an `int`).

## 8. Backup schema 1.3.0

- **Decision**: bump `BackupSchema.CurrentSchemaVersion` to `1.3.0` and make `BackupService` read the constant instead of its `"1.2.0"` literal. Add `BackupEnvelope` members 27 (`ExternalOrganisations`) and 28 (`ExternalOrganisationContacts`), plus `JournalEntryBackupDto` member 6 (`ExternalOrganisationId`). `BackupRepository` reads both sets with `IgnoreQueryFilters()` and upserts organisations, then contacts, before `JournalEntries`.
- **Rationale**: protobuf members are additive. A 1.2.0 file deserializes with null lists and a null link, which the existing `??= []` defaulting turns into an empty register (FR-015). `BackupSchema.IsRestorable` already accepts older minors. A 1.3.0 file is refused by a 1.2.0 app, which is correct.
- **Alternatives considered**: keeping 1.2.0. An older app would then silently drop organisation data on restore.

## 9. Localization area

- **Decision**: a new `OrganisationsResource` area under `src/StageFright.UI/Resources/Strings/`, a neutral file plus seven cultures, with `qps-ploc` regenerated. The menu keys go in `NavigationResource`, service messages in `ValidationResource`, and the picker label in `FinanceResource`. The area is added to the `Us1`/`Us2` guard dictionaries.
- **Rationale**: this follows the area-per-feature pattern, and the guard tests only see areas listed in their dictionaries.
- **Alternatives considered**: putting the strings in `MembersResource` (wrong owner, and it muddles that area).
