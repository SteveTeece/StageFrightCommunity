using StageFright.Core.Enums;

namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Input model for recording a member payment.
/// </summary>
public class RecordPaymentRequest
{
    /// <summary>The member making the payment.</summary>
    public Guid MemberId { get; set; }

    /// <summary>UTC date the payment was received.</summary>
    public DateTime Date { get; set; }

    /// <summary>Amount paid. Must be greater than zero.</summary>
    public decimal Amount { get; set; }

    /// <summary>Tender method. Defaults to Cash.</summary>
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    /// <summary>Reporting classification of the payment.</summary>
    public PaymentType PaymentType { get; set; }

    /// <summary>Optional free-text notes.</summary>
    public string? Notes { get; set; }
}
