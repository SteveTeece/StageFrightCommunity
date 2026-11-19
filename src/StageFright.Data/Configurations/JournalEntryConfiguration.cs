using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StageFright.Core.Entities;

namespace StageFright.Data.Configurations;

public class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        builder.HasKey(j => j.Id);

        builder.Property(j => j.Type).HasConversion<string>().IsRequired();
        builder.Property(j => j.Date).IsRequired();
        builder.Property(j => j.CreatedAt).IsRequired();

        // No soft-delete fields; no global query filter — journal entries are immutable
        // financial records (Constitution §3.4). The Transactions relationship is
        // configured in TransactionConfiguration alongside the other FKs.

        builder.HasIndex(j => j.Type);

        // Optional, write-once link to an external organisation (spec 032). Restrict delete:
        // organisations are soft-deleted only, and a linked financial record must never lose it.
        builder.HasOne(j => j.ExternalOrganisation)
            .WithMany(o => o.JournalEntries)
            .HasForeignKey(j => j.ExternalOrganisationId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(j => j.ExternalOrganisationId);
    }
}
