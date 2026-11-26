using StageFright.Core.Enums;

namespace StageFright.UI.Pages.Finance;

internal sealed class RecordIncomeModel
{
    public DateTime Date { get; set; } = DateTime.Today;
    public decimal Amount { get; set; }
    public Guid AccountId { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public Guid BankAccountId { get; set; }
    public string? Reference { get; set; }
    public TaxCode? TaxCode { get; set; }
    public string? Description { get; set; }
    public Guid? ExternalOrganisationId { get; set; }
}
