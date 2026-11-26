using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StageFright.Core.Entities;

namespace StageFright.Data.Configurations;

public class ExternalOrganisationContactConfiguration : IEntityTypeConfiguration<ExternalOrganisationContact>
{
    public void Configure(EntityTypeBuilder<ExternalOrganisationContact> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Role).HasMaxLength(100);
        builder.Property(c => c.Phone).HasMaxLength(50);
        builder.Property(c => c.Mobile).HasMaxLength(50);
        builder.Property(c => c.Email).HasMaxLength(254);
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt).IsRequired();

        builder.HasIndex(c => c.ExternalOrganisationId);

        builder.HasQueryFilter(c => !c.IsDeleted);

        // Nothing is ever hard-deleted, so no cascade is needed.
        builder.HasOne(c => c.ExternalOrganisation)
            .WithMany(o => o.Contacts)
            .HasForeignKey(c => c.ExternalOrganisationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
