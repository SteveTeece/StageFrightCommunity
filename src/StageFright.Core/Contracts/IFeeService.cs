using StageFright.Core.Entities;

namespace StageFright.Core.Contracts;

/// <summary>
/// Application service for annual fee batch operations.
/// </summary>
public interface IFeeService
{
    /// <summary>
    /// Returns members who are eligible for an annual fee in the current calendar year:
    /// Active status with no existing Annual fee record (paid or unpaid) for the current year.
    /// </summary>
    Task<IReadOnlyList<Member>> GetEligibleMembersAsync(CancellationToken ct = default);

    /// <summary>
    /// Applies annual fees to the specified members inside a single atomic transaction.
    /// Creates a Fee(Annual) + GL pair (Debit MemberReceivable / Credit Income) per member.
    /// Returns the number of fees successfully created.
    /// </summary>
    Task<int> ApplyAnnualFeesAsync(IReadOnlyList<Guid> memberIds, CancellationToken ct = default);
}
