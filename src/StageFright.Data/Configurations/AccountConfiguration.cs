using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StageFright.Core.Entities;

namespace StageFright.Data.Configurations;

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("Accounts");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Name).IsRequired();
        // Persisted as a string so the legacy "Income"/"Expense" values survive the enum rename.
        builder.Property(a => a.Type).HasConversion<string>();
        builder.Property(a => a.AccountNumber).IsRequired();
        // Uniqueness doubles as the migration's defensive assertion against renumber collisions.
        builder.HasIndex(a => a.AccountNumber).IsUnique();
        builder.Property(a => a.IsBankAccount).HasDefaultValue(false);
        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.UpdatedAt).IsRequired();

        builder.HasQueryFilter(a => !a.IsDeleted);
    }
}
