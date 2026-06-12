using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StageFright.Core.Entities;

namespace StageFright.Data.Configurations;

public class AuditTrailEntryConfiguration : IEntityTypeConfiguration<AuditTrailEntry>
{
    public void Configure(EntityTypeBuilder<AuditTrailEntry> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.EntityType).IsRequired();
        builder.Property(a => a.Action).HasConversion<string>();
        builder.Property(a => a.UserId).IsRequired();
        builder.Property(a => a.Timestamp).IsRequired();

        // No soft-delete fields; governed by 12-month retention policy (hard delete at startup)
    }
}
