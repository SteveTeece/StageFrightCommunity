using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Modules.Events;
using DomainValidationException = StageFright.Core.Exceptions.ValidationException;

namespace StageFright.UI.Pages.Events;

public partial class EventForm
{
    [Inject] private IEventService EventService { get; set; } = null!;
    [Inject] private IEventTypeService EventTypeService { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;

    private readonly EventFormModel _model = new();
    private bool _saving;
    private bool _loadingTypes = true;
    private string? _errorMessage;
    private List<EventType> _eventTypes = new();

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var types = await EventTypeService.GetAllAsync();
            _eventTypes = types.OrderBy(t => t.Name).ToList();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to load event types: {ex.Message}";
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
            _errorMessage = $"An unexpected error occurred: {ex.Message}";
        }
        finally
        {
            _saving = false;
        }
    }

    private void Cancel() => Nav.NavigateTo("/events");

    private sealed class EventFormModel
    {
        [Required(ErrorMessage = "Date is required.")]
        public DateTime Date { get; set; } = DateTime.UtcNow.Date;

        [Required(ErrorMessage = "Event type is required.")]
        public Guid EventTypeId { get; set; }

        public string? Notes { get; set; }
    }
}
