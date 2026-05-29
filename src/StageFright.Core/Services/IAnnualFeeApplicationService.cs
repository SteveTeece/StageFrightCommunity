namespace StageFright.Core.Services;

/// <summary>
/// Service for annual fee application batch processing.
/// </summary>
public interface IAnnualFeeApplicationService
{
    /// <summary>
    /// Applies annual fees to all eligible active members in a single batch transaction.
    /// </summary>
    /// <remarks>
    /// Applies annual fees only to:
    /// - Members with Status = "Active" (as of today)
    /// - Members without an existing unpaid annual fee from the current renewal month period
    /// 
    /// All fee records are created as unpaid with CreatedAt = today.
    /// The operation is atomic—all fees are created together or none at all.
    /// </remarks>
    /// <returns>Number of fees applied</returns>
    Task<int> ApplyAnnualFeesAsync();

    /// <summary>
    /// Gets the number of eligible members for annual fee application.
    /// </summary>
    /// <remarks>
    /// Returns the count of active members that would receive annual fees.
    /// </remarks>
    /// <returns>Count of eligible members</returns>
    Task<int> GetEligibleMemberCountAsync();

    /// <summary>
    /// Gets the annual fee amount from settings.
    /// </summary>
    /// <returns>Annual fee amount</returns>
    Task<decimal> GetAnnualFeeAmountAsync();
}
