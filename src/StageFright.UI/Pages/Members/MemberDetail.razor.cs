using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Modules.Members;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Pages.Members;

public partial class MemberDetail : ComponentBase
{
    [Parameter] public Guid Id { get; set; }

    [Inject] private IMemberService MemberService { get; set; } = null!;
    [Inject] private ICommitteeService CommitteeService { get; set; } = null!;
    [Inject] private IFeeRepository FeeRepository { get; set; } = null!;
    [Inject] private IGLRepository GLRepository { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private IStringLocalizer<MembersResource> L { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResource> Shared { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;
    [Inject] private AgeCalculationService AgeCalc { get; set; } = null!;

    private Member? _member;
    private List<CommitteePositionRecord> _committeeHistory = new();
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
            _age = AgeCalc.Calculate(_member.DateOfBirth, DateTime.UtcNow.Date);
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
            var fees = (await FeeRepository.GetByMemberAsync(Id))
                .OrderByDescending(f => f.FeeDate)
                .ThenByDescending(f => f.CreatedAt);
            _feeHistory = new();

            foreach (var fee in fees)
            {
                var transactions = await GLRepository.GetByFeeAsync(fee.Id);
                // Only credits to Member Receivable represent actual payments clearing the debt.
                // The accrual entry credits Income — that must not be counted as payment received.
                // Match by AccountId (not the GLAccount snapshot string, which is legacy on older rows).
                var totalCredits = transactions
                    .Where(t => t.AccountId == SystemAccounts.MemberReceivableId && t.CreditAmount > 0)
                    .Sum(t => t.CreditAmount);
                bool isPaid = totalCredits >= fee.Amount;

                _feeHistory.Add(new FeeHistoryItem
                {
                    Id = fee.Id,
                    FeeType = fee.FeeType.LocalizeEnum(),
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

    private static int GetEffectiveYear(CommitteePositionRecord record) =>
        record.CommitteeTermId is not null ? record.CommitteeTerm!.LabelYear : record.Year ?? 0;

    private string GetEffectiveLabel(CommitteePositionRecord record)
    {
        if (record.OfficeHolderTypeId is not null)
            return record.OfficeHolderType!.Name;

        var position = record.Position;
        return !string.IsNullOrWhiteSpace(position) ? position : L["Members_Detail_GeneralCommitteeMember"];
    }

    /// <summary>Browser tab title — the member's name (or a fallback) plus the app suffix.</summary>
    private string PageTitle() =>
        Loc.Get<MembersResource>("Members_Detail_PageTitle", _member?.FullName ?? L["Members_Detail_PageTitleFallback"].Value);

    private bool IsCurrent(CommitteePositionRecord record) =>
        record.CommitteeTermId is not null
            ? record.CommitteeTerm!.EndDate is null
            : record.Year == _currentYear;

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
