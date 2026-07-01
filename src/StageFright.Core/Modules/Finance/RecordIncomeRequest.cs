namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Input model for recording a non-member income entry such as a raffle, donation, or fundraising event.
/// </summary>
public class RecordIncomeRequest
{
    /// <summary>UTC date the income was received. Required.</summary>
    public DateTime Date { get; set; }

    /// <summary>Amount received. Must be greater than zero.</summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Id of the Income category to credit. Must be a non-system Income category.
    /// </summary>
    public Guid CategoryId { get; set; }

    /// <summary>Optional description for the GL transaction.</summary>
    public string? Description { get; set; }
}
