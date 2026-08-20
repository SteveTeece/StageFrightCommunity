using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StageFright.Core.Entities;

namespace StageFright.Data.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Date).IsRequired();
        builder.Property(t => t.DebitAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(t => t.CreditAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(t => t.GLAccount).IsRequired();
        builder.Property(t => t.TaxCode).HasConversion<string>();
        builder.Property(t => t.CreatedAt).IsRequired();

        // No soft-delete fields; no global query filter on Transaction (Constitution §3.4)

        builder.HasOne(t => t.Account)
            .WithMany(c => c.Transactions)
            .HasForeignKey(t => t.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Member)
            .WithMany()
            .HasForeignKey(t => t.MemberId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Payment)
            .WithMany()
            .HasForeignKey(t => t.PaymentId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Fee)
            .WithMany()
            .HasForeignKey(t => t.FeeId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.JournalEntry)
            .WithMany(j => j.Transactions)
            .HasForeignKey(t => t.JournalEntryId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
