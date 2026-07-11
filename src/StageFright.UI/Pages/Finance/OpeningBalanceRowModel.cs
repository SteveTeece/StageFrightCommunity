using StageFright.Core.Entities;

namespace StageFright.UI.Pages.Finance;

internal sealed class OpeningBalanceRowModel
{
    public required Account Account { get; init; }
    public decimal Amount { get; set; }
}
