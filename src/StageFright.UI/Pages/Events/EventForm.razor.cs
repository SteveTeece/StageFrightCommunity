using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Events;
using StageFright.UI.Resources.Strings;
using DomainValidationException = StageFright.Core.Exceptions.ValidationException;

namespace StageFright.UI.Pages.Events;

public partial class EventForm
{
    [Inject] private IEventService EventService { get; set; } = null!;
    [Inject] private IEventTypeService EventTypeService { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private IStringLocalizer<EventsResource> L { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResource> Shared { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;

    private readonly EventFormModel _model = new();
    private bool _saving;
    private bool _loadingTypes = true;
    private string? _errorMessage;
    private List<EventType> _eventTypes = new();

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var types = await EventTypeService.GetSelectableForNewEventsAsync();
            _eventTypes = types.OrderBy(t => t.Name).ToList();
        }
        catch (Exception ex)
        {
            _errorMessage = Loc.Get<EventsResource>("Events_Form_LoadTypesError", ex.Message);
        }
        finally
        {
            _loadingTypes = false;
        }
    }

    private async Task HandleSubmit()
    {
        _saving = true;
        _errorMessage = null;

        try
        {
            await EventService.ScheduleAsync(new ScheduleEventRequest
            {
                Date = DateTime.SpecifyKind(_model.Date, DateTimeKind.Utc),
                EventTypeId = _model.EventTypeId,
                Notes = string.IsNullOrWhiteSpace(_model.Notes) ? null : _model.Notes.Trim()
            });

            Nav.NavigateTo("/events");
        }
        catch (DomainValidationException ex)
        {
            _errorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            _errorMessage = Loc.Get<EventsResource>("Events_Form_SaveError", ex.Message);
        }
        finally
        {
            _saving = false;
        }
    }

    private void Cancel() => Nav.NavigateTo("/events");

    private sealed class EventFormModel
    {
        // A non-nullable DateTime is never null, so [Required] here is presentational only —
        // the picker always supplies a value; a custom message would be dead code (spec 027).
        [Required]
        public DateTime Date { get; set; } = DateTime.UtcNow.Date;

        [Required]
        public Guid EventTypeId { get; set; }

        public string? Notes { get; set; }
    }
}
