using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StageFright.Core.Entities;

namespace StageFright.Data.Configurations;

public class AgmAttendanceRecordConfiguration : IEntityTypeConfiguration<AgmAttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AgmAttendanceRecord> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.CreatedAt).IsRequired();

        // Unique constraint: one attendance record per member per AGM
        builder.HasIndex(a => new { a.AnnualGeneralMeetingId, a.MemberId })
            .IsUnique();

        builder.HasQueryFilter(a => !a.IsDeleted);

        builder.HasOne(a => a.Member)
            .WithMany()
            .HasForeignKey(a => a.MemberId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
