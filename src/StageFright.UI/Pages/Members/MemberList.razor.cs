using Microsoft.AspNetCore.Components;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Members;

namespace StageFright.UI.Pages.Members;

public partial class MemberList : ComponentBase
{
    [Inject] private IMemberService MemberService { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;

    private readonly AgeCalculationService _ageCalc = new();
    private MemberStatus _filter = MemberStatus.Active;
    private List<Member> _members = new();
    private string _searchTerm = string.Empty;
    private bool _loading = true;

    private IEnumerable<Member> DisplayMembers =>
        string.IsNullOrWhiteSpace(_searchTerm)
            ? _members
            : _members.Where(m =>
                (m.Name?.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (m.Phone?.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (m.Email?.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (m.StreetAddress?.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase) ?? false));

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task SetFilter(MemberStatus status)
    {
        _filter = status;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            var result = await MemberService.GetByStatusAsync(_filter);
            _members = result.ToList();
        }
        finally
        {
            _loading = false;
        }
    }

    private void AddMember() => Nav.NavigateTo("/members/new");
    private void ViewMember(Guid id) => Nav.NavigateTo($"/members/{id}");
    private void EditMember(Guid id) => Nav.NavigateTo($"/members/edit/{id}");
}
