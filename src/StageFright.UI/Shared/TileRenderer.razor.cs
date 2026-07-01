using Microsoft.AspNetCore.Components;
using StageFright.Core.Modules.Dashboard;
using StageFright.Plugins.Contracts;

namespace StageFright.UI.Shared;

public partial class TileRenderer : ComponentBase
{
    /// <summary>The tile provider whose body component this renderer displays.</summary>
    [Parameter, EditorRequired]
    public IDashboardTileProvider Provider { get; set; } = null!;

    /// <summary>Pre-started load task from DashboardService.LoadTileAsync.</summary>
    [Parameter, EditorRequired]
    public Task<TileLoadResult> LoadTask { get; set; } = null!;

    private TileLoadResult? _result;
    private bool _loading = true;
    private Task<TileLoadResult>? _activeTask;
    private Guid _renderKey = Guid.NewGuid();

    /// <summary>
    /// Fires on first render and whenever parameters change (e.g. new LoadTask after dashboard refresh).
    /// Changing _renderKey forces Blazor to dispose and recreate DynamicComponent even when _loading
    /// toggles true→false synchronously (before a render cycle) due to a fast-completing LoadTask.
    /// </summary>
    protected override void OnParametersSet()
    {
        if (LoadTask == _activeTask) return;
        _activeTask = LoadTask;
        _loading = true;
        _result = null;
        _renderKey = Guid.NewGuid();
        _ = AwaitLoadAsync();
    }

    private async Task AwaitLoadAsync()
    {
        var taskSnapshot = _activeTask;
        try
        {
            _result = await taskSnapshot!;
        }
        catch (Exception ex)
        {
            _result = new TileLoadResult(Provider, null, ex);
        }
        finally
        {
            if (_activeTask == taskSnapshot)
            {
                _loading = false;
                await InvokeAsync(StateHasChanged);
            }
        }
    }
}
