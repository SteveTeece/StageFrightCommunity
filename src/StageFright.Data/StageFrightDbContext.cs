using Microsoft.EntityFrameworkCore;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Data.Configurations;

namespace StageFright.Data;

/// <summary>
/// Central EF Core DbContext for the StageFright Community application.
/// Applies global soft-delete query filters on all entities that carry IsDeleted.
/// Seeds system accounts (Cash, MemberReceivable, BadDebtExpense) on first model creation.
/// </summary>
public class StageFrightDbContext : DbContext
{
    public StageFrightDbContext(DbContextOptions<StageFrightDbContext> options) : base(options) { }

    public DbSet<Member> Members => Set<Member>();
    public DbSet<CommitteePositionRecord> CommitteePositionRecords => Set<CommitteePositionRecord>();
    public DbSet<AnnualGeneralMeeting> AnnualGeneralMeetings => Set<AnnualGeneralMeeting>();
    public DbSet<AgmAttendanceRecord> AgmAttendanceRecords => Set<AgmAttendanceRecord>();
    public DbSet<CommitteeOfficeHolderType> CommitteeOfficeHolderTypes => Set<CommitteeOfficeHolderType>();
    public DbSet<CommitteeTerm> CommitteeTerms => Set<CommitteeTerm>();
    public DbSet<Rehearsal> Rehearsals => Set<Rehearsal>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<EventType> EventTypes => Set<EventType>();
    public DbSet<ParticipationRecord> ParticipationRecords => Set<ParticipationRecord>();
    public DbSet<Fee> Fees => Set<Fee>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<BankReconciliation> BankReconciliations => Set<BankReconciliation>();
    public DbSet<ReconciliationLine> ReconciliationLines => Set<ReconciliationLine>();
    public DbSet<Settings> Settings => Set<Settings>();
    public DbSet<AuditTrailEntry> AuditTrailEntries => Set<AuditTrailEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new MemberConfiguration());
        modelBuilder.ApplyConfiguration(new CommitteePositionRecordConfiguration());
        modelBuilder.ApplyConfiguration(new AnnualGeneralMeetingConfiguration());
        modelBuilder.ApplyConfiguration(new AgmAttendanceRecordConfiguration());
        modelBuilder.ApplyConfiguration(new CommitteeOfficeHolderTypeConfiguration());
        modelBuilder.ApplyConfiguration(new CommitteeTermConfiguration());
        modelBuilder.ApplyConfiguration(new RehearsalConfiguration());
        modelBuilder.ApplyConfiguration(new AttendanceRecordConfiguration());
        modelBuilder.ApplyConfiguration(new EventConfiguration());
        modelBuilder.ApplyConfiguration(new EventTypeConfiguration());
        modelBuilder.ApplyConfiguration(new ParticipationRecordConfiguration());
        modelBuilder.ApplyConfiguration(new FeeConfiguration());
        modelBuilder.ApplyConfiguration(new PaymentConfiguration());
        modelBuilder.ApplyConfiguration(new TransactionConfiguration());
        modelBuilder.ApplyConfiguration(new JournalEntryConfiguration());
        modelBuilder.ApplyConfiguration(new AccountConfiguration());
        modelBuilder.ApplyConfiguration(new BankReconciliationConfiguration());
        modelBuilder.ApplyConfiguration(new ReconciliationLineConfiguration());
        modelBuilder.ApplyConfiguration(new SettingsConfiguration());
        modelBuilder.ApplyConfiguration(new AuditTrailEntryConfiguration());

        SeedSystemAccounts(modelBuilder);
    }

    private static void SeedSystemAccounts(ModelBuilder modelBuilder)
    {
        // Fixed seed timestamp — must never change or EF emits spurious UpdateData migrations.
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<Account>().HasData(
            new Account
            {
                Id = new Guid("00000000-0000-0000-0000-000000000001"),
                Name = "Cash on Hand",
                Type = AccountType.Asset,
                AccountNumber = "1100",
                SortOrder = 0,
                IsSystem = true,
                IsBankAccount = true,
                IsDeleted = false,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Account
            {
                Id = new Guid("00000000-0000-0000-0000-000000000002"),
                Name = "Member Receivable",
                Type = AccountType.Asset,
                AccountNumber = "1200",
                SortOrder = 1,
                IsSystem = true,
                IsDeleted = false,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Account
            {
                Id = new Guid("00000000-0000-0000-0000-000000000003"),
                Name = "Bad Debt Expense",
                Type = AccountType.Expense,
                AccountNumber = "6999",
                SortOrder = 999,
                IsSystem = true,
                IsDeleted = false,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Account
            {
                Id = new Guid("00000000-0000-0000-0000-000000000004"),
                Name = "Tax Collected",
                Type = AccountType.Liability,
                AccountNumber = "2310",
                SortOrder = 10,
                IsSystem = true,
                IsDeleted = false,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Account
            {
                Id = new Guid("00000000-0000-0000-0000-000000000005"),
                // Tax paid on purchases is recoverable from the tax authority, so it is an
                // asset (a receivable), not a liability (spec 028 Phase 16 / issue #355). The
                // number stays in the 2000s as a documented exception — renumbering would
                // desync the denormalized Transaction.GLAccount snapshot on historical rows.
                Name = "Tax Receivable",
                Type = AccountType.Asset,
                AccountNumber = "2320",
                SortOrder = 11,
                IsSystem = true,
                IsDeleted = false,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Account
            {
                Id = new Guid("00000000-0000-0000-0000-000000000006"),
                Name = "Opening Balance Equity",
                Type = AccountType.Equity,
                AccountNumber = "3100",
                SortOrder = 20,
                IsSystem = true,
                IsDeleted = false,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Account
            {
                Id = new Guid("00000000-0000-0000-0000-000000000007"),
                Name = "Accumulated Surplus",
                Type = AccountType.Equity,
                AccountNumber = "3200",
                SortOrder = 21,
                IsSystem = true,
                IsDeleted = false,
                CreatedAt = now,
                UpdatedAt = now
            }
        );
    }
}
