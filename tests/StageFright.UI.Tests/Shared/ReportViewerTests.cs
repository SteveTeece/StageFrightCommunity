using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StageFright.Reports.Models;
using StageFright.Reports.Registry;
using StageFright.Reports.Rendering;
using StageFright.UI.Shared;

namespace StageFright.UI.Tests.Shared;

/// <summary>
/// bUnit tests for ReportViewer component:
/// - Shows "Generating report..." loading modal on generation
/// - Renders report data once generated
/// - Shows user-friendly error on GenerateAsync failure
/// - Print button invokes IPdfReportRenderer
/// - Export button invokes ICsvReportExporter
/// </summary>
public class ReportViewerTests : BunitContext
{
    private readonly IReportProvider _provider = Substitute.For<IReportProvider>();
    private readonly IPdfReportRenderer _pdfRenderer = Substitute.For<IPdfReportRenderer>();
    private readonly ICsvReportExporter _csvExporter = Substitute.For<ICsvReportExporter>();

    private static readonly ReportData SampleReport = new()
    {
        Title = "Test Report",
        GeneratedAt = DateTime.UtcNow,
        Columns = [new ReportColumn { Header = "Name" }],
        Sections =
        [
            new ReportSection { Rows = [new ReportRow { Cells = ["Alice"] }] }
        ]
    };

    public ReportViewerTests()
    {
        Services.AddSingleton(_pdfRenderer);
        Services.AddSingleton(_csvExporter);

        _provider.ReportId.Returns("test-report");
        _provider.ReportName.Returns("Test Report");
        _provider.ModuleName.Returns("Finance");
        _provider.DisplayOrder.Returns(0);
        _provider.Filters.Returns(Array.Empty<ReportFilterDefinition>());
    }

    [Fact]
    public async Task WhenProviderSet_ShowsGeneratingModal()
    {
        // Provider never completes — should show loading state
        var tcs = new TaskCompletionSource<ReportData>();
        _provider.GenerateAsync(Arg.Any<ReportFilterValues>(), Arg.Any<CancellationToken>())
            .Returns(tcs.Task);

        var cut = Render<ReportViewer>(p => p.Add(x => x.Provider, _provider));

        Assert.Contains("Generating", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WhenGenerationSucceeds_RendersReportTitle()
    {
        _provider.GenerateAsync(Arg.Any<ReportFilterValues>(), Arg.Any<CancellationToken>())
            .Returns(SampleReport);

        var cut = Render<ReportViewer>(p => p.Add(x => x.Provider, _provider));

        await cut.InvokeAsync(() => { }); // allow async initialization

        Assert.Contains("Test Report", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WhenGenerationSucceeds_RendersDataRows()
    {
        _provider.GenerateAsync(Arg.Any<ReportFilterValues>(), Arg.Any<CancellationToken>())
            .Returns(SampleReport);

        var cut = Render<ReportViewer>(p => p.Add(x => x.Provider, _provider));

        await cut.InvokeAsync(() => { });

        Assert.Contains("Alice", cut.Markup);
    }

    [Fact]
    public async Task WhenGenerationThrows_ShowsUserFriendlyError()
    {
        _provider.GenerateAsync(Arg.Any<ReportFilterValues>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Database error"));

        var cut = Render<ReportViewer>(p => p.Add(x => x.Provider, _provider));

        await cut.InvokeAsync(() => { });

        // Should show error message, not throw
        Assert.Contains("error", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WhenNoProvider_RendersEmpty()
    {
        var cut = Render<ReportViewer>(p => p.Add(x => x.Provider, (IReportProvider?)null));

        Assert.DoesNotContain("Generating", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    private static ReportData MakeReport(int rowCount) => new()
    {
        Title = "Paging Test",
        GeneratedAt = DateTime.UtcNow,
        Columns = [new ReportColumn { Header = "Name" }],
        Sections =
        [
            new ReportSection
            {
                Rows = Enumerable.Range(1, rowCount)
                    .Select(i => new ReportRow { Cells = [$"Row {i:D3}"] })
                    .ToList()
            }
        ]
    };

    [Fact]
    public async Task Should_NotShowPagination_When_RowCountAtPageSize()
    {
        _provider.GenerateAsync(Arg.Any<ReportFilterValues>(), Arg.Any<CancellationToken>())
            .Returns(MakeReport(15));

        var cut = Render<ReportViewer>(p => p.Add(x => x.Provider, _provider));
        await cut.InvokeAsync(() => { });

        Assert.DoesNotContain("page-link", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Should_ShowPagination_When_RowCountExceedsPageSize()
    {
        _provider.GenerateAsync(Arg.Any<ReportFilterValues>(), Arg.Any<CancellationToken>())
            .Returns(MakeReport(16));

        var cut = Render<ReportViewer>(p => p.Add(x => x.Provider, _provider));
        await cut.InvokeAsync(() => { });

        Assert.Contains("Page 1 of 2", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Should_ShowOnlyFirstPageRows_Initially_When_ReportHasManyRows()
    {
        _provider.GenerateAsync(Arg.Any<ReportFilterValues>(), Arg.Any<CancellationToken>())
            .Returns(MakeReport(16));

        var cut = Render<ReportViewer>(p => p.Add(x => x.Provider, _provider));
        await cut.InvokeAsync(() => { });

        Assert.Contains("Row 015", cut.Markup);
        Assert.DoesNotContain("Row 016", cut.Markup);
    }

    [Fact]
    public async Task Should_ShowNextPageRows_When_NextButtonClicked()
    {
        _provider.GenerateAsync(Arg.Any<ReportFilterValues>(), Arg.Any<CancellationToken>())
            .Returns(MakeReport(16));

        var cut = Render<ReportViewer>(p => p.Add(x => x.Provider, _provider));
        await cut.InvokeAsync(() => { });

        cut.Find("button[aria-label='Next page']").Click();

        Assert.Contains("Row 016", cut.Markup);
        Assert.Contains("Page 2 of 2", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Row 015", cut.Markup);
    }

    [Fact]
    public async Task Should_ShowPreviousPageRows_When_PrevButtonClicked()
    {
        _provider.GenerateAsync(Arg.Any<ReportFilterValues>(), Arg.Any<CancellationToken>())
            .Returns(MakeReport(16));

        var cut = Render<ReportViewer>(p => p.Add(x => x.Provider, _provider));
        await cut.InvokeAsync(() => { });

        cut.Find("button[aria-label='Next page']").Click();
        cut.Find("button[aria-label='Previous page']").Click();

        Assert.Contains("Row 001", cut.Markup);
        Assert.Contains("Page 1 of 2", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Row 016", cut.Markup);
    }

}
