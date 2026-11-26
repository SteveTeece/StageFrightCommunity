using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StageFright.Core.Entities;

namespace StageFright.Data.Configurations;

public class BankDepositConfiguration : IEntityTypeConfiguration<BankDeposit>
{
    public void Configure(EntityTypeBuilder<BankDeposit> builder)
    {
        builder.ToTable("BankDeposits");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Date).IsRequired();
        builder.Property(d => d.CashAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(d => d.ChequeTotal).HasPrecision(18, 2).IsRequired();
        builder.Property(d => d.CreatedAt).IsRequired();
        builder.Ignore(d => d.Total);

        // No soft-delete fields; no global query filter — a financial record (Constitution §3.4).

        builder.HasOne(d => d.BankAccount)
            .WithMany()
            .HasForeignKey(d => d.BankAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.JournalEntry)
            .WithMany()
            .HasForeignKey(d => d.JournalEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.ReversalJournalEntry)
            .WithMany()
            .HasForeignKey(d => d.ReversalJournalEntryId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(d => d.Lines)
            .WithOne(l => l.BankDeposit)
            .HasForeignKey(l => l.BankDepositId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => d.JournalEntryId).IsUnique();
        builder.HasIndex(d => d.ReversalJournalEntryId).IsUnique().HasFilter("[ReversalJournalEntryId] IS NOT NULL");
    }
}
