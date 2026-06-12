using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StageFright.Core.Entities;

namespace StageFright.Data.Configurations;

public class ParticipationRecordConfiguration : IEntityTypeConfiguration<ParticipationRecord>
{
    public void Configure(EntityTypeBuilder<ParticipationRecord> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.CreatedAt).IsRequired();

        // Unique constraint: one participation record per member per event
        builder.HasIndex(p => new { p.EventId, p.MemberId })
            .IsUnique();

        builder.HasQueryFilter(p => !p.IsDeleted);

        builder.HasOne(p => p.Member)
            .WithMany()
            .HasForeignKey(p => p.MemberId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
