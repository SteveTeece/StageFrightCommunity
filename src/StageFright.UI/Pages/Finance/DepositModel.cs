namespace StageFright.UI.Pages.Finance;

/// <summary>Form state for <see cref="DepositPage"/> (spec 033). The ticked items live in the item grid.</summary>
internal sealed class DepositModel
{
    public DateTime Date { get; set; } = DateTime.Today;
    public Guid BankAccountId { get; set; }
    // Nullable so a blank cash box is valid while the user types.
    public decimal? CashAmount { get; set; }
}
