using StageFright.Core.Enums;

namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Input model for recording an expense payment out of a bank/cash account.
/// </summary>
public class RecordExpenseRequest
{
    /// <summary>
    /// GST treatment of this payment. Only honoured while the organisation is GST
    /// registered; defaults to GST-free when registered and null when not.
    /// The amount is always GST-inclusive.
    /// </summary>
    public GstCode? GstCode { get; set; }

    /// <summary>UTC date the payment was made. Required.</summary>
    public DateTime Date { get; set; }

    /// <summary>Amount paid. Must be greater than zero.</summary>
    public decimal Amount { get; set; }

    /// <summary>Id of the bank/cash account the money came out of. Must be a bank account.</summary>
    public Guid BankAccountId { get; set; }

    /// <summary>Id of the Expense account to debit. Must be a non-system Expense account.</summary>
    public Guid ExpenseAccountId { get; set; }

    /// <summary>Optional payee (who was paid).</summary>
    public string? Payee { get; set; }

    /// <summary>Optional description for the GL transaction.</summary>
    public string? Description { get; set; }
}
