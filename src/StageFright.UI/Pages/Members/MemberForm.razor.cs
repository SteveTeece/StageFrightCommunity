using Microsoft.AspNetCore.Components;
using StageFright.Core.Contracts;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Members;

namespace StageFright.UI.Pages.Members;

public partial class MemberForm : ComponentBase
{
    [Parameter] public Guid? Id { get; set; }

    [Inject] private IMemberService MemberService { get; set; } = null!;
    [Inject] private ICommitteeService CommitteeService { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;

    private readonly MemberFormModel _form = new();
    private Dictionary<string, string> _errors = new();
    private string? _globalError;
    private bool _loading = true;
    private bool _saving;
    private bool _isCommitteeMember;

    private bool _isNew => Id is null;

    protected override async Task OnInitializedAsync()
    {
        if (!_isNew)
        {
            var member = await MemberService.GetByIdAsync(Id!.Value);
            if (member is null)
            {
                Nav.NavigateTo("/members");
                return;
            }

            _form.Name = member.Name;
            _form.StreetAddress = member.StreetAddress;
            _form.Phone = member.Phone;
            _form.Email = member.Email;
            _form.JoinDate = member.JoinDate;
            _form.DateOfBirth = member.DateOfBirth;

            var history = await CommitteeService.GetHistoryAsync(member.Id);
            var currentYear = DateTime.UtcNow.Year;
            var currentRecord = history.FirstOrDefault(h => h.Year == currentYear);
            if (currentRecord is not null)
            {
                _isCommitteeMember = true;
                _form.CommitteePosition = currentRecord.Position;
            }
        }
        else
        {
            _form.JoinDate = DateTime.UtcNow.Date;
        }

        _loading = false;
    }

    private void OnCommitteeCheckChanged(ChangeEventArgs e)
    {
        _isCommitteeMember = e.Value is true;
        if (!_isCommitteeMember)
            _form.CommitteePosition = null;
    }

    private async Task SaveAsync()
    {
        _errors = new();
        _globalError = null;

        if (_isCommitteeMember && string.IsNullOrWhiteSpace(_form.CommitteePosition))
        {
            _errors["Position"] = "Position is required for committee members.";
            return;
        }

        _saving = true;
        try
        {
            if (_isNew)
            {
                var request = new CreateMemberRequest
                {
                    Name = _form.Name,
                    StreetAddress = _form.StreetAddress,
                    Phone = _form.Phone,
                    Email = _form.Email,
                    JoinDate = _form.JoinDate,
                    DateOfBirth = _form.DateOfBirth
                };
                var member = await MemberService.CreateAsync(request);

                if (_isCommitteeMember && !string.IsNullOrWhiteSpace(_form.CommitteePosition))
                    await CommitteeService.AddOrUpdateAsync(member.Id, DateTime.UtcNow.Year, _form.CommitteePosition);

                Nav.NavigateTo($"/members/{member.Id}");
            }
            else
            {
                var request = new UpdateMemberRequest
                {
                    Name = _form.Name,
                    StreetAddress = _form.StreetAddress,
                    Phone = _form.Phone,
                    Email = _form.Email,
                    JoinDate = _form.JoinDate,
                    DateOfBirth = _form.DateOfBirth
                };
                await MemberService.UpdateAsync(Id!.Value, request);

                if (_isCommitteeMember && !string.IsNullOrWhiteSpace(_form.CommitteePosition))
                    await CommitteeService.AddOrUpdateAsync(Id!.Value, DateTime.UtcNow.Year, _form.CommitteePosition);

                Nav.NavigateTo($"/members/{Id}");
            }
        }
        catch (ValidationException ex)
        {
            _globalError = ex.Message;
        }
        catch (Exception)
        {
            _globalError = "An unexpected error occurred. Please try again.";
        }
        finally
        {
            _saving = false;
        }
    }

    private void Cancel() =>
        Nav.NavigateTo(_isNew ? "/members" : $"/members/{Id}");

    private sealed class MemberFormModel
    {
        public string Name { get; set; } = string.Empty;
        public string StreetAddress { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public DateTime JoinDate { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? CommitteePosition { get; set; }
    }
}
