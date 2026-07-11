using StageFright.Core.Enums;

namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Input model for recording a non-member income entry such as a raffle, donation, or fundraising event.
/// </summary>
public class RecordIncomeRequest
{
    /// <summary>
    /// GST treatment of this entry. Only honoured while the organisation is GST
    /// registered; defaults to GST-free when registered and null when not.
    /// The amount is always GST-inclusive.
    /// </summary>
    public GstCode? GstCode { get; set; }

    /// <summary>UTC date the income was received. Required.</summary>
    public DateTime Date { get; set; }

    /// <summary>Amount received. Must be greater than zero.</summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Id of the Income account to credit. Must be a non-system Income account.
    /// </summary>
    public Guid AccountId { get; set; }

    /// <summary>
    /// Id of the bank/cash account the income was deposited to.
    /// Defaults to Cash on Hand (1100) when null.
    /// </summary>
    public Guid? DepositAccountId { get; set; }

    /// <summary>Optional description for the GL transaction.</summary>
    public string? Description { get; set; }
}
