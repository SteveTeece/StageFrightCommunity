using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StageFright.Core.Entities;

namespace StageFright.Data.Configurations;

public class RehearsalConfiguration : IEntityTypeConfiguration<Rehearsal>
{
    public void Configure(EntityTypeBuilder<Rehearsal> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Date).IsRequired();
        builder.Property(r => r.Time).IsRequired();
        builder.Property(r => r.StoredAttendanceRate).HasPrecision(18, 2);
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.UpdatedAt).IsRequired();

        builder.HasQueryFilter(r => !r.IsDeleted);

        builder.HasMany(r => r.AttendanceRecords)
            .WithOne(a => a.Rehearsal)
            .HasForeignKey(a => a.RehearsalId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
