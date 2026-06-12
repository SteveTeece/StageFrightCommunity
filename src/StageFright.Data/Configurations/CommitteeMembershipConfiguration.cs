using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StageFright.Core.Entities;

namespace StageFright.Data.Configurations;

public class CommitteeMembershipConfiguration : IEntityTypeConfiguration<CommitteeMembership>
{
    public void Configure(EntityTypeBuilder<CommitteeMembership> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Position).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Year).IsRequired();
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt).IsRequired();

        // Unique constraint: one committee assignment per member per year
        builder.HasIndex(c => new { c.MemberId, c.Year })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}
