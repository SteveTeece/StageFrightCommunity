using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StageFright.Core.Entities;

namespace StageFright.Data.Configurations;

public class EventTypeConfiguration : IEntityTypeConfiguration<EventType>
{
    public void Configure(EntityTypeBuilder<EventType> builder)
    {
        builder.HasKey(et => et.Id);

        builder.Property(et => et.Name).IsRequired();
        builder.Property(et => et.CreatedAt).IsRequired();
        builder.Property(et => et.UpdatedAt).IsRequired();

        builder.HasIndex(et => et.Name)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasQueryFilter(et => !et.IsDeleted);
    }
}
