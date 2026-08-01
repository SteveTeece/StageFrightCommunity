using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StageFright.Core.Entities;

namespace StageFright.Data.Configurations;

public class CommitteeOfficeHolderTypeConfiguration : IEntityTypeConfiguration<CommitteeOfficeHolderType>
{
    public void Configure(EntityTypeBuilder<CommitteeOfficeHolderType> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name).IsRequired().HasMaxLength(100).UseCollation("NOCASE");
        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.UpdatedAt).IsRequired();

        builder.HasIndex(t => t.Name)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}
