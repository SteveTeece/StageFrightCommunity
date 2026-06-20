using Microsoft.AspNetCore.Components;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Modules.Members;

namespace StageFright.UI.Pages.Members;

public partial class MemberDetail : ComponentBase
{
    [Parameter] public Guid Id { get; set; }

    [Inject] private IMemberService MemberService { get; set; } = null!;
    [Inject] private ICommitteeService CommitteeService { get; set; } = null!;
    [Inject] private IFeeRepository FeeRepository { get; set; } = null!;
    [Inject] private IGLRepository GLRepository { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;

    private readonly AgeCalculationService _ageCalc = new();
    private Member? _member;
    private List<CommitteeMembership> _committeeHistory = new();
    private List<FeeHistoryItem> _feeHistory = new();
    private int? _age;
    private bool _loading = true;
    private readonly int _currentYear = DateTime.UtcNow.Year;

    protected override async Task OnParametersSetAsync()
    {
        _loading = true;
        _member = await MemberService.GetByIdAsync(Id);
        if (_member is not null)
        {
            _age = _ageCalc.Calculate(_member.DateOfBirth, DateTime.UtcNow.Date);
            var history = await CommitteeService.GetHistoryAsync(Id);
            _committeeHistory = history.ToList();

            await LoadFeeHistoryAsync();
        }
        _loading = false;
    }

    private async Task LoadFeeHistoryAsync()
    {
        try
        {
            var fees = await FeeRepository.GetByMemberAsync(Id);
            _feeHistory = new();

            foreach (var fee in fees)
            {
                var transactions = await GLRepository.GetByFeeAsync(fee.Id);
                var totalCredits = transactions.Where(t => t.CreditAmount > 0).Sum(t => t.CreditAmount);
                bool isPaid = totalCredits >= fee.Amount;

                _feeHistory.Add(new FeeHistoryItem
                {
                    Id = fee.Id,
                    FeeType = fee.FeeType.ToString(),
                    Amount = fee.Amount,
                    FeeDate = fee.FeeDate,
                    DueDate = fee.DueDate,
                    IsPaid = isPaid,
                    PaidAmount = totalCredits
                });
            }
        }
        catch
        {
            // If fee history fails to load, continue without it
            _feeHistory = new();
        }
    }

    private void Edit() => Nav.NavigateTo($"/members/edit/{Id}");

    private async Task InactivateAsync()
    {
        await MemberService.InactivateAsync(Id);
        await OnParametersSetAsync();
    }

    private async Task ActivateAsync()
    {
        await MemberService.ActivateAsync(Id);
        await OnParametersSetAsync();
    }

    private async Task ArchiveAsync()
    {
        await MemberService.ArchiveAsync(Id);
        Nav.NavigateTo("/members");
    }

    private sealed class FeeHistoryItem
    {
        public Guid Id { get; init; }
        public string FeeType { get; init; } = string.Empty;
        public decimal Amount { get; init; }
        public DateTime FeeDate { get; init; }
        public DateTime DueDate { get; init; }
        public bool IsPaid { get; init; }
        public decimal PaidAmount { get; init; }
    }
}
