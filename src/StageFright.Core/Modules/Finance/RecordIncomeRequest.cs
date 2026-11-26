using StageFright.Core.Enums;

namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Input model for recording a non-member income entry such as a raffle, donation, or fundraising event.
/// </summary>
public class RecordIncomeRequest
{
    /// <summary>
    /// Tax treatment of this entry. Only honoured while tax applies to the organisation;
    /// defaults to tax-exempt when applicable and null when not.
    /// The amount is always tax-inclusive.
    /// </summary>
    public TaxCode? TaxCode { get; set; }

    /// <summary>UTC date the income was received. Required.</summary>
    public DateTime Date { get; set; }

    /// <summary>Amount received. Must be greater than zero.</summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Id of the Income account to credit. Must be a non-system Income account.
    /// </summary>
    public Guid AccountId { get; set; }

    /// <summary>
    /// How the income was received (spec 033, research §6). Cash goes to Petty Cash, cheque and other
    /// wait in Undeposited Funds as a received item, and card and electronic transfer go straight to
    /// <see cref="BankAccountId"/>. Defaults to cash.
    /// </summary>
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    /// <summary>
    /// The bank account a card or electronic-transfer receipt went into. Required for those methods
    /// (a bank account other than Petty Cash); ignored for every other method.
    /// </summary>
    public Guid? BankAccountId { get; set; }

    /// <summary>
    /// Optional cheque number or note (max 50) kept on the received item. Only used for cheque and other.
    /// </summary>
    public string? Reference { get; set; }

    /// <summary>Optional description for the GL transaction.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// Optional id of the active external organisation the income came from (spec 032 FR-010).
    /// Stamped on the journal entry only; it never changes the amounts, accounts or tax posted.
    /// </summary>
    public Guid? ExternalOrganisationId { get; set; }
}
