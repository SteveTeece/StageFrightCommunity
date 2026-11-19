using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StageFright.Core.Entities;

namespace StageFright.Data.Configurations;

/// <summary>
/// EF configuration for <see cref="ExternalOrganisation"/> (spec 032). The filtered unique,
/// case-insensitive <c>Name</c> index is a backstop for the service's own duplicate-name check
/// (research §4); SQLite <c>NOCASE</c> folds ASCII only, so the service's ordinal-ignore-case
/// check stays the stricter rule.
/// </summary>
public class ExternalOrganisationConfiguration : IEntityTypeConfiguration<ExternalOrganisation>
{
    public void Configure(EntityTypeBuilder<ExternalOrganisation> builder)
    {
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Name).IsRequired().HasMaxLength(200).UseCollation("NOCASE");
        builder.Property(o => o.Address).HasMaxLength(1000);
        builder.Property(o => o.Email).HasMaxLength(254);
        builder.Property(o => o.CreatedAt).IsRequired();
        builder.Property(o => o.UpdatedAt).IsRequired();

        builder.HasIndex(o => o.Name)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasQueryFilter(o => !o.IsDeleted);
    }
}
