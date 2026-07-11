using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StageFright.Core.Entities;

namespace StageFright.Data.Configurations;

public class FeeConfiguration : IEntityTypeConfiguration<Fee>
{
    public void Configure(EntityTypeBuilder<Fee> builder)
    {
        builder.HasKey(f => f.Id);

        builder.Property(f => f.FeeType).HasConversion<string>();
        builder.Property(f => f.GstCode).HasConversion<string>();
        builder.Property(f => f.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(f => f.FeeDate).IsRequired();
        builder.Property(f => f.DueDate).IsRequired();
        builder.Property(f => f.CreatedAt).IsRequired();

        // No soft-delete fields; no global query filter on Fee (Constitution §3.4)

        builder.HasOne(f => f.Member)
            .WithMany()
            .HasForeignKey(f => f.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(f => f.Rehearsal)
            .WithMany()
            .HasForeignKey(f => f.RehearsalId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
