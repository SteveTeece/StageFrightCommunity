using StageFright.Core.Entities;

namespace StageFright.Core.Contracts;

/// <summary>
/// Repository contract for Payment entities. Immutable except for Notes.
/// UpdateNotesAsync is the only mutator; all other field updates throw ValidationException.
/// </summary>
public interface IPaymentRepository
{
    /// <summary>Returns the payment with the given primary key, or null.</summary>
    Task<Payment?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Persists a new payment record and returns the tracked instance.</summary>
    Task<Payment> AddAsync(Payment payment, CancellationToken ct = default);

    /// <summary>
    /// Updates only the Notes field and bumps UpdatedAt. Audits old and new values.
    /// Throws ValidationException if any other field is attempted.
    /// </summary>
    Task UpdateNotesAsync(Guid id, string? notes, CancellationToken ct = default);

    /// <summary>Returns all payment records for the specified member.</summary>
    Task<IReadOnlyList<Payment>> GetByMemberAsync(Guid memberId, CancellationToken ct = default);
}
