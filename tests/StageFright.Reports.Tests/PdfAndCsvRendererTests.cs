using StageFright.Core.Enums;
using StageFright.Reports.Models;
using StageFright.Reports.Rendering;

namespace StageFright.Reports.Tests;

/// <summary>
/// Tests for PdfReportRenderer and CsvReportExporter:
/// - PDF: byte[] is non-empty
/// - CSV: first row = column headers
/// - CSV: values with commas/quotes are RFC 4180 escaped
/// - CSV: empty report → headers only
/// </summary>
public class PdfAndCsvRendererTests
{
    private readonly IPdfReportRenderer _pdfRenderer;
    private readonly ICsvReportExporter _csvExporter;

    public PdfAndCsvRendererTests()
    {
        _pdfRenderer = new PdfReportRenderer(RealLocalizer.Instance);
        _csvExporter = new CsvReportExporter();
    }

    // --- PDF tests ---

    [Fact]
    public void PdfRenderer_Render_ReturnsNonEmptyByteArray()
    {
        var report = MakeSimpleReport();

        var bytes = _pdfRenderer.Render(report);

        Assert.NotNull(bytes);
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public void PdfRenderer_Render_EmptyReport_ReturnsNonEmptyByteArray()
    {
        var report = new ReportData
        {
            Title = "Empty Report",
            GeneratedAt = DateTime.UtcNow,
            Columns = Array.Empty<ReportColumn>(),
            Sections = Array.Empty<ReportSection>()
        };

        var bytes = _pdfRenderer.Render(report);

        Assert.NotEmpty(bytes);
    }

    [Fact]
    public void PdfRenderer_Render_WithGrandTotal_DoesNotThrow()
    {
        var report = MakeReportWithGrandTotal();

        var exception = Record.Exception(() => _pdfRenderer.Render(report));

        Assert.Null(exception);
    }

    // --- CSV tests ---

    [Fact]
    public void CsvExporter_Export_FirstRow_IsHeaders()
    {
        var report = MakeSimpleReport();

        var csv = _csvExporter.Export(report);
        var firstLine = csv.Split('\n')[0].TrimEnd('\r');

        Assert.Contains("Account", firstLine);
        Assert.Contains("Amount", firstLine);
    }

    [Fact]
    public void CsvExporter_Export_EmptyReport_HasHeadersOnly()
    {
        var report = new ReportData
        {
            Title = "Empty",
            GeneratedAt = DateTime.UtcNow,
            Columns = new[]
            {
                new ReportColumn { Header = "Col1" },
                new ReportColumn { Header = "Col2" }
            },
            Sections = Array.Empty<ReportSection>()
        };

        var csv = _csvExporter.Export(report);
        var lines = csv.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();

        Assert.Single(lines); // headers only
        Assert.Contains("Col1", lines[0]);
    }

    [Fact]
    public void CsvExporter_Export_ValueWithComma_IsQuoted()
    {
        var report = MakeReportWithValue("Smith, John");

        var csv = _csvExporter.Export(report);

        Assert.Contains("\"Smith, John\"", csv);
    }

    [Fact]
    public void CsvExporter_Export_ValueWithQuote_EscapedPerRfc4180()
    {
        var report = MakeReportWithValue("He said \"Hello\"");

        var csv = _csvExporter.Export(report);

        // RFC 4180: embedded quotes doubled
        Assert.Contains("\"He said \"\"Hello\"\"\"", csv);
    }

    [Fact]
    public void CsvExporter_Export_MultipleDataRows_AllPresent()
    {
        var report = MakeReportWithRows("Alice", "Bob", "Charlie");

        var csv = _csvExporter.Export(report);

        Assert.Contains("Alice", csv);
        Assert.Contains("Bob", csv);
        Assert.Contains("Charlie", csv);
    }

    [Fact]
    public void CsvExporter_Export_WritesSectionHeading()
    {
        // Regression for #286: section headings (e.g. "Income"/"Expenses" on Trial Balance/
        // Balance Sheet/Income Statement) must survive CSV export, not just PDF/on-screen.
        var report = MakeSimpleReport(); // section has Heading = "Income"

        var csv = _csvExporter.Export(report);
        var lines = csv.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();

        Assert.Contains(lines, l => l.TrimStart().StartsWith("Income"));
    }

    [Fact]
    public void CsvExporter_Export_OmitsHeadingRow_WhenSectionHasNoHeading()
    {
        var report = MakeReportWithValue("Some Row");

        var csv = _csvExporter.Export(report);
        var lines = csv.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();

        // Header row + one data row only — no extra heading row inserted.
        Assert.Equal(2, lines.Length);
    }

    // --- Helpers ---

    private static ReportData MakeSimpleReport()
        => new()
        {
            Title = "Test Report",
            SubTitle = "Jan – Dec 2026",
            GeneratedAt = DateTime.UtcNow,
            Columns = new[]
            {
                new ReportColumn { Header = "Account", Alignment = ReportColumnAlignment.Left },
                new ReportColumn { Header = "Amount", Alignment = ReportColumnAlignment.Right }
            },
            Sections = new[]
            {
                new ReportSection
                {
                    Heading = "Income",
                    Rows = new[]
                    {
                        new ReportRow { Cells = new[] { "Membership Dues", "1000.00" } }
                    },
                    Subtotal = new ReportRow { Cells = new[] { "Total Income", "1000.00" }, IsEmphasized = true }
                }
            },
            GrandTotal = new ReportRow { Cells = new[] { "Net Income", "1000.00" }, IsEmphasized = true }
        };

    private static ReportData MakeReportWithGrandTotal()
        => new()
        {
            Title = "With Grand Total",
            GeneratedAt = DateTime.UtcNow,
            Columns = new[] { new ReportColumn { Header = "Col" } },
            Sections = Array.Empty<ReportSection>(),
            GrandTotal = new ReportRow { Cells = new[] { "Grand Total" }, IsEmphasized = true }
        };

    private static ReportData MakeReportWithValue(string value)
        => new()
        {
            Title = "CSV Test",
            GeneratedAt = DateTime.UtcNow,
            Columns = new[] { new ReportColumn { Header = "Name" } },
            Sections = new[]
            {
                new ReportSection
                {
                    Rows = new[] { new ReportRow { Cells = new[] { value } } }
                }
            }
        };

    private static ReportData MakeReportWithRows(params string[] names)
        => new()
        {
            Title = "Multi Row",
            GeneratedAt = DateTime.UtcNow,
            Columns = new[] { new ReportColumn { Header = "Name" } },
            Sections = new[]
            {
                new ReportSection
                {
                    Rows = names.Select(n => new ReportRow { Cells = new[] { n } }).ToList()
                }
            }
        };
}
