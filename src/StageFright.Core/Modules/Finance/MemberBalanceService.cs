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

            balances.Add(new MemberBalance
            {
                MemberId = member.Id,
                Name = member.Name,
                Balance = balance,
                Fees = SelectOutstandingFees(fees, balance)
            });
        }

        return balances;
    }

    /// <summary>
    /// Fees carry no per-record paid flag — the GL balance is authoritative. Given fees in
    /// FIFO order and the member's current outstanding balance, the fees already covered by
    /// payments are exactly the oldest prefix summing to (total fees − balance), since payments
    /// are allocated FIFO. This walks that prefix off and returns only the still-owed tail.
    /// </summary>
    private static IReadOnlyList<Fee> SelectOutstandingFees(IReadOnlyList<Fee> feesFifoOrder, decimal balance)
    {
        var paidAmount = feesFifoOrder.Sum(f => f.Amount) - balance;

        var outstanding = new List<Fee>();
        foreach (var fee in feesFifoOrder)
        {
            if (paidAmount >= fee.Amount)
                paidAmount -= fee.Amount;
            else
                outstanding.Add(fee);
        }

        return outstanding;
    }
}
