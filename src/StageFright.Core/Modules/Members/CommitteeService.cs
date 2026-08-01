using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;

namespace StageFright.Core.Modules.Members;

/// <summary>
/// Manages committee position records: legacy year/position add-update and term-scoped queries.
/// </summary>
public class CommitteeService : ICommitteeService
{
    private readonly ICommitteePositionRecordRepository _repo;
    private readonly ICommitteeTermRepository _termRepo;
    private readonly IAuditTrailService _audit;
    private readonly IUnitOfWork _unitOfWork;

    public CommitteeService(
        ICommitteePositionRecordRepository repo,
        ICommitteeTermRepository termRepo,
        IAuditTrailService audit,
        IUnitOfWork unitOfWork)
    {
        _repo = repo;
        _termRepo = termRepo;
        _audit = audit;
        _unitOfWork = unitOfWork;
    }

    public async Task<CommitteePositionRecord> AddOrUpdateAsync(
        Guid memberId, int year, string position, CancellationToken ct = default)
    {
        var existing = await FindExistingAsync(memberId, year, ct);

        CommitteePositionRecord result = null!;
        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            if (existing is null)
            {
                var record = new CommitteePositionRecord
                {
                    Id = Guid.NewGuid(),
                    MemberId = memberId,
                    Year = year,
                    Position = position.Trim(),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                result = await _repo.AddAsync(record, innerCt);
                await _audit.LogAsync(nameof(CommitteePositionRecord), result.Id, AuditAction.Create, ct: innerCt);
            }
            else
            {
                existing.Position = position.Trim();
                existing.UpdatedAt = DateTime.UtcNow;
                await _repo.UpdateAsync(existing, innerCt);
                await _audit.LogAsync(nameof(CommitteePositionRecord), existing.Id, AuditAction.Update, ct: innerCt);
                result = existing;
            }
        }, ct);

        return result;
    }

    public Task<IReadOnlyList<CommitteePositionRecord>> GetHistoryAsync(Guid memberId, CancellationToken ct = default) =>
        _repo.GetByMemberAsync(memberId, ct);

    public async Task<IReadOnlyList<CommitteePositionRecord>> GetCurrentAsync(CancellationToken ct = default)
    {
        var openTerm = await _termRepo.GetOpenAsync(ct);
        return openTerm is null
            ? Array.Empty<CommitteePositionRecord>()
            : await _repo.GetByTermAsync(openTerm.Id, ct);
    }

    public Task<IReadOnlyList<CommitteePositionRecord>> GetByTermAsync(Guid committeeTermId, CancellationToken ct = default) =>
        _repo.GetByTermAsync(committeeTermId, ct);

    public Task<IReadOnlyList<CommitteePositionRecord>> GetByAgmAsync(Guid annualGeneralMeetingId, CancellationToken ct = default) =>
        _repo.GetByAgmAsync(annualGeneralMeetingId, ct);

    private async Task<CommitteePositionRecord?> FindExistingAsync(Guid memberId, int year, CancellationToken ct)
    {
        var all = await _repo.GetByMemberAsync(memberId, ct);
        return all.FirstOrDefault(r => r.Year == year);
    }
}
