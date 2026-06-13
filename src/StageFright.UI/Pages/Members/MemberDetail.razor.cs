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
    [Inject] private NavigationManager Nav { get; set; } = null!;

    private readonly AgeCalculationService _ageCalc = new();
    private Member? _member;
    private List<CommitteeMembership> _committeeHistory = new();
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
        }
        _loading = false;
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
}
