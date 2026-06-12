using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StageFright.Core.Entities;

namespace StageFright.Data.Configurations;

public class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Date).IsRequired();
        builder.Property(e => e.StoredParticipationRate).HasPrecision(18, 2);
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();

        builder.HasQueryFilter(e => !e.IsDeleted);

        builder.HasOne(e => e.EventType)
            .WithMany(et => et.Events)
            .HasForeignKey(e => e.EventTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.ParticipationRecords)
            .WithOne(p => p.Event)
            .HasForeignKey(p => p.EventId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
