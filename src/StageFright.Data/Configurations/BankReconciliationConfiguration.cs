using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StageFright.Core.Entities;

namespace StageFright.Data.Configurations;

public class BankReconciliationConfiguration : IEntityTypeConfiguration<BankReconciliation>
{
    public void Configure(EntityTypeBuilder<BankReconciliation> builder)
    {
        builder.ToTable("BankReconciliations");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.StatementDate).IsRequired();
        builder.Property(r => r.StatementClosingBalance).HasPrecision(18, 2).IsRequired();
        builder.Property(r => r.OpeningBalance).HasPrecision(18, 2).IsRequired();
        builder.Property(r => r.Status).HasConversion<string>().IsRequired();
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.UpdatedAt).IsRequired();

        builder.HasOne(r => r.Account)
            .WithMany()
            .HasForeignKey(r => r.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.AccountId);

        builder.HasQueryFilter(r => !r.IsDeleted);
    }
}
