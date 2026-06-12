using StageFright.Core.Entities;

namespace StageFright.Core.Contracts;

/// <summary>
/// Repository contract for Fee entities. Immutable — no Update or Delete methods.
/// No soft-delete operations (Fee has no soft-delete fields per Constitution §3.4).
/// </summary>
public interface IFeeRepository
{
    /// <summary>Returns the fee with the given primary key, or null.</summary>
    Task<Fee?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Persists a new fee record and returns the tracked instance.</summary>
    Task<Fee> AddAsync(Fee fee, CancellationToken ct = default);

    /// <summary>Returns all fee records for the specified member.</summary>
    Task<IReadOnlyList<Fee>> GetByMemberAsync(Guid memberId, CancellationToken ct = default);

    /// <summary>
    /// Returns the member's unpaid fees ordered FIFO (FeeDate ASC, CreatedAt ASC, Id ASC).
    /// "Unpaid" is derived from the GL: fees where the member's Σdebits - Σcredits &gt; 0.
    /// </summary>
    Task<IReadOnlyList<Fee>> GetUnpaidOrderedFifoAsync(Guid memberId, CancellationToken ct = default);

    /// <summary>Returns true if an annual fee already exists for the member in the given calendar year (paid or unpaid).</summary>
    Task<bool> AnnualFeeExistsAsync(Guid memberId, int year, CancellationToken ct = default);

    /// <summary>Returns true if an attendance fee already exists for the member at the given rehearsal (idempotency check).</summary>
    Task<bool> AttendanceFeeExistsAsync(Guid memberId, Guid rehearsalId, CancellationToken ct = default);
}
