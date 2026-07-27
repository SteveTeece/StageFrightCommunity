using StageFright.Core.Contracts;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;

namespace StageFright.Core.Modules.Rehearsals;

/// <summary>Assembles the printable attendance roll for a scheduled rehearsal. Read-only.</summary>
public class AttendanceRollService : IAttendanceRollService
{
    private readonly IRehearsalRepository _rehearsalRepo;
    private readonly IMemberService _memberService;
    private readonly IMemberBalanceService _memberBalanceService;
    private readonly IFeeRepository _feeRepo;

    public AttendanceRollService(
        IRehearsalRepository rehearsalRepo,
        IMemberService memberService,
        IMemberBalanceService memberBalanceService,
        IFeeRepository feeRepo)
    {
        _rehearsalRepo = rehearsalRepo;
        _memberService = memberService;
        _memberBalanceService = memberBalanceService;
        _feeRepo = feeRepo;
    }

    public async Task<AttendanceRollData> GenerateAsync(Guid rehearsalId, CancellationToken ct = default)
    {
        var rehearsal = await _rehearsalRepo.GetByIdAsync(rehearsalId, ct)
            ?? throw new EntityNotFoundException("Rehearsal", rehearsalId, nameof(GenerateAsync));

        var activeMembers = (await _memberService.GetByStatusAsync(MemberStatus.Active, ct))
            .OrderBy(m => m.LastName).ThenBy(m => m.FirstName)
            .ToList();

        var members = new List<AttendanceRollMember>();
        foreach (var member in activeMembers)
        {
            members.Add(new AttendanceRollMember
            {
                FirstName = member.FirstName,
                LastName = member.LastName,
                AnnualFeePaid = await IsCurrentYearAnnualFeePaidAsync(member.Id, ct)
            });
        }

        return new AttendanceRollData
        {
            RehearsalDate = rehearsal.Date,
            RehearsalTime = rehearsal.Time,
            Members = members
        };
    }

    private async Task<bool> IsCurrentYearAnnualFeePaidAsync(Guid memberId, CancellationToken ct)
    {
        var currentYear = DateTime.Today.Year;

        var hasCurrentYearAnnualFee = await _feeRepo.AnnualFeeExistsAsync(memberId, currentYear, ct);
        if (!hasCurrentYearAnnualFee)
            return false;

        var outstanding = await _memberBalanceService.GetOutstandingFeesAsync(memberId, ct);
        return !outstanding.Any(f => f.FeeType == FeeType.Annual && f.FeeDate.Year == currentYear);
    }
}
