using Microsoft.EntityFrameworkCore;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Data.Configurations;

namespace StageFright.Data;

/// <summary>
/// Central EF Core DbContext for the StageFright Community application.
/// Applies global soft-delete query filters on all entities that carry IsDeleted.
/// Seeds system categories (Cash, MemberReceivable, BadDebtExpense) on first model creation.
/// </summary>
public class StageFrightDbContext : DbContext
{
    public StageFrightDbContext(DbContextOptions<StageFrightDbContext> options) : base(options) { }

    public DbSet<Member> Members => Set<Member>();
    public DbSet<CommitteeMembership> CommitteeMemberships => Set<CommitteeMembership>();
    public DbSet<Rehearsal> Rehearsals => Set<Rehearsal>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<EventType> EventTypes => Set<EventType>();
    public DbSet<ParticipationRecord> ParticipationRecords => Set<ParticipationRecord>();
    public DbSet<Fee> Fees => Set<Fee>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Settings> Settings => Set<Settings>();
    public DbSet<AuditTrailEntry> AuditTrailEntries => Set<AuditTrailEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new MemberConfiguration());
        modelBuilder.ApplyConfiguration(new CommitteeMembershipConfiguration());
        modelBuilder.ApplyConfiguration(new RehearsalConfiguration());
        modelBuilder.ApplyConfiguration(new AttendanceRecordConfiguration());
        modelBuilder.ApplyConfiguration(new EventConfiguration());
        modelBuilder.ApplyConfiguration(new EventTypeConfiguration());
        modelBuilder.ApplyConfiguration(new ParticipationRecordConfiguration());
        modelBuilder.ApplyConfiguration(new FeeConfiguration());
        modelBuilder.ApplyConfiguration(new PaymentConfiguration());
        modelBuilder.ApplyConfiguration(new TransactionConfiguration());
        modelBuilder.ApplyConfiguration(new CategoryConfiguration());
        modelBuilder.ApplyConfiguration(new SettingsConfiguration());
        modelBuilder.ApplyConfiguration(new AuditTrailEntryConfiguration());

        SeedSystemCategories(modelBuilder);
    }

    private static void SeedSystemCategories(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<Category>().HasData(
            new Category
            {
                Id = new Guid("00000000-0000-0000-0000-000000000001"),
                Name = "Cash",
                Type = CategoryType.Income,
                GLAccount = "0100",
                SortOrder = 0,
                IsSystem = true,
                IsDeleted = false,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Category
            {
                Id = new Guid("00000000-0000-0000-0000-000000000002"),
                Name = "Member Receivable",
                Type = CategoryType.Income,
                GLAccount = "0101",
                SortOrder = 1,
                IsSystem = true,
                IsDeleted = false,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Category
            {
                Id = new Guid("00000000-0000-0000-0000-000000000003"),
                Name = "Bad Debt Expense",
                Type = CategoryType.Expense,
                GLAccount = "9900",
                SortOrder = 999,
                IsSystem = true,
                IsDeleted = false,
                CreatedAt = now,
                UpdatedAt = now
            }
        );
    }
}
