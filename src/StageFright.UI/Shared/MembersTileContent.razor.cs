using BlazorBootstrap;
using Microsoft.AspNetCore.Components;
using Radzen;
using Radzen.Blazor;
using Radzen.Blazor.Rendering;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Members;

namespace StageFright.UI.Shared;

public partial class MembersTileContent : ComponentBase
{
    [Inject] private IMemberService MemberService { get; set; } = null!;

    private bool _loading = true;
    private bool _error;
    private int _activeCount;
    private int _inactiveCount;
    private int _total => _activeCount + _inactiveCount;

    private DoughnutChart _chart = default!;
    private ChartData _chartData = default!;
    private DoughnutChartOptions _chartOptions = default!;
    private bool _chartInitialized;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var active = await MemberService.GetByStatusAsync(MemberStatus.Active);
            var inactive = await MemberService.GetByStatusAsync(MemberStatus.Inactive);
            _activeCount = active.Count;
            _inactiveCount = inactive.Count;

            _chartData = new ChartData
            {
                Labels = new List<string> { "Active", "Inactive" },
                Datasets = new List<IChartDataset>
                {
                    new DoughnutChartDataset
                    {
                        Data = new List<double?> { _activeCount, _inactiveCount },
                        BackgroundColor = new List<string> { "#0d6efd", "#dc3545" }
                    }
                }
            };

            _chartOptions = new DoughnutChartOptions { Responsive = true };
            _chartOptions.Plugins.Legend!.Display = false;
        }
        catch
        {
            _error = true;
        }
        finally
        {
            _loading = false;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_loading && !_error && _total > 0 && !_chartInitialized && _chart is not null)
        {
            _chartInitialized = true;
            await _chart.InitializeAsync(_chartData, _chartOptions);
        }
    }
}
