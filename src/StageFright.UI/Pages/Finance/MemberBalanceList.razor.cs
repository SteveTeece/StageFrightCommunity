using Microsoft.AspNetCore.Components;
using StageFright.Core.Contracts;
using StageFright.Core.Modules.Finance;

namespace StageFright.UI.Pages.Finance;

public partial class MemberBalanceList : ComponentBase
{
    [Inject] private IMemberBalanceService BalanceService { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;

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
            _errorMessage = $"Failed to load balances: {ex.Message}";
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
}
