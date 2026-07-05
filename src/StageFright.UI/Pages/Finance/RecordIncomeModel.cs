namespace StageFright.UI.Pages.Finance;

internal sealed class RecordIncomeModel
{
    public DateTime Date { get; set; } = DateTime.Today;
    public decimal Amount { get; set; }
    public Guid AccountId { get; set; }
    public string? Description { get; set; }
}
