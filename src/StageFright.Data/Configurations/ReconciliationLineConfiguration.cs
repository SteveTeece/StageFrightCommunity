using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StageFright.Core.Entities;

namespace StageFright.Data.Configurations;

public class ReconciliationLineConfiguration : IEntityTypeConfiguration<ReconciliationLine>
{
    public void Configure(EntityTypeBuilder<ReconciliationLine> builder)
    {
        builder.ToTable("ReconciliationLines");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.CreatedAt).IsRequired();

        builder.HasOne(l => l.Reconciliation)
            .WithMany(r => r.Lines)
            .HasForeignKey(l => l.ReconciliationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.Transaction)
            .WithMany()
            .HasForeignKey(l => l.TransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        // A transaction may appear at most once within a reconciliation.
        builder.HasIndex(l => new { l.ReconciliationId, l.TransactionId }).IsUnique();

        // Lines follow their parent's soft-delete filter (required FK to a filtered entity).
        builder.HasQueryFilter(l => !l.Reconciliation!.IsDeleted);
    }
}
