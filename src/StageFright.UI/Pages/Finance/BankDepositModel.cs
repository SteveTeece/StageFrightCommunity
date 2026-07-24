namespace StageFright.UI.Pages.Finance;

internal sealed class BankDepositModel
{
    public DateTime Date { get; set; } = DateTime.Today;
    public decimal Amount { get; set; }
    public Guid ToAccountId { get; set; }
    public string? Description { get; set; }
}
