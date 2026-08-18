using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StageFright.Core.Entities;

namespace StageFright.Data.Configurations;

public class SettingsConfiguration : IEntityTypeConfiguration<Settings>
{
    public void Configure(EntityTypeBuilder<Settings> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.OrganizationName).IsRequired();
        builder.Property(s => s.AnnualFee).HasPrecision(18, 2).IsRequired();
        builder.Property(s => s.AttendanceFee).HasPrecision(18, 2).IsRequired();
        builder.Property(s => s.Theme).HasConversion<string>();
        builder.Property(s => s.IsTaxApplicable).HasDefaultValue(false);
        builder.Property(s => s.TaxRate).HasPrecision(5, 2);
        builder.Property(s => s.AuditRetentionYears).HasDefaultValue(1);
        builder.Property(s => s.AnnualFeeTaxCode).HasConversion<string>();
        builder.Property(s => s.AttendanceFeeTaxCode).HasConversion<string>();
        builder.Property(s => s.SchemaVersion).IsRequired();
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.UpdatedAt).IsRequired();

        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}
