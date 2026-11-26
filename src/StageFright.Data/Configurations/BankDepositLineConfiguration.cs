using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StageFright.Core.Entities;

namespace StageFright.Data.Configurations;

public class BankDepositLineConfiguration : IEntityTypeConfiguration<BankDepositLine>
{
    public void Configure(EntityTypeBuilder<BankDepositLine> builder)
    {
        builder.ToTable("BankDepositLines");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.CreatedAt).IsRequired();

        // No soft-delete fields; no global query filter — a financial record (Constitution §3.4).
        // The BankDeposit relationship is configured in BankDepositConfiguration.

        builder.HasOne(l => l.ReceivedItem)
            .WithMany()
            .HasForeignKey(l => l.ReceivedItemId)
            .OnDelete(DeleteBehavior.Restrict);

        // Unique per deposit only: an item can be deposited again after its deposit is reversed.
        builder.HasIndex(l => new { l.BankDepositId, l.ReceivedItemId }).IsUnique();
    }
}
