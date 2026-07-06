namespace StageFright.UI.Pages.Finance;

internal sealed class TransferModel
{
    public DateTime Date { get; set; } = DateTime.Today;
    public decimal Amount { get; set; }
    public Guid FromAccountId { get; set; }
    public Guid ToAccountId { get; set; }
    public string? Description { get; set; }
}
