using StageFright.Core.Contracts;

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

            var fees = await _feeRepo.GetByMemberAsync(member.Id, ct);

            balances.Add(new MemberBalance
            {
                MemberId = member.Id,
                Name = member.Name,
                Balance = balance,
                Fees = fees
            });
        }

        return balances;
    }
}
