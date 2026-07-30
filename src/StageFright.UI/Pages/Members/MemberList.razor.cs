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
    private bool _showInactive;
    private List<Member> _members = new();
    private string _searchTerm = string.Empty;
    private bool _loading = true;

    private IEnumerable<Member> DisplayMembers =>
        string.IsNullOrWhiteSpace(_searchTerm)
            ? _members
            : _members.Where(m =>
                m.FirstName.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase) ||
                m.LastName.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase) ||
                m.FullName.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase) ||
                (m.Phone?.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (m.Email?.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (m.StreetAddress?.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase) ?? false));

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            var active = await MemberService.GetByStatusAsync(MemberStatus.Active);
            if (_showInactive)
            {
                var inactive = await MemberService.GetByStatusAsync(MemberStatus.Inactive);
                _members = active.Concat(inactive).ToList();
            }
            else
            {
                _members = active.ToList();
            }
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
