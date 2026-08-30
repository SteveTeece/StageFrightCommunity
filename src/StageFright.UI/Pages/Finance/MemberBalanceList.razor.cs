using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.Core.Contracts;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Finance;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Pages.Finance;

public partial class MemberBalanceList : ComponentBase
{
    [Inject] private IMemberBalanceService BalanceService { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private IStringLocalizer<FinanceResource> L { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResource> Shared { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;

    private List<MemberBalance> _balances = new();
    private HashSet<Guid> _expanded = new();
    private bool _loading = true;
    private string? _errorMessage;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var result = await BalanceService.GetAllMemberBalancesAsync();
            _balances = result.ToList();
        }
        catch (Exception ex)
        {
            _errorMessage = Loc.Get<FinanceResource>("Finance_Balances_LoadError", ex.Message);
        }
        finally
        {
            _loading = false;
        }
    }

    private void ToggleExpand(Guid memberId)
    {
        if (!_expanded.Add(memberId))
            _expanded.Remove(memberId);
    }

    private void GoToPayment(Guid memberId) =>
        Nav.NavigateTo($"/finance?tab=outstanding&memberId={memberId}");

    /// <summary>"due {date}" caption in a member's fee breakdown.</summary>
    private string FeeDueText(DateTime dueDate) =>
        Loc.Get<FinanceResource>("Finance_Balances_FeeDue", dueDate.ToString("d MMM yyyy"));

    /// <summary>"Show N fee(s)" toggle button — count-dependent wording.</summary>
    private string ShowFeesText(int count) =>
        Loc.Plural<FinanceResource>("Finance_Balances_ShowFeesButton", count);
}
