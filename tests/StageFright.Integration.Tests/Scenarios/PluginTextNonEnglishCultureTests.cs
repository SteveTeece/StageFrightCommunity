using System.Globalization;
using StageFright.Reports.Models;
using StageFright.Reports.Rendering;
using StageFright.TestPlugin;

namespace StageFright.Integration.Tests.Scenarios;

/// <summary>
/// FR-020 (T030): plugin-contributed text is the plugin's own responsibility. Under a culture
/// the app ships no resource set for, a plugin that supplies only English strings must still
/// render through the host's report/tile pipeline without an exception and without blanking —
/// the host never tries to localise plugin text.
/// </summary>
/// <remarks>
/// Each case runs on a dedicated thread whose culture is set to <c>fr-FR</c> and discarded when
/// the thread exits, so the ambient culture never leaks into xUnit's parallel test threads.
/// </remarks>
public sealed class PluginTextNonEnglishCultureTests
{
    [Fact]
    public void Should_RenderPluginReportEnglishTextUnchanged_When_NonEnglishCultureActive_Integration()
    {
        RunUnderFrenchCulture(() =>
        {
            var provider = new TestReportProvider();
            var data = provider.GenerateAsync(new ReportFilterValues()).GetAwaiter().GetResult();

            // Plugin's own English strings survive verbatim — the host does not translate them.
            Assert.Equal("Test Plugin Report", data.Title);
            Assert.Equal(["Metric", "Value"], data.Columns.Select(c => c.Header));
            Assert.Equal(["Plugin Status", "Active"], data.Sections.Single().Rows.Single().Cells);

            // CsvReportExporter emits column headers + row cells (not the report Title).
            var csv = new CsvReportExporter().Export(data);
            Assert.Contains("Metric,Value", csv);
            Assert.Contains("Plugin Status,Active", csv);

            var pdf = new PdfReportRenderer().Render(data, "Test Org");
            Assert.NotNull(pdf);
            Assert.NotEmpty(pdf);
        });
    }

    [Fact]
    public void Should_ReturnPluginTileMetricsUnchanged_When_NonEnglishCultureActive_Integration()
    {
        RunUnderFrenchCulture(() =>
        {
            var provider = new TestTileProvider();
            var tile = provider.GetTileDataAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.Equal("Test Tile", provider.Title);
            Assert.Equal("42", tile.Metrics["Test Metric"]);
            Assert.Equal("Active", tile.Metrics["Plugin Status"]);
        });
    }

    private static void RunUnderFrenchCulture(Action body)
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            var french = new CultureInfo("fr-FR");
            CultureInfo.CurrentCulture = french;
            CultureInfo.CurrentUICulture = french;
            try
            {
                body();
            }
            catch (Exception ex)
            {
                captured = ex;
            }
        });
        thread.Start();
        thread.Join();

        if (captured is not null)
            throw new Xunit.Sdk.XunitException($"Assertion failed under fr-FR culture: {captured}");
    }
}
