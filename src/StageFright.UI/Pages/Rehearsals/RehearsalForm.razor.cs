using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.Core.Contracts;
using StageFright.Core.Modules.Rehearsals;
using StageFright.UI.Resources.Strings;
using DomainValidationException = StageFright.Core.Exceptions.ValidationException;

namespace StageFright.UI.Pages.Rehearsals;

public partial class RehearsalForm
{
    [Inject] private IRehearsalService RehearsalService { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private IStringLocalizer<RehearsalsResource> L { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResource> Shared { get; set; } = null!;

    private readonly RehearsalFormModel _model = new();
    private string _timeString = "19:00";
    private bool _timeError;
    private bool _saving;
    private string? _errorMessage;

    private void HandleTimeChange(ChangeEventArgs e)
    {
        _timeString = e.Value?.ToString() ?? "";
        _timeError = string.IsNullOrWhiteSpace(_timeString);
    }

    private async Task HandleSubmit()
    {
        _timeError = string.IsNullOrWhiteSpace(_timeString);
        if (_timeError) return;

        if (!TimeSpan.TryParse(_timeString, out var time))
        {
            _timeError = true;
            return;
        }

        _saving = true;
        _errorMessage = null;

        try
        {
            await RehearsalService.ScheduleAsync(new ScheduleRehearsalRequest
            {
                Date = DateTime.SpecifyKind(_model.Date, DateTimeKind.Utc),
                Time = time,
                Notes = string.IsNullOrWhiteSpace(_model.Notes) ? null : _model.Notes.Trim()
            });

            Nav.NavigateTo("/rehearsals");
        }
        catch (DomainValidationException ex)
        {
            _errorMessage = ex.Message;
        }
        catch (Exception)
        {
            _errorMessage = Shared["Shared_Error_Unexpected"];
        }
        finally
        {
            _saving = false;
        }
    }

    private void Cancel() => Nav.NavigateTo("/rehearsals");

    private sealed class RehearsalFormModel
    {
        [Required]
        public DateTime Date { get; set; } = DateTime.UtcNow.Date;

        public string? Notes { get; set; }
    }
}
