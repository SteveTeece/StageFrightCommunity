using Microsoft.AspNetCore.Components;
using StageFright.Core.Contracts;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Agm;

namespace StageFright.UI.Pages.Events;

public partial class ScheduleAgm : ComponentBase
{
    [Inject] private IAgmService AgmService { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;

    private DateTime _date = DateTime.Today;
    private string? _notes;
    private bool _saving;
    private string? _errorMessage;

    private async Task SaveAsync()
    {
        _errorMessage = null;
        _saving = true;
        try
        {
            var agm = await AgmService.ScheduleAsync(new ScheduleAgmRequest(_date, _notes));
            Nav.NavigateTo($"/events/agm/{agm.Id}");
        }
        catch (ValidationException ex)
        {
            _errorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to schedule AGM: {ex.Message}";
        }
        finally
        {
            _saving = false;
        }
    }
}
