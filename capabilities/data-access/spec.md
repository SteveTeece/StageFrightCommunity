# Data Access — Living Spec

> [DRAFT] Surface-first draft from existing code — every requirement is observed from the code surface unless tagged otherwise. Review before trusting.

## Purpose

This capability is the single, centrally-owned data-access layer through which all application and plugin code reads and writes persisted state. Without it, soft-delete filtering, GL double-entry integrity, and exception-boundary translation would each be re-implemented inconsistently per module, and plugin schemas would collide with the core SQLite database.

## Requirements

### Repositories are centrally owned, one per entity

Every persisted entity SHALL have exactly one repository implementation living in `StageFright.Data/Repositories/`, never inside a `StageFright.Core/Modules/<X>/` slice — this is a deliberate deviation from pure vertical-slice architecture (FR-042), keeping all EF Core access behind a single, uniformly-testable boundary regardless of which module consumes it.

#### Scenario: a new module needs persistence
- **WHEN** a new module slice under `StageFright.Core/Modules/` needs to read or write an entity
- **THEN** it depends on a repository interface implemented centrally in `StageFright.Data/Repositories/`, not on a repository it owns itself

### Soft-deletable entities are excluded from normal reads by a global filter, not per-query code

Entities that support soft-delete SHALL declare a global EF Core query filter on `IsDeleted` in their `IEntityTypeConfiguration`, so every LINQ query against that `DbSet` automatically excludes archived rows without repository authors needing to remember a `WHERE` clause. Code that must see archived rows SHALL opt out explicitly via `IgnoreQueryFilters()`.

#### Scenario: a normal list query
- **WHEN** a repository runs `GetAllAsync` or any plain LINQ query against a soft-deletable `DbSet` (e.g. Members, Events, Rehearsals, Accounts)
- **THEN** archived rows are absent from the results
- **AND** no repository method needs its own `IsDeleted` predicate to achieve this

#### Scenario: recovering or auditing archived data
- **WHEN** code needs to see a soft-deleted row (`RestoreAsync`, duplicate/uniqueness checks, account-number reuse checks, full backup export)
- **THEN** it calls `IgnoreQueryFilters()` explicitly rather than relying on the default filter

#### Scenario: a child entity's lifecycle is owned by its parent
- **WHEN** `ReconciliationLine` rows belong to a `BankReconciliation`
- **THEN** their query filter derives from the parent's `IsDeleted` (`!l.Reconciliation!.IsDeleted`) rather than carrying an independent flag, so archiving the parent implicitly hides its lines

### Financial and audit records opt out of soft-delete, each for a different reason

`Fee`, `Payment`, `Transaction`, and `JournalEntry` SHALL carry no soft-delete fields and no query filter — they are permanently immutable once written; corrections happen via reversing GL entries, never edits or deletes. `AuditTrailEntry` likewise carries no soft-delete fields, but is the one entity genuinely hard-deleted, purged on a time-based retention policy rather than by user action.

#### Scenario: a fee or payment needs correcting
- **WHEN** a posted financial record turns out to be wrong
- **THEN** a new reversing transaction is posted alongside it — the original row is never updated or removed

#### Scenario: audit history ages out
- **WHEN** audit trail entries pass the retention cutoff
- **THEN** `AuditTrailRepository.PurgeOlderThanAsync` performs a real SQL delete — the only repository method in this capability that hard-deletes rows

### Archive/Restore is a reusable base contract that soft-deletable repositories opt into individually

`SoftDeletableBaseRepository<TEntity>` SHALL provide `ArchiveAsync`/`RestoreAsync`/`GetArchivedAsync` via reflection over the shared `IsDeleted`/`DeletedAt`/`DeletedBy`/`UpdatedAt` properties. Inheriting it is not mandatory for every entity that has those fields — an entity whose deletion rule is more than "flip a flag" implements its own path instead.

#### Scenario: generic archive/restore
- **WHEN** a repository (Account, CommitteeMembership, Event, EventType, Member, ParticipationRecord, Rehearsal) extends `SoftDeletableBaseRepository<T>`
- **THEN** `ArchiveAsync` rejects an already-archived entity with a `ValidationException`, and `RestoreAsync` locates the row with `IgnoreQueryFilters()`

#### Scenario: a state-dependent deletion rule
- **WHEN** an entity's deletion is conditional on its own state — a `BankReconciliation` may only be deleted while still `Draft`, never once `Finalised`
- **THEN** its repository (`BankReconciliationRepository.SoftDeleteDraftAsync`) sets the soft-delete fields directly against that precondition instead of using the shared reflection-based base class
- **AND** an entity whose soft-delete fields exist structurally but are never exercised by any workflow (`AttendanceRecord`) is deliberately given only `BaseRepository`, with no archive path exposed at all [inferred: confirmed by the absence of an archive method here, not by reading the calling workflows]

### Repository writes translate framework exceptions into domain exceptions before they leave the DAL

Repository writes SHALL catch EF Core / ADO.NET exceptions and re-throw a `StageFright.Core.Exceptions` type — `DuplicateEntityException` for unique-constraint violations, `ConcurrencyException` for concurrency conflicts, `DataAccessException` for everything else — so callers outside `StageFright.Data` never have to handle a raw `DbException`.

#### Scenario: a unique-constraint violation on insert
- **WHEN** `SaveChangesAsync` throws a `DbUpdateException` wrapping a SQLite `UNIQUE` violation during `AddAsync`
- **THEN** the repository raises `DuplicateEntityException` carrying the entity type and operation name, not the raw EF exception

#### Scenario: a concurrent update conflict
- **WHEN** `UpdateAsync` hits a `DbUpdateConcurrencyException`
- **THEN** the repository raises `ConcurrencyException` identifying the entity type and operation

### Multi-step writes run inside one all-or-nothing transaction

`UnitOfWork.ExecuteInTransactionAsync` SHALL wrap a caller-supplied operation in a single database transaction, rolling back on any failure. A `GLBalanceException` passes through unwrapped as a first-class domain signal; any other unexpected exception is wrapped as `DataAccessException` so a partial multi-step write can never survive the transaction boundary.

#### Scenario: an operation throws partway through
- **WHEN** the operation delegate raises any exception after some writes have already been staged in the `DbContext`
- **THEN** the transaction is rolled back before the exception (or its `DataAccessException` wrapper) propagates to the caller

#### Scenario: a GL imbalance is detected mid-operation
- **WHEN** `GLBalanceException` is thrown inside the wrapped operation
- **THEN** the transaction still rolls back, but the original `GLBalanceException` propagates unchanged rather than being re-wrapped

### GL postings are rejected before persistence unless every debit is matched by an equal credit

`GLRepository.AddPairAsync` / `AddBalancedSetAsync` SHALL validate — before calling `SaveChangesAsync` — that every line has exactly one non-zero side, no line is negative, and Σdebits equals Σcredits across the set, throwing `GLBalanceException` otherwise. An unbalanced posting must never reach the database.

#### Scenario: an imbalanced pair
- **WHEN** a caller attempts to post a debit/credit pair whose amounts differ
- **THEN** `AddPairAsync` throws `GLBalanceException` and nothing is written to `Transactions`

#### Scenario: a line with both sides non-zero or both zero
- **WHEN** any line in a balanced set has debit and credit either both non-zero or both zero
- **THEN** the whole set is rejected with `GLBalanceException` before insert

### Balances are computed from the canonical account id, not the denormalized GL account string

Member and account balance queries (`GetMemberBalanceAsync`, `GetAccountBalanceAsync`, `GetOutstandingByFeeTypeAsync`, `GetOutstandingMemberCountAsync`) SHALL filter on `Transaction.AccountId` against the live `Account`/`SystemAccounts` identity, not the denormalized `GLAccount` string captured at posting time, so balances stay correct even for legacy rows whose `GLAccount` string predates a chart-of-accounts renumbering.

#### Scenario: a legacy transaction with a stale GLAccount string
- **WHEN** a transaction was posted before an account renumbering and still carries an old `GLAccount` value
- **THEN** balance queries keyed on `AccountId` still include it correctly, because they never read the string field

### Archival is blocked for accounts still in use or marked as system accounts

`AccountRepository.ArchiveAsync` SHALL reject archiving a system account (`IsSystem`) or any account still referenced by at least one transaction. Comparable repositories expose an equivalent "is referenced" check (`EventTypeRepository.IsReferencedByEventsAsync`) before allowing removal of a referenced parent, because the FK's `DeleteBehavior.Restrict` alone would only surface as an opaque database error at save time, not an actionable validation message.

#### Scenario: archiving an account with posted transactions
- **WHEN** an account referenced by at least one `Transaction` is archived
- **THEN** a `ValidationException` naming the reason is thrown before any database write is attempted

#### Scenario: archiving a system account
- **WHEN** any of the seeded system accounts (Cash, Member Receivable, Bad Debt Expense, Tax Collected, Tax Receivable, Opening Balance Equity, Accumulated Surplus) is archived
- **THEN** the operation is rejected regardless of whether it is referenced by transactions

### Account numbers are allocated deterministically within a fixed range per account type

`GetNextAccountNumberAsync` SHALL compute the next number as one past the current maximum within that type's fixed numeric range — considering archived accounts too, via `IgnoreQueryFilters()`, since they retain their number — and SHALL throw `DataIntegrityException` when the range is exhausted or a collision is detected, never silently reusing or skipping a number.

#### Scenario: a range is exhausted
- **WHEN** the highest existing account number in a type's range is already at the range ceiling
- **THEN** `GetNextAccountNumberAsync` throws `DataIntegrityException` instead of returning an out-of-range or duplicate number

### Seeded system accounts are deterministic so migrations stay reproducible

The chart of accounts' system rows SHALL be seeded via `HasData` in `OnModelCreating` using fixed GUIDs and one fixed, never-changing seed timestamp, so that regenerating a migration after unrelated model changes never produces a spurious `UpdateData` migration for seed rows.

#### Scenario: an unrelated model change triggers a new migration
- **WHEN** a new EF Core migration is generated after adding or changing an unrelated entity
- **THEN** the seed-data diff for system accounts is empty, because neither their ids nor their `CreatedAt`/`UpdatedAt` values ever change between builds

### Plugin schemas merge into the shared database without colliding with core or each other

`PluginMigrationRunner` SHALL run each discovered `IDataAccessProvider`'s own `DbContext` migrations against the same SQLite connection string as the core context, but under a distinct `__EFMigrationsHistory_<PluginName>` history table per plugin — so plugin schema additions land in the one physical database file while each plugin (and core) tracks its own applied-migrations history independently.

#### Scenario: two unrelated plugins are discovered at startup
- **WHEN** both plugins' `IDataAccessProvider` implementations are found
- **THEN** each runs its migrations against its own history table, unaware of and unaffected by the other's applied migrations

#### Scenario: a plugin's migration fails
- **WHEN** a plugin's `DbContextType` fails to construct or its `MigrateAsync` call throws
- **THEN** the failure is caught, wrapped as `PluginLoadException`, logged, and startup continues with that plugin's data access skipped — it never blocks core startup

### Backup and restore intentionally bypass the soft-delete filter and use upsert, not insert

`BackupRepository` SHALL read every soft-deletable entity with `IgnoreQueryFilters()` so a full backup snapshot includes archived rows, and SHALL restore via a primary-key upsert — update if the id already exists, otherwise add — inside a single `SaveChangesAsync`, after clearing the change tracker so re-attached POCOs from a backup file don't collide with already-tracked instances of the same key.

#### Scenario: restoring a backup over an existing database
- **WHEN** a backup file is restored and some ids already exist in the target database
- **THEN** those rows are updated in place, new ids are inserted, and no previously-archived data is lost or duplicated

## Uncovered

- `src/StageFright.Data/Migrations/` was intentionally not read file-by-file — these are EF Core generated migrations (six migration pairs, `<timestamp>_<Description>.cs` + matching `.Designer.cs`, plus one `StageFrightDbContextModelSnapshot.cs`), not hand-authored contracts, so they were only listed for naming/organization, not read in detail: `20260611224108_InitialCreate`, `20260627031057_AddShowParticipationGraphs`, `20260705071238_ConvertCategoriesToAccounts`, `20260705120006_AddJournalEntries`, `20260705213129_AddBankReconciliation`, `20260705222449_AddGst`, `20260708050050_AddAbnToSettings`, `20260726053354_SplitMemberNameIntoFirstLastName`.
- Every other file in scope — all 16 files under `Configurations/`, all 18 files under `Repositories/` (including `BaseRepository.cs` and `SoftDeletableBaseRepository.cs`), `PluginData/PluginMigrationRunner.cs`, `StageFrightDbContext.cs`, `StageFrightDbContextFactory.cs`, and `UnitOfWork.cs` — was read in full.
