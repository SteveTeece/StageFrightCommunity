using System.Globalization;
using Microsoft.AspNetCore.Components;
using Radzen.Blazor;
using Radzen.Blazor.Rendering;
using StageFright.Core.Contracts;

namespace StageFright.UI.Shared;

public partial class FinanceTileContent : ComponentBase
{
    [Inject] private IGLRepository GLRepository { get; set; } = null!;

    private bool _loading = true;
    private bool _error;
    private decimal _current;
    private decimal _days30;
    private decimal _days60;
    private decimal _days90Plus;
    private decimal _total => _current + _days30 + _days60 + _days90Plus;

    private BarChart _chart = default!;
    private ChartData _chartData = default!;
    private BarChartOptions _chartOptions = default!;
    private bool _chartInitialized;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            (_current, _days30, _days60, _days90Plus) = await GLRepository.GetAgingBucketsAsync();

            _chartData = new ChartData
            {
                Labels = new List<string> { "Current", "30 days", "60 days", "90+ days" },
                Datasets = new List<IChartDataset>
                {
                    new BarChartDataset
                    {
                        Data = new List<double?> { (double)_current, (double)_days30, (double)_days60, (double)_days90Plus },
                        BackgroundColor = new List<string> { "#0d6efd", "#ffc107", "#fd7e14", "#dc3545" }
                    }
                }
            };

            _chartOptions = new BarChartOptions { Responsive = true };
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
        if (!_loading && !_error && !_chartInitialized && _chart is not null)
        {
            _chartInitialized = true;
            await _chart.InitializeAsync(_chartData, _chartOptions);
        }
    }
}
