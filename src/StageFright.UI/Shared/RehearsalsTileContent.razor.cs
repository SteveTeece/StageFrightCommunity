using Microsoft.AspNetCore.Components;
using Radzen.Blazor;
using Radzen.Blazor.Rendering;
using StageFright.Core.Modules.Rehearsals;

namespace StageFright.UI.Shared;

public partial class RehearsalsTileContent : ComponentBase
{
    [Inject] private IRehearsalService RehearsalService { get; set; } = null!;

    private bool _loading = true;
    private bool _error;
    private DateTime? _recordedDate;
    private DateTime? _pendingDate;
    private DateTime? _upcomingDate;
    private string _attendedLabel = "—";
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
            var recorded = await RehearsalService.GetMostRecentPastWithAttendanceAsync(DateTime.Today);
            var pending = await RehearsalService.GetMostRecentPastWithoutAttendanceAsync(DateTime.Today);
            var upcoming = await RehearsalService.GetNextUpcomingAsync(DateTime.Today.AddDays(1));

            _recordedDate = recorded?.Date;
            _pendingDate = pending?.Date;
            _upcomingDate = upcoming?.Date;

            if (recorded?.StoredAttendanceRate is { } rate)
            {
                double ap = (double)rate;
                double abp = 100.0 - ap;
                _attendedLabel = $"{ap:F1}%";
                _absentLabel = $"{abp:F1}%";
                _hasRate = true;

                _chartData = new ChartData
                {
                    Labels = new List<string> { "Attended", "Absent" },
                    Datasets = new List<IChartDataset>
                    {
                        new DoughnutChartDataset
                        {
                            Data = new List<double?> { ap, abp },
                            BackgroundColor = new List<string> { "#198754", "#dc3545" }
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
