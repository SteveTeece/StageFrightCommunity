using Microsoft.AspNetCore.Components;
using StageFright.Core.Contracts;
using StageFright.Core.Enums;

namespace StageFright.UI.Modules.Members;

/// <summary>
/// Dashboard tile body for the Members module (design 3a): active/inactive/total
/// member counts plus an outstanding-fees alert chip sourced from GL balances.
/// </summary>
public partial class MembersTile : ComponentBase
{
    [Inject] private IMemberService MemberService { get; set; } = null!;
    [Inject] private IMemberBalanceService MemberBalanceService { get; set; } = null!;

    private bool _loading = true;
    private bool _error;
    private int _activeCount;
    private int _inactiveCount;
    private int _outstandingCount;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var active = await MemberService.GetByStatusAsync(MemberStatus.Active);
            var inactive = await MemberService.GetByStatusAsync(MemberStatus.Inactive);
            var balances = await MemberBalanceService.GetAllMemberBalancesAsync();

            _activeCount = active.Count;
            _inactiveCount = inactive.Count;
            _outstandingCount = balances.Count;
        }
        catch
        {
            _error = true;
        }
        finally
        {
            _loading = false;
        }
    }
}
