namespace StageFright.UI.Pages.Finance;

internal sealed class JournalLineModel
{
    public Guid AccountId { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
}
