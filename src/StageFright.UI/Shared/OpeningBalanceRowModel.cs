using StageFright.Core.Entities;

namespace StageFright.UI.Shared;

/// <summary>
/// One account's row in the shared <see cref="OpeningBalanceEntryForm"/> entry table.
/// Moved from <c>StageFright.UI.Pages.Finance</c> (was <c>internal</c> there) since the
/// shared component now owns the entry table both the standalone Opening Balances page
/// and the setup wizard's Opening Balances tab render.
/// </summary>
public sealed class OpeningBalanceRowModel
{
    public required Account Account { get; init; }
    public decimal Amount { get; set; }
}
