using Microsoft.AspNetCore.Components;
using StageFright.Core.Contracts;
using StageFright.Core.Modules.Finance;

namespace StageFright.UI.Shared;

public partial class ReactivationForgivenessDialog : ComponentBase
{
    [Parameter] public Guid MemberId { get; set; }
    [Parameter] public bool IsVisible { get; set; }
    [Parameter] public EventCallback<bool> IsVisibleChanged { get; set; }
    [Parameter] public EventCallback OnForgivenessApplied { get; set; }

    [Inject] private IReactivationForgivenessService ForgivenessService { get; set; } = null!;

    private List<ForgivenessItem> _items = new();
    private HashSet<Guid> _selected = new();
    private bool _loading = true;
    private bool _applying;
    private string? _errorMessage;
    private string? _applyError;

    protected override async Task OnParametersSetAsync()
    {
        if (!IsVisible)
            return;

        if (_items.Count > 0)
            return;

        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        _errorMessage = null;
        try
        {
            var items = await ForgivenessService.GetForgivenessItemsAsync(MemberId);
            _items = items.ToList();

            _selected = _items
                .Where(i => i.IsDefaultForgiven)
                .Select(i => i.FeeId)
                .ToHashSet();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to load fees: {ex.Message}";
        }
        finally
        {
            _loading = false;
        }
    }

    private void OnCheckedChange(Guid feeId, bool isChecked)
    {
        if (isChecked)
            _selected.Add(feeId);
        else
            _selected.Remove(feeId);
    }

    private async Task ApplyAsync()
    {
        _applying = true;
        _applyError = null;
        try
        {
            await ForgivenessService.ApplyForgivenessAsync(MemberId, _selected.ToList());
            await OnForgivenessApplied.InvokeAsync();
            await CloseAsync();
        }
        catch (Exception ex)
        {
            _applyError = $"Failed to apply forgiveness: {ex.Message}";
        }
        finally
        {
            _applying = false;
        }
    }

    private void Close() => _ = CloseAsync();

    private async Task CloseAsync()
    {
        _items.Clear();
        _selected.Clear();
        _loading = true;
        _errorMessage = null;
        _applyError = null;
        await IsVisibleChanged.InvokeAsync(false);
    }
}
