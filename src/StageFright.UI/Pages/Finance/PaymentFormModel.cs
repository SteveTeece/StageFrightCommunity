using StageFright.Core.Enums;

namespace StageFright.UI.Pages.Finance;

internal sealed class PaymentFormModel
{
    public DateTime Date { get; set; } = DateTime.Today;
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public PaymentType PaymentType { get; set; } = PaymentType.Annual;
    public string? Notes { get; set; }
}
