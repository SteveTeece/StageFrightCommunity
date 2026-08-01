using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StageFright.Core.Entities;

namespace StageFright.Data.Configurations;

public class CommitteeTermConfiguration : IEntityTypeConfiguration<CommitteeTerm>
{
    public void Configure(EntityTypeBuilder<CommitteeTerm> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.StartDate).IsRequired();
        builder.Property(t => t.LabelYear).IsRequired();
        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.UpdatedAt).IsRequired();

        // No soft-delete fields — a term is archived only as a side effect of archiving
        // its starting AGM — so no HasQueryFilter here.

        builder.HasOne(t => t.StartedByAgm)
            .WithMany()
            .HasForeignKey(t => t.StartedByAgmId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
