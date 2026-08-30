# Domain Model — Living Spec

> [DRAFT] Surface-first draft from existing code — every requirement is observed from the code surface unless tagged otherwise. Review before trusting.

## Purpose

The domain model — entities, enums, custom exceptions, and repository/service contracts in `StageFright.Core` — is the single shared vocabulary every module and layer builds on. Without a consistent PK strategy, audit-timestamp convention, soft-delete rule, and repository/service split, each module would invent its own persistence and error-handling conventions, and the DAL, UI, and reporting layers would no longer be able to treat entities uniformly.

## Requirements

### Entities are identified by a domain-assigned GUID primary key

Every entity SHALL expose a `Guid Id` property as its primary key. This keeps identity generation independent of the database provider and lets services create fully-formed entity graphs (e.g. a `Fee` plus its GL pair) before any row is persisted.

#### Scenario: a new entity is created in a service

- **WHEN** an application service constructs a new entity instance
- **THEN** it assigns the `Id` itself rather than relying on a database-generated identity column

### Audit timestamps are present on every entity, but UpdatedAt only where edits are possible

Every entity SHALL carry a `CreatedAt` UTC timestamp. Entities that support post-creation edits (directly, or indirectly via archive/restore) SHALL also carry `UpdatedAt`; entities that are append-only and never edited after insert MAY omit it. `AuditTrailEntry` is the one entity that uses a differently-named timestamp (`Timestamp`) instead of `CreatedAt`, reflecting its role as a log record rather than a mutable domain object `[inferred]`.

#### Scenario: an editable entity is updated

- **WHEN** a service updates an entity that supports edits or archival (e.g. `Member`, `Account`, `Rehearsal`)
- **THEN** the entity's `UpdatedAt` is refreshed to the current UTC time

#### Scenario: an append-only record is inserted

- **WHEN** a service inserts an immutable, never-edited record (e.g. `Transaction`, `Fee`, `JournalEntry`, `AttendanceRecord`, `ParticipationRecord`, `ReconciliationLine`)
- **THEN** only `CreatedAt` is set; there is no `UpdatedAt` to maintain

### Soft-delete is the default, with narrow, explicitly documented exemptions

Entities SHALL default to soft-delete (`IsDeleted`, `DeletedAt`, `DeletedBy`) rather than physical deletion. The append-only financial ledger — `Fee`, `Payment`, `Transaction`, and `JournalEntry` — is exempt because corrections must happen via GL-reversing entries, never by deleting or hiding the original record. `AuditTrailEntry` is exempt for the opposite reason: it is not preserved indefinitely, but hard-deleted in bulk by a retention-policy purge (`IAuditTrailRepository.PurgeOlderThanAsync`), so soft-delete bookkeeping would serve no purpose.

#### Scenario: a financial record needs correction

- **WHEN** a `Fee`, `Payment`, `Transaction`, or `JournalEntry` needs to be corrected
- **THEN** the correction is made by posting a new GL-reversing entry, never by deleting or soft-deleting the original row

#### Scenario: audit log retention runs

- **WHEN** the startup retention purge runs against entries older than the cutoff
- **THEN** matching `AuditTrailEntry` rows are physically removed from the database, not soft-deleted

### Some historical records carry soft-delete fields as reserved schema without exposing the operations that would use them

`AttendanceRecord` carries `IsDeleted`/`DeletedAt`/`DeletedBy` for schema-level future-proofing, but its repository contract (`IAttendanceRepository`) intentionally does not extend `ISoftDeletableRepository<T>` and exposes no archive/restore operation — attendance records are permanently immutable in practice even though the columns exist `[inferred]`. This is a distinct convention from the no-fields-at-all exemption above: here the fields are present but functionally inert.

#### Scenario: attendance is recorded

- **WHEN** a batch of `AttendanceRecord`s is saved for a rehearsal
- **THEN** no code path in the repository or service contracts ever sets `IsDeleted` on those records

`ParticipationRecord` looks structurally identical to `AttendanceRecord` (same batch-save shape, same event/member pairing) but its repository *does* extend `ISoftDeletableRepository<T>`, exposing archive/restore. [NEEDS CLARIFICATION: is ParticipationRecord's soft-delete support intentional and actually exercised by a workflow, or is it a leftover from copying the AttendanceRecord pattern before the "permanently immutable" decision was made for attendance only?]

### Repository contracts are pure data-access surfaces; service contracts own business rules

Repository interfaces (`I<Entity>Repository`) SHALL expose only CRUD and entity-specific query/mutation operations with no business validation or cross-entity orchestration. Service interfaces (`I<Entity>Service` / workflow services like `IFeeService`, `IPaymentService`) SHALL own validation, multi-repository coordination, and GL/transactional orchestration, and are the only contracts that raise domain validation exceptions such as `ValidationException`.

#### Scenario: a new business rule is added

- **WHEN** a new validation or cross-entity rule is introduced for an existing entity
- **THEN** it is added to the corresponding service contract's implementation, not to the repository

#### Scenario: a repository method is reviewed

- **WHEN** a method on an `I<Entity>Repository` interface is read
- **THEN** it describes a direct data operation (get, add, update, exists, archive) with no orchestration across other entity types

### A shared generic contract standardizes CRUD and archive/restore semantics

`IRepository<TEntity>` SHALL define the common CRUD surface (`GetByIdAsync`, `GetAllAsync`, `AddAsync`, `UpdateAsync`) used by every repository contract that has one, and `ISoftDeletableRepository<TEntity>` SHALL extend it with `ArchiveAsync`/`RestoreAsync`/`GetArchivedAsync` for entities that support archival. A concrete repository contract's choice of base (or neither, for immutable-only entities like `IFeeRepository`, `IPaymentRepository`, `IJournalEntryRepository`) is the definitive signal for whether that entity is editable, archivable, or fully immutable.

#### Scenario: a new archivable entity is added

- **WHEN** a new entity needs soft-delete support
- **THEN** its repository contract extends `ISoftDeletableRepository<TEntity>` rather than reimplementing archive/restore methods independently

#### Scenario: a new immutable ledger entity is added

- **WHEN** a new append-only financial entity is added (as `JournalEntry` was)
- **THEN** its repository contract extends neither `IRepository<T>` nor `ISoftDeletableRepository<T>`, and exposes only `AddAsync`/`GetByIdAsync`-style read/insert operations

### Custom exceptions share one uniform, non-hierarchical shape at every layer boundary

Every type in `StageFright.Core/Exceptions/` SHALL be a `sealed class` deriving directly from `System.Exception` (not from a shared custom base) and SHALL carry the same five members: `EntityType`, `EntityId` (nullable `Guid`), `OperationContext`, a `Timestamp` defaulted to `DateTime.UtcNow`, and a `CorrelationId` defaulted to a new `Guid`. Raw framework exceptions (`DbException`, `IOException`, etc.) MUST be caught and re-thrown as one of these types before crossing a layer boundary, so every exception that reaches the UI carries the same structured diagnostic shape.

#### Scenario: a repository catches a framework exception

- **WHEN** an EF Core operation throws `DbUpdateException` or similar
- **THEN** the repository catches it and re-throws the appropriate `StageFright.Core.Exceptions` type (e.g. `DataAccessException`, `ConcurrencyException`) populated with `EntityType`, `EntityId`, and `OperationContext`

#### Scenario: a new exception type is needed

- **WHEN** a new failure category is identified that doesn't fit an existing exception type
- **THEN** the new type is added as its own `sealed class : Exception` file following the same five-member shape, not as a subclass of an existing exception type

### Fields that snapshot state at posting/accrual time are immutable and never retroactively rewritten

Several entities stamp a value at the moment of creation that reflects "the rule in force then," and that value SHALL NOT be updated even if the organization-wide rule later changes: `Transaction.TaxCode`, `Transaction.GLAccount`, and `Fee.TaxCode` are all fixed at posting/accrual time; `Account.AccountNumber` is fixed at account creation. Historical rows are read literally, never recalculated against current settings.

#### Scenario: sales-tax applicability changes

- **WHEN** an organisation's `Settings.IsTaxApplicable` flag changes after some fees/transactions were already posted
- **THEN** existing `Fee.TaxCode` and `Transaction.TaxCode` values on prior rows are left untouched; only newly created rows use the new setting

#### Scenario: an account's legacy number scheme is queried

- **WHEN** a report reads `Transaction.GLAccount` for a row posted years ago under a legacy numbering scheme
- **THEN** the legacy value is returned as-is; it is never rewritten to match the current chart-of-accounts numbering

## Uncovered

_None — every file in the area was read (16 entity files, 14 enum files, 10 exception files, and all 46 files in `StageFright.Core/Contracts/`)._
