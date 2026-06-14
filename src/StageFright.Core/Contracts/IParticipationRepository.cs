using StageFright.Core.Entities;

namespace StageFright.Core.Contracts;

/// <summary>Repository contract for ParticipationRecord entities.</summary>
public interface IParticipationRepository : ISoftDeletableRepository<ParticipationRecord>
{
    /// <summary>Returns true if a participation record already exists for the given event and member (idempotency check).</summary>
    Task<bool> ExistsAsync(Guid eventId, Guid memberId, CancellationToken ct = default);

    /// <summary>Inserts a batch of participation records within the ambient unit-of-work transaction.</summary>
    Task AddBatchAsync(IReadOnlyList<ParticipationRecord> records, CancellationToken ct = default);
}
