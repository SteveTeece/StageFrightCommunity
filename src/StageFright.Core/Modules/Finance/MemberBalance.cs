using StageFright.Core.Entities;

namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Aggregates a member's outstanding balance and their fee breakdown for display.
/// </summary>
public class MemberBalance
{
    /// <summary>Member primary key.</summary>
    public Guid MemberId { get; set; }

    /// <summary>Member display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Outstanding balance from GL: Σdebits(0101) − Σcredits(0101).</summary>
    public decimal Balance { get; set; }

    /// <summary>All fee records for the member (for expanded breakdown view).</summary>
    public IReadOnlyList<Fee> Fees { get; set; } = [];
}
