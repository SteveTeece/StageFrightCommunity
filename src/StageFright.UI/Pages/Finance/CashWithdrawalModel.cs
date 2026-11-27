namespace StageFright.UI.Pages.Finance;

/// <summary>Form state for <see cref="CashWithdrawalPage"/> (spec 033, US4).</summary>
internal sealed class CashWithdrawalModel
{
    public DateTime Date { get; set; } = DateTime.Today;
    public Guid BankAccountId { get; set; }
    // Nullable so a blank amount box is valid while the user types.
    public decimal? Amount { get; set; }
    public string? Purpose { get; set; }
}
