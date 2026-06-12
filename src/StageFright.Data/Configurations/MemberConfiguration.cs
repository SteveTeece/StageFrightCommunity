using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StageFright.Core.Entities;

namespace StageFright.Data.Configurations;

public class MemberConfiguration : IEntityTypeConfiguration<Member>
{
    public void Configure(EntityTypeBuilder<Member> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Name).IsRequired().HasMaxLength(255);
        builder.Property(m => m.StreetAddress).IsRequired();
        builder.Property(m => m.Status).HasConversion<string>();
        builder.Property(m => m.CreatedAt).IsRequired();
        builder.Property(m => m.UpdatedAt).IsRequired();

        builder.HasQueryFilter(m => !m.IsDeleted);

        builder.HasMany(m => m.CommitteeMemberships)
            .WithOne(c => c.Member)
            .HasForeignKey(c => c.MemberId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
