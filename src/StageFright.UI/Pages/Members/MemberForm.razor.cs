using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.Core.Contracts;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Members;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Pages.Members;

public partial class MemberForm : ComponentBase
{
    [Parameter] public Guid? Id { get; set; }

    [Inject] private IMemberService MemberService { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private IStringLocalizer<MembersResource> L { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResource> Shared { get; set; } = null!;

    private readonly MemberFormModel _form = new();
    private Dictionary<string, string> _errors = new();
    private string? _globalError;
    private bool _loading = true;
    private bool _saving;

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

            _form.FirstName = member.FirstName;
            _form.LastName = member.LastName;
            _form.StreetAddress = member.StreetAddress;
            _form.Phone = member.Phone;
            _form.Email = member.Email;
            _form.JoinDate = member.JoinDate;
            _form.DateOfBirth = member.DateOfBirth;
        }
        else
        {
            _form.JoinDate = DateTime.UtcNow.Date;
        }

        _loading = false;
    }

    private async Task SaveAsync()
    {
        _errors = new();
        _globalError = null;

        _saving = true;
        try
        {
            if (_isNew)
            {
                var request = new CreateMemberRequest
                {
                    FirstName = _form.FirstName,
                    LastName = _form.LastName,
                    StreetAddress = _form.StreetAddress,
                    Phone = _form.Phone,
                    Email = _form.Email,
                    JoinDate = _form.JoinDate,
                    DateOfBirth = _form.DateOfBirth
                };
                var member = await MemberService.CreateAsync(request);

                Nav.NavigateTo($"/members/{member.Id}");
            }
            else
            {
                var request = new UpdateMemberRequest
                {
                    FirstName = _form.FirstName,
                    LastName = _form.LastName,
                    StreetAddress = _form.StreetAddress,
                    Phone = _form.Phone,
                    Email = _form.Email,
                    JoinDate = _form.JoinDate,
                    DateOfBirth = _form.DateOfBirth
                };
                await MemberService.UpdateAsync(Id!.Value, request);

                Nav.NavigateTo($"/members/{Id}");
            }
        }
        catch (ValidationException ex)
        {
            _globalError = ex.Message;
        }
        catch (Exception)
        {
            _globalError = Shared["Shared_Error_Unexpected"];
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
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string StreetAddress { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public DateTime JoinDate { get; set; }
        public DateTime? DateOfBirth { get; set; }
    }
}
