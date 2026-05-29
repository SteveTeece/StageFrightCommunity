using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using StageFright.Core.Entities;

namespace StageFright.Data.Context;

/// <summary>
/// StageFright DbContext managing all entities and database operations.
/// Implements soft-delete pattern with automatic query filters.
/// </summary>
public class StageFrightContext : DbContext
{
	public DbSet<Member> Members { get; set; } = null!;
	public DbSet<Rehearsal> Rehearsals { get; set; } = null!;
	public DbSet<Event> Events { get; set; } = null!;
	public DbSet<Attendance> Attendances { get; set; } = null!;
	public DbSet<Participation> Participations { get; set; } = null!;
	public DbSet<Category> Categories { get; set; } = null!;
	public DbSet<Fee> Fees { get; set; } = null!;
	public DbSet<Payment> Payments { get; set; } = null!;
	public DbSet<Transaction> Transactions { get; set; } = null!;
	public DbSet<CommitteeMembership> CommitteeMemberships { get; set; } = null!;
	public DbSet<Settings> Settings { get; set; } = null!;
	public DbSet<AuditTrail> AuditTrails { get; set; } = null!;

	public StageFrightContext(DbContextOptions<StageFrightContext> options) : base(options)
	{
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

		// Configure soft-delete query filters
		modelBuilder.Entity<Member>().HasQueryFilter(m => !m.IsDeleted);
		modelBuilder.Entity<Rehearsal>().HasQueryFilter(r => !r.IsDeleted);
		modelBuilder.Entity<Event>().HasQueryFilter(e => !e.IsDeleted);
		modelBuilder.Entity<Category>().HasQueryFilter(c => !c.IsDeleted);
		modelBuilder.Entity<CommitteeMembership>().HasQueryFilter(cm => !cm.IsDeleted);

		// Configure Member entity
		modelBuilder.Entity<Member>(entity =>
		{
			entity.HasKey(m => m.Id);
			entity.Property(m => m.Name).IsRequired().HasMaxLength(256);
			entity.Property(m => m.StreetAddress).IsRequired().HasMaxLength(512);
			entity.Property(m => m.Phone).HasMaxLength(20);
			entity.Property(m => m.Email).HasMaxLength(256);
			entity.Property(m => m.Status).IsRequired().HasMaxLength(50); // Status stored as string: "Active" or "Inactive"
			entity.Property(m => m.IsDeleted).HasDefaultValue(false);
			entity.Property(m => m.JoinDate).IsRequired();

			// Indexes for common queries
			entity.HasIndex(m => m.Email).IsUnique();
			entity.HasIndex(m => m.Status);
			entity.HasIndex(m => m.IsDeleted);
		});

		// Configure Rehearsal entity
		modelBuilder.Entity<Rehearsal>(entity =>
		{
			entity.HasKey(r => r.Id);
			entity.Property(r => r.Date).IsRequired();
			entity.Property(r => r.Time).IsRequired();
			entity.Property(r => r.Notes).HasMaxLength(1024);
			entity.Property(r => r.IsDeleted).HasDefaultValue(false);

			// Indexes
			entity.HasIndex(r => r.Date);
			entity.HasIndex(r => r.IsDeleted);
		});

		// Configure Event entity
		modelBuilder.Entity<Event>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Date).IsRequired();
			entity.Property(e => e.EventType).IsRequired().HasMaxLength(100);
			entity.Property(e => e.Notes).HasMaxLength(1024);
			entity.Property(e => e.IsDeleted).HasDefaultValue(false);

			// Indexes
			entity.HasIndex(e => e.Date);
			entity.HasIndex(e => e.IsDeleted);
		});

		// Configure Attendance entity (junction table)
		modelBuilder.Entity<Attendance>(entity =>
		{
			entity.HasKey(a => a.Id);
			entity.Property(a => a.RehearsalId).IsRequired();
			entity.Property(a => a.MemberId).IsRequired();
			entity.Property(a => a.RecordedAt).IsRequired();

			// Unique constraint: only one attendance record per member per rehearsal
			entity.HasIndex(a => new { a.RehearsalId, a.MemberId }).IsUnique();

			// Foreign keys
			entity.HasOne<Rehearsal>().WithMany().HasForeignKey(a => a.RehearsalId).OnDelete(DeleteBehavior.Cascade);
			entity.HasOne<Member>().WithMany().HasForeignKey(a => a.MemberId).OnDelete(DeleteBehavior.Cascade);

			// Indexes
			entity.HasIndex(a => a.MemberId);
			entity.HasIndex(a => a.RehearsalId);
		});

		// Configure Participation entity (junction table)
		modelBuilder.Entity<Participation>(entity =>
		{
			entity.HasKey(p => p.Id);
			entity.Property(p => p.EventId).IsRequired();
			entity.Property(p => p.MemberId).IsRequired();
			entity.Property(p => p.RecordedAt).IsRequired();

			// Unique constraint: only one participation record per member per event
			entity.HasIndex(p => new { p.EventId, p.MemberId }).IsUnique();

			// Foreign keys
			entity.HasOne<Event>().WithMany().HasForeignKey(p => p.EventId).OnDelete(DeleteBehavior.Cascade);
			entity.HasOne<Member>().WithMany().HasForeignKey(p => p.MemberId).OnDelete(DeleteBehavior.Cascade);

			// Indexes
			entity.HasIndex(p => p.MemberId);
			entity.HasIndex(p => p.EventId);
		});

		// Configure Category entity
		modelBuilder.Entity<Category>(entity =>
		{
			entity.HasKey(c => c.Id);
			entity.Property(c => c.Name).IsRequired().HasMaxLength(256);
			entity.Property(c => c.Type).IsRequired().HasMaxLength(50); // Stored as string: "Income" or "Expense"
			entity.Property(c => c.SortOrder).IsRequired();
			entity.Property(c => c.IsArchived).HasDefaultValue(false);
			entity.Property(c => c.GlAccount).HasMaxLength(10);
			entity.Property(c => c.IsDeleted).HasDefaultValue(false);

			// Indexes
			entity.HasIndex(c => c.Type);
			entity.HasIndex(c => c.IsArchived);
			entity.HasIndex(c => c.IsDeleted);
		});

		// Configure Fee entity (immutable after creation, NO soft-delete)
		modelBuilder.Entity<Fee>(entity =>
		{
			entity.HasKey(f => f.Id);
			entity.Property(f => f.MemberId).IsRequired();
			entity.Property(f => f.FeeType).IsRequired().HasMaxLength(50); // Stored as string
			entity.Property(f => f.Amount).IsRequired().HasColumnType("decimal(10,2)");
			entity.Property(f => f.FeeDate).IsRequired();
			entity.Property(f => f.DueDate).IsRequired();
			entity.Property(f => f.CreatedAt).IsRequired();

			// Foreign key
			entity.HasOne<Member>().WithMany().HasForeignKey(f => f.MemberId).OnDelete(DeleteBehavior.Cascade);

			// Indexes
			entity.HasIndex(f => f.MemberId);
			entity.HasIndex(f => f.FeeType);
			entity.HasIndex(f => f.FeeDate);
		});

		// Configure Payment entity (Amount/Date/Category locked after creation)
		modelBuilder.Entity<Payment>(entity =>
		{
			entity.HasKey(p => p.Id);
			entity.Property(p => p.Date).IsRequired();
			entity.Property(p => p.Amount).IsRequired().HasColumnType("decimal(10,2)");
			entity.Property(p => p.PaymentMethod).IsRequired().HasMaxLength(50); // Stored as string
			entity.Property(p => p.PaymentType).IsRequired().HasMaxLength(50); // Stored as string
			entity.Property(p => p.MemberId).IsRequired();
			entity.Property(p => p.Category).IsRequired().HasMaxLength(256);
			entity.Property(p => p.Notes).HasMaxLength(1024);
			entity.Property(p => p.CreatedAt).IsRequired();
			entity.Property(p => p.UpdatedAt).IsRequired();

			// Foreign key
			entity.HasOne<Member>().WithMany().HasForeignKey(p => p.MemberId).OnDelete(DeleteBehavior.Cascade);

			// Indexes
			entity.HasIndex(p => p.MemberId);
			entity.HasIndex(p => p.Date);
		});

		// Configure Transaction entity (GL paired, immutable)
		modelBuilder.Entity<Transaction>(entity =>
		{
			entity.HasKey(t => t.Id);
			entity.Property(t => t.Date).IsRequired();
			entity.Property(t => t.Category).IsRequired().HasMaxLength(256);
			entity.Property(t => t.DebitAmount).HasColumnType("decimal(10,2)");
			entity.Property(t => t.CreditAmount).HasColumnType("decimal(10,2)");
			entity.Property(t => t.MemberId);
			entity.Property(t => t.PaymentId);
			entity.Property(t => t.Description).HasMaxLength(512);
			entity.Property(t => t.CreatedAt).IsRequired();
			entity.Property(t => t.ModifiedAt).IsRequired();

			// Foreign keys
			entity.HasOne<Member>().WithMany().HasForeignKey(t => t.MemberId).OnDelete(DeleteBehavior.SetNull);
			entity.HasOne<Payment>().WithMany().HasForeignKey(t => t.PaymentId).OnDelete(DeleteBehavior.SetNull);

			// Indexes
			entity.HasIndex(t => t.Date);
			entity.HasIndex(t => t.Category);
			entity.HasIndex(t => t.MemberId);
		});

		// Configure CommitteeMembership entity
		modelBuilder.Entity<CommitteeMembership>(entity =>
		{
			entity.HasKey(cm => cm.Id);
			entity.Property(cm => cm.MemberId).IsRequired();
			entity.Property(cm => cm.Year).IsRequired();
			entity.Property(cm => cm.Position).IsRequired().HasMaxLength(100);
			entity.Property(cm => cm.IsDeleted).HasDefaultValue(false);
			entity.Property(cm => cm.CreatedAt).IsRequired();
			entity.Property(cm => cm.ModifiedAt).IsRequired();

			// Unique constraint: only one position per member per year
			entity.HasIndex(cm => new { cm.MemberId, cm.Year }).IsUnique();

			// Foreign key
			entity.HasOne<Member>().WithMany().HasForeignKey(cm => cm.MemberId).OnDelete(DeleteBehavior.Cascade);

			// Indexes
			entity.HasIndex(cm => cm.Year);
			entity.HasIndex(cm => cm.IsDeleted);
		});

		// Configure Settings entity (singleton)
		modelBuilder.Entity<Settings>(entity =>
		{
			entity.HasKey(s => s.Id);
			entity.Property(s => s.OrganizationName).IsRequired().HasMaxLength(256);
			entity.Property(s => s.AnnualFee).HasColumnType("decimal(10,2)");
			entity.Property(s => s.AttendanceFee).HasColumnType("decimal(10,2)");
			entity.Property(s => s.RenewalMonth).IsRequired();
			entity.Property(s => s.CommitteeRenewalMonth).IsRequired();
			entity.Property(s => s.LastCommitteeResetYear).IsRequired();
			entity.Property(s => s.MaxAgeRange).IsRequired();
			entity.Property(s => s.MinimumMemberAge).IsRequired();
			entity.Property(s => s.Theme).IsRequired().HasMaxLength(50); // Stored as string
			entity.Property(s => s.CreatedAt).IsRequired();
			entity.Property(s => s.ModifiedAt).IsRequired();
		});

		// Configure AuditTrail entity
		modelBuilder.Entity<AuditTrail>(entity =>
		{
			entity.HasKey(at => at.Id);
			entity.Property(at => at.EntityType).IsRequired().HasMaxLength(256);
			entity.Property(at => at.EntityId).IsRequired();
			entity.Property(at => at.Action).IsRequired().HasMaxLength(50); // Stored as string
			entity.Property(at => at.UserId).HasMaxLength(256);
			entity.Property(at => at.Timestamp).IsRequired();
			entity.Property(at => at.OldValue).HasMaxLength(4000);
			entity.Property(at => at.NewValue).HasMaxLength(4000);

			// Indexes
			entity.HasIndex(at => at.EntityType);
			entity.HasIndex(at => at.EntityId);
			entity.HasIndex(at => at.Timestamp);
		});
	}
}
