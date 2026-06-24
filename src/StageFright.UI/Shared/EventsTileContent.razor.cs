using BlazorBootstrap;
using Microsoft.AspNetCore.Components;
using Radzen;
using Radzen.Blazor;
using Radzen.Blazor.Rendering;
using StageFright.Core.Contracts;
using StageFright.Core.Modules.Dashboard;
using StageFright.Core.Modules.Events;

namespace StageFright.UI.Shared;

public partial class EventsTileContent : ComponentBase
{
    [Inject] private IEventService EventService { get; set; } = null!;

    private bool _loading = true;
    private bool _error;
    private DateTime? _recordedDate;
    private DateTime? _pendingDate;
    private DateTime? _upcomingDate;
    private string _participatedLabel = "—";
    private string _absentLabel = "—";
    private bool _hasRate;

    private DoughnutChart _chart = default!;
    private ChartData _chartData = default!;
    private DoughnutChartOptions _chartOptions = default!;
    private bool _chartInitialized;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var recorded = await EventService.GetMostRecentPastWithParticipationAsync(DateTime.Today);
            var pending = await EventService.GetMostRecentPastWithoutParticipationAsync(DateTime.Today);
            var upcoming = await EventService.GetNextUpcomingAsync(DateTime.Today.AddDays(1));

            _recordedDate = recorded?.Date;
            _pendingDate = pending?.Date;
            _upcomingDate = upcoming?.Date;

            if (recorded?.StoredParticipationRate is { } rate)
            {
                double pp = (double)rate;
                double abp = 100.0 - pp;
                _participatedLabel = $"{pp:F1}%";
                _absentLabel = $"{abp:F1}%";
                _hasRate = true;

                _chartData = new ChartData
                {
                    Labels = new List<string> { "Participated", "Absent" },
                    Datasets = new List<IChartDataset>
                    {
                        new DoughnutChartDataset
                        {
                            Data = new List<double?> { pp, abp },
                            BackgroundColor = new List<string> { "#fd7e14", "#dc3545" }
                        }
                    }
                };

                _chartOptions = new DoughnutChartOptions { Responsive = true };
                _chartOptions.Plugins.Legend!.Display = false;
            }
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
        if (!_loading && !_error && _hasRate && !_chartInitialized && _chart is not null)
        {
            _chartInitialized = true;
            await _chart.InitializeAsync(_chartData, _chartOptions);
        }
    }
}
