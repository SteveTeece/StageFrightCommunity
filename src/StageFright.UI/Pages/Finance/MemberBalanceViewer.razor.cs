using Microsoft.AspNetCore.Components;
using StageFright.Core.Services;
using StageFright.Core.Entities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StageFright.UI.Pages.Finance;

public partial class MemberBalanceViewer
{
    [Inject]
    private IMemberService MemberService { get; set; } = null!;

    [Inject]
    private IFinanceService FinanceService { get; set; } = null!;

    private string? ErrorMessage;
    private string? LoadingMessage;
    private List<MemberBalanceItem> MemberBalances = new();
    private decimal TotalBalance = 0;
    private int MembersWithBalances = 0;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            LoadingMessage = "Loading member balances...";
            MemberBalances.Clear();

            // Get all active members
            var members = (await MemberService.GetActiveMembersAsync()).ToList();

            foreach (var member in members)
            {
                var balance = await FinanceService.GetMemberBalanceAsync(member.Id);

                if (balance > 0)
                {
                    // Get breakdown of fees
                    var paymentHistory = await FinanceService.GetMemberPaymentHistoryAsync(member.Id);
                    
                    // For now, we'll estimate the breakdown
                    // In a real implementation, we'd query the fee repository directly
                    var item = new MemberBalanceItem
                    {
                        MemberId = member.Id,
                        MemberName = member.Name,
                        TotalBalance = balance,
                        AnnualFeesBalance = balance * 0.5m, // Placeholder
                        AttendanceFeesBalance = balance * 0.5m // Placeholder
                    };

                    MemberBalances.Add(item);
                    TotalBalance += balance;
                    MembersWithBalances++;
                }
            }

            // Sort by member name
            MemberBalances = MemberBalances.OrderBy(b => b.MemberName).ToList();

            LoadingMessage = null;
        }
        catch (Exception ex)
        {
            LoadingMessage = null;
            ErrorMessage = $"Error loading member balances: {ex.Message}";
        }
    }

    private class MemberBalanceItem
    {
        public Guid MemberId { get; set; }
        public string MemberName { get; set; } = string.Empty;
        public decimal TotalBalance { get; set; }
        public decimal AnnualFeesBalance { get; set; }
        public decimal AttendanceFeesBalance { get; set; }
    }
}
