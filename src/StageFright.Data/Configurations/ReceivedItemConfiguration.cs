using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StageFright.Core.Entities;

namespace StageFright.Data.Configurations;

public class ReceivedItemConfiguration : IEntityTypeConfiguration<ReceivedItem>
{
    public void Configure(EntityTypeBuilder<ReceivedItem> builder)
    {
        // Exactly one source: a member payment, or an income/opening-balance journal entry.
        builder.ToTable("ReceivedItems", t => t.HasCheckConstraint(
            "CK_ReceivedItems_ExactlyOneSource",
            "([PaymentId] IS NULL) <> ([JournalEntryId] IS NULL)"));

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Date).IsRequired();
        builder.Property(i => i.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(i => i.Method).HasConversion<string>().IsRequired();
        builder.Property(i => i.Reference).HasMaxLength(50);
        builder.Property(i => i.CreatedAt).IsRequired();

        // No soft-delete fields; no global query filter — a financial record (Constitution §3.4).

        builder.HasOne(i => i.Payment)
            .WithMany()
            .HasForeignKey(i => i.PaymentId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.JournalEntry)
            .WithMany()
            .HasForeignKey(i => i.JournalEntryId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // A payment or journal entry yields at most one received item.
        builder.HasIndex(i => i.PaymentId).IsUnique().HasFilter("[PaymentId] IS NOT NULL");
        builder.HasIndex(i => i.JournalEntryId).IsUnique().HasFilter("[JournalEntryId] IS NOT NULL");
    }
}
