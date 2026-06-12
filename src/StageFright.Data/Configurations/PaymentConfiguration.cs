using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StageFright.Core.Entities;

namespace StageFright.Data.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(p => p.Date).IsRequired();
        builder.Property(p => p.PaymentMethod).HasConversion<string>();
        builder.Property(p => p.PaymentType).HasConversion<string>();
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.UpdatedAt).IsRequired();

        // No soft-delete fields; no global query filter on Payment (Constitution §3.4)

        builder.HasOne(p => p.Member)
            .WithMany()
            .HasForeignKey(p => p.MemberId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
