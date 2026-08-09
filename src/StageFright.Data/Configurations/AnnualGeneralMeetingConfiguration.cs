using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StageFright.Core.Entities;

namespace StageFright.Data.Configurations;

public class AnnualGeneralMeetingConfiguration : IEntityTypeConfiguration<AnnualGeneralMeeting>
{
    public void Configure(EntityTypeBuilder<AnnualGeneralMeeting> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Date).IsRequired();
        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.UpdatedAt).IsRequired();

        builder.HasQueryFilter(a => !a.IsDeleted);

        builder.HasMany(a => a.AttendanceRecords)
            .WithOne(r => r.AnnualGeneralMeeting)
            .HasForeignKey(r => r.AnnualGeneralMeetingId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
