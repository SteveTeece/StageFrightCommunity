using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;

namespace StageFright.Core.Modules.Members;

/// <summary>
/// Manages committee membership records: add/update positions and annual reset.
/// </summary>
public class CommitteeService : ICommitteeService
{
    private readonly ICommitteeMembershipRepository _repo;
    private readonly IAuditTrailService _audit;
    private readonly IUnitOfWork _unitOfWork;

    public CommitteeService(
        ICommitteeMembershipRepository repo,
        IAuditTrailService audit,
        IUnitOfWork unitOfWork)
    {
        _repo = repo;
        _audit = audit;
        _unitOfWork = unitOfWork;
    }

    public async Task<CommitteeMembership> AddOrUpdateAsync(
        Guid memberId, int year, string position, CancellationToken ct = default)
    {
        var existing = await FindExistingAsync(memberId, year, ct);

        CommitteeMembership result = null!;
        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            if (existing is null)
            {
                var record = new CommitteeMembership
                {
                    Id = Guid.NewGuid(),
                    MemberId = memberId,
                    Year = year,
                    Position = position.Trim(),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                result = await _repo.AddAsync(record, innerCt);
                await _audit.LogAsync("CommitteeMembership", result.Id, AuditAction.Create, ct: innerCt);
            }
            else
            {
                existing.Position = position.Trim();
                existing.UpdatedAt = DateTime.UtcNow;
                await _repo.UpdateAsync(existing, innerCt);
                await _audit.LogAsync("CommitteeMembership", existing.Id, AuditAction.Update, ct: innerCt);
                result = existing;
            }
        }, ct);

        return result;
    }

    public Task<IReadOnlyList<CommitteeMembership>> GetHistoryAsync(Guid memberId, CancellationToken ct = default) =>
        _repo.GetByMemberAsync(memberId, ct);

    public async Task SoftDeleteCurrentYearAsync(int year, CancellationToken ct = default)
    {
        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            await _repo.SoftDeleteCurrentYearAsync(year, "system", innerCt);
            await _audit.LogAsync("CommitteeMembership", Guid.Empty, AuditAction.CommitteeReset,
                newValue: year.ToString(), ct: innerCt);
        }, ct);
    }

    private async Task<CommitteeMembership?> FindExistingAsync(Guid memberId, int year, CancellationToken ct)
    {
        var all = await _repo.GetByMemberAsync(memberId, ct);
        return all.FirstOrDefault(r => r.Year == year);
    }
}
