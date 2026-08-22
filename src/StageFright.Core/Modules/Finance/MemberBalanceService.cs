using StageFright.Core.Contracts;
using StageFright.Core.Entities;

namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Provides GL-derived balance views by delegating to the GL repository and member/fee repositories.
/// </summary>
public class MemberBalanceService : IMemberBalanceService
{
    private readonly IMemberRepository _memberRepo;
    private readonly IFeeRepository _feeRepo;
    private readonly IGLRepository _glRepo;

    public MemberBalanceService(
        IMemberRepository memberRepo,
        IFeeRepository feeRepo,
        IGLRepository glRepo)
    {
        _memberRepo = memberRepo;
        _feeRepo = feeRepo;
        _glRepo = glRepo;
    }

    public Task<decimal> GetBalanceAsync(Guid memberId, CancellationToken ct = default)
        => _glRepo.GetMemberBalanceAsync(memberId, ct);

    public async Task<IReadOnlyList<OutstandingFee>> GetOutstandingFeesAsync(Guid memberId, CancellationToken ct = default)
    {
        var fees = await _feeRepo.GetUnpaidOrderedFifoAsync(memberId, ct);
        return await BuildOutstandingFeesAsync(fees, ct);
    }

    public async Task<IReadOnlyList<MemberBalance>> GetAllMemberBalancesAsync(CancellationToken ct = default)
    {
        var members = await _memberRepo.GetAllAsync(ct);

        var balances = new List<MemberBalance>();

        foreach (var member in members)
        {
            var balance = await _glRepo.GetMemberBalanceAsync(member.Id, ct);
            if (balance <= 0m)
                continue;

            var fees = await _feeRepo.GetUnpaidOrderedFifoAsync(member.Id, ct);
            var outstandingFeeIds = (await BuildOutstandingFeesAsync(fees, ct))
                .Select(o => o.FeeId)
                .ToHashSet();

            balances.Add(new MemberBalance
            {
                MemberId = member.Id,
                Name = member.SortableFullName,
                Balance = balance,
                Fees = fees.Where(f => outstandingFeeIds.Contains(f.Id)).ToList()
            });
        }

        return balances;
    }

    /// <summary>
    /// Fees carry no per-record paid flag, so which fees are still owed is derived per-fee
    /// from the GL: each fee's own MemberReceivable credits (from payments or forgiveness,
    /// however they were allocated — not assumed to be FIFO) are summed and subtracted from
    /// its original amount. This is the single source of truth for "is this specific fee
    /// outstanding," shared by GetOutstandingFeesAsync and GetAllMemberBalancesAsync so both
    /// agree on the same per-fee GL state instead of one of them guessing via a FIFO-balance
    /// prefix walk.
    /// </summary>
    private async Task<IReadOnlyList<OutstandingFee>> BuildOutstandingFeesAsync(IReadOnlyList<Fee> feesFifoOrder, CancellationToken ct)
    {
        var outstanding = new List<OutstandingFee>();
        foreach (var fee in feesFifoOrder.OrderBy(f => f.FeeDate).ThenBy(f => f.CreatedAt).ThenBy(f => f.Id))
        {
            var feeTransactions = await _glRepo.GetByFeeAsync(fee.Id, ct);
            var alreadySettled = feeTransactions
                .Where(t => t.AccountId == SystemAccounts.MemberReceivableId)
                .Sum(t => t.CreditAmount);
            var remainingAmount = fee.Amount - alreadySettled;

            if (remainingAmount <= 0m)
                continue;

            outstanding.Add(new OutstandingFee
            {
                FeeId = fee.Id,
                FeeType = fee.FeeType,
                FeeDate = fee.FeeDate,
                DueDate = fee.DueDate,
                RemainingAmount = remainingAmount
            });
        }

        return outstanding;
    }
}
