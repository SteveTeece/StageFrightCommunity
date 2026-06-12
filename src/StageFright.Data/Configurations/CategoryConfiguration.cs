using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StageFright.Core.Entities;

namespace StageFright.Data.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).IsRequired();
        builder.Property(c => c.Type).HasConversion<string>();
        builder.Property(c => c.GLAccount).IsRequired();
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt).IsRequired();

        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}
