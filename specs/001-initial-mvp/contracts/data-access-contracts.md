# Contracts: Centralized Data Access Layer

**Assembly**: interfaces in `StageFright.Core/Contracts` (domain owns abstractions — Dependency Inversion); implementations in `StageFright.Data/Repositories` (FR-042). All persistence failures are translated to custom exceptions before leaving the DAL (Constitution §5.3): `DataAccessException`, `EntityNotFoundException`, `DuplicateEntityException`, `ConcurrencyException`, `DataIntegrityException`, `GLBalanceException`.

## Base repository contract

```csharp
public interface IRepository<TEntity> where TEntity : class
{
    Task<TEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken ct = default);   // Filters IsDeleted=false where applicable
    Task<TEntity> AddAsync(TEntity entity, CancellationToken ct = default);
    Task UpdateAsync(TEntity entity, CancellationToken ct = default);
}

public interface ISoftDeletableRepository<TEntity> : IRepository<TEntity> where TEntity : class
{
    Task ArchiveAsync(Guid id, string deletedBy, CancellationToken ct = default);   // Sets IsDeleted/DeletedAt/DeletedBy
    Task RestoreAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TEntity>> GetArchivedAsync(CancellationToken ct = default);
}
```

Soft-deletable repositories apply EF global query filters (`IsDeleted == false`) with explicit opt-out (`GetArchivedAsync`, historical reporting queries). Updates to soft-deleted entities are rejected (`ValidationException`) unless restoring.

## Entity repository contracts

```csharp
public interface IMemberRepository : ISoftDeletableRepository<Member>
{
    Task<IReadOnlyList<Member>> GetByStatusAsync(MemberStatus status, CancellationToken ct = default);
    Task<IReadOnlyList<Member>> GetActiveAsOfAsync(DateTime date, CancellationToken ct = default);  // Effective-dates query (FR-007/FR-023)
}

public interface ICommitteeMembershipRepository : ISoftDeletableRepository<CommitteeMembership>
{
    Task<IReadOnlyList<CommitteeMembership>> GetByMemberAsync(Guid memberId, CancellationToken ct = default);
    Task<IReadOnlyList<CommitteeMembership>> GetByYearAsync(int year, CancellationToken ct = default);
    Task SoftDeleteCurrentYearAsync(int year, string deletedBy, CancellationToken ct = default);    // Annual reset (FR-031)
}

public interface IRehearsalRepository : ISoftDeletableRepository<Rehearsal>
{
    Task<Rehearsal?> GetMostRecentPastAsync(DateTime asOf, CancellationToken ct = default);         // Dashboard tile
}

public interface IEventRepository : ISoftDeletableRepository<Event>
{
    Task<Event?> GetMostRecentPastAsync(DateTime asOf, CancellationToken ct = default);
    Task<bool> AgmExistsInYearAsync(int year, CancellationToken ct = default);                      // AGM banner (FR-031)
}

public interface IEventTypeRepository : ISoftDeletableRepository<EventType> { }
public interface IParticipationRepository : ISoftDeletableRepository<ParticipationRecord> { }
public interface IAttendanceRepository : IRepository<AttendanceRecord>
{
    Task<bool> ExistsAsync(Guid rehearsalId, Guid memberId, CancellationToken ct = default);        // Idempotency
    Task AddBatchAsync(IReadOnlyList<AttendanceRecord> records, CancellationToken ct = default);    // Within ambient transaction
}

public interface IFeeRepository                                  // Immutable: no Update, no Delete, no soft-delete
{
    Task<Fee?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Fee> AddAsync(Fee fee, CancellationToken ct = default);
    Task<IReadOnlyList<Fee>> GetByMemberAsync(Guid memberId, CancellationToken ct = default);
    Task<IReadOnlyList<Fee>> GetUnpaidOrderedFifoAsync(Guid memberId, CancellationToken ct = default); // FeeDate, CreatedAt, Id ASC; unpaid derived from GL
    Task<bool> AnnualFeeExistsAsync(Guid memberId, int year, CancellationToken ct = default);
    Task<bool> AttendanceFeeExistsAsync(Guid memberId, Guid rehearsalId, CancellationToken ct = default);
}

public interface IPaymentRepository                              // Immutable except Notes
{
    Task<Payment?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Payment> AddAsync(Payment payment, CancellationToken ct = default);
    Task UpdateNotesAsync(Guid id, string? notes, CancellationToken ct = default);   // Only mutator; audits old/new; bumps UpdatedAt
    Task<IReadOnlyList<Payment>> GetByMemberAsync(Guid memberId, CancellationToken ct = default);
}

public interface IGLRepository                                   // Append-only ledger
{
    Task AddPairAsync(Transaction debit, Transaction credit, CancellationToken ct = default);  // Validates equal amounts
    Task<decimal> GetMemberBalanceAsync(Guid memberId, CancellationToken ct = default);        // Σdebits − Σcredits
    Task<decimal> GetTotalOutstandingAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Transaction>> GetByDateRangeAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<IReadOnlyList<Transaction>> GetByMemberAsync(Guid memberId, DateTime from, DateTime to, CancellationToken ct = default);
    Task<(decimal TotalDebits, decimal TotalCredits)> GetBalanceTotalsAsync(DateTime from, DateTime to, CancellationToken ct = default);
}

public interface ICategoryRepository : ISoftDeletableRepository<Category>
{
    Task<bool> IsReferencedByTransactionsAsync(Guid categoryId, CancellationToken ct = default);   // Archive guard (FR-009)
    Task<string> GetNextGLAccountAsync(CategoryType type, CancellationToken ct = default);          // 10xx / 20xx sequential
    Task ReorderAsync(IReadOnlyList<(Guid Id, int SortOrder)> order, CancellationToken ct = default);
}

public interface ISettingsRepository
{
    Task<Settings?> GetAsync(CancellationToken ct = default);          // Singleton; null before first-run setup
    Task SaveAsync(Settings settings, CancellationToken ct = default);
}

public interface IAuditTrailRepository
{
    Task AddAsync(AuditTrailEntry entry, CancellationToken ct = default);
    Task<IReadOnlyList<AuditTrailEntry>> GetByEntityAsync(string entityType, Guid entityId, CancellationToken ct = default);
    Task<int> PurgeOlderThanAsync(DateTime cutoff, CancellationToken ct = default);   // Startup-only; hard delete (log exemption)
}
```

## Unit of work / atomic financial operations

```csharp
public interface IUnitOfWork
{
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken ct = default);
}
```

Every financial workflow (fee creation, attendance batch, payment + FIFO allocation, reactivation forgiveness, import) runs inside `ExecuteInTransactionAsync`: primary records + GL pairs + balance verification, then commit; any failure → full rollback (spec §6 Pass #3 "GL Transaction Recording Timing"). GL imbalance throws `GLBalanceException` ("GL transaction pair imbalanced; operation cancelled.").

## DbContext shape

- `StageFrightDbContext` with `DbSet<>` per core entity; `IEntityTypeConfiguration<T>` classes in `Configurations/` (one per file) define: soft-delete query filters, unique indexes ((MemberId, Year), (RehearsalId, MemberId), (EventId, MemberId)), decimal `HasPrecision(18,2)` + TEXT-affinity conversion (research.md R10), required relationships with `DeleteBehavior.Restrict` (nothing cascades — preservation rules).
- Database file: `stagefright.db` in the platform app-data directory; directory auto-created at startup.
- Schema version: semver recorded in Settings.SchemaVersion and in every backup manifest (NFR-002).
- Plugin contexts: see [plugin-contracts.md](./plugin-contracts.md) `IDataAccessProvider`.

## Audit integration

Repositories raise audit entries through `IAuditTrailService` (Core) for every Create/Update/Archive/Restore, status change, Notes edit (old/new), forgiveness, committee reset, and import/export — user fixed to `"system"` (NFR-013).
