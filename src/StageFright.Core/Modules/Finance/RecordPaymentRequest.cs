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

    /// <summary>
    /// The bank account the money went into. Required for <see cref="PaymentMethod.Card"/> and
    /// <see cref="PaymentMethod.ElectronicTransfer"/> (an existing bank account other than Petty Cash);
    /// ignored for every other method.
    /// </summary>
    public Guid? BankAccountId { get; set; }

    /// <summary>
    /// Optional cheque number or note, kept on the item awaiting deposit. Used for
    /// <see cref="PaymentMethod.Check"/> and <see cref="PaymentMethod.Other"/> only; trimmed, null when blank.
    /// </summary>
    public string? Reference { get; set; }

    /// <summary>Reporting classification of the payment.</summary>
    public PaymentType PaymentType { get; set; }

    /// <summary>Optional free-text notes.</summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Fee IDs to allocate this payment against, oldest-first. Null (default) preserves
    /// legacy FIFO-across-all-fees behavior. An explicitly empty list is rejected.
    /// </summary>
    public IReadOnlyList<Guid>? SelectedFeeIds { get; set; }
}
