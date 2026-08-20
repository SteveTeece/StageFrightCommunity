using StageFright.Core.Enums;

namespace StageFright.UI.Pages.Finance;

internal sealed class ExpensePaymentModel
{
    public DateTime Date { get; set; } = DateTime.Today;
    public decimal Amount { get; set; }
    public Guid BankAccountId { get; set; }
    public Guid ExpenseAccountId { get; set; }
    public TaxCode? TaxCode { get; set; }
    public string? Payee { get; set; }
    public string? Description { get; set; }
}
