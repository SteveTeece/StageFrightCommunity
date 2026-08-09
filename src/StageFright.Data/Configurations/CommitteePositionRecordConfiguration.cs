using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StageFright.Core.Entities;

namespace StageFright.Data.Configurations;

public class CommitteePositionRecordConfiguration : IEntityTypeConfiguration<CommitteePositionRecord>
{
    public void Configure(EntityTypeBuilder<CommitteePositionRecord> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Position).HasMaxLength(100);
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt).IsRequired();

        // One open holder per named position per term (built-in and custom titles alike).
        builder.HasIndex(c => new { c.CommitteeTermId, c.OfficeHolderTypeId })
            .IsUnique()
            .HasFilter("[EndDate] IS NULL AND [OfficeHolderTypeId] IS NOT NULL AND [IsDeleted] = 0");

        // A member can't hold more than one open position/seat in the same term (FR-008 backstop).
        builder.HasIndex(c => new { c.CommitteeTermId, c.MemberId })
            .IsUnique()
            .HasFilter("[EndDate] IS NULL AND [IsDeleted] = 0");

        builder.HasQueryFilter(c => !c.IsDeleted);

        builder.HasOne(c => c.CommitteeTerm)
            .WithMany(t => t.PositionRecords)
            .HasForeignKey(c => c.CommitteeTermId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.OfficeHolderType)
            .WithMany(t => t.PositionRecords)
            .HasForeignKey(c => c.OfficeHolderTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
