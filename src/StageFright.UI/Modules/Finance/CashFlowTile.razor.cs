using System.Globalization;
using BlazorBootstrap;
using Microsoft.AspNetCore.Components;
using StageFright.Core.Contracts;

namespace StageFright.UI.Modules.Finance;

/// <summary>
/// Dashboard tile body for the six-month cash-flow chart (design 3a): paired monthly
/// income/expense bars from the GL summary service, as a BlazorBootstrap bar chart.
/// </summary>
public partial class CashFlowTile : ComponentBase
{
    private const int MonthWindow = 6;

    [Inject] private IFinanceSummaryService FinanceSummaryService { get; set; } = null!;

    private bool _loading = true;
    private bool _error;
    private bool _hasData;

    private BarChart _chart = default!;
    private ChartData _chartData = default!;
    private BarChartOptions _chartOptions = default!;
    private bool _chartInitialized;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var months = await FinanceSummaryService.GetMonthlyCashFlowAsync(DateTime.Today, MonthWindow);

            if (months.Any(m => m.Income != 0m || m.Expenses != 0m))
            {
                _chartData = new ChartData
                {
                    Labels = months
                        .Select(m => CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(m.Month))
                        .ToList(),
                    Datasets = new List<IChartDataset>
                    {
                        new BarChartDataset
                        {
                            Label = "Income",
                            Data = months.Select(m => (double?)(double)m.Income).ToList(),
                            BackgroundColor = new List<string> { "#4f8dff" },
                            BorderColor = new List<string> { "#4f8dff" }
                        },
                        new BarChartDataset
                        {
                            Label = "Expenses",
                            Data = months.Select(m => (double?)(double)m.Expenses).ToList(),
                            BackgroundColor = new List<string> { "rgba(148, 163, 184, 0.55)" },
                            BorderColor = new List<string> { "rgba(148, 163, 184, 0.55)" }
                        }
                    }
                };

                _chartOptions = new BarChartOptions { Responsive = true };
                _chartOptions.Plugins.Legend!.Display = true;
                _hasData = true;
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
        if (!_loading && !_error && _hasData && !_chartInitialized && _chart is not null)
        {
            _chartInitialized = true;
            await _chart.InitializeAsync(_chartData, _chartOptions);
        }
    }
}
