using StageFright.Core.Modules.Events;
using StageFright.Reports.Rendering;

namespace StageFright.Reports.Tests;

/// <summary>
/// Tests for EventAttendanceSheetPdfRenderer: byte[] output is non-empty/non-null and no
/// exception is thrown for populated, zero-member, or empty-organization-name input. QuestPDF's
/// raw PDF bytes can't be asserted on visual content, mirroring AttendanceRollPdfRendererTests.cs's
/// convention.
/// </summary>
public class EventAttendanceSheetPdfRendererTests
{
    private readonly IEventAttendanceSheetPdfRenderer _renderer = new EventAttendanceSheetPdfRenderer();

    private static EventAttendanceSheetData MakeSheet(params EventAttendanceSheetMember[] members) => new()
    {
        EventDate = new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc),
        EventTypeName = "Performance",
        Members = members
    };

    private static EventAttendanceSheetMember AMember(string firstName, string lastName, bool participated = false) => new()
    {
        FirstName = firstName,
        LastName = lastName,
        Participated = participated
    };

    [Fact]
    public void Render_ReturnsNonEmptyByteArray_ForPopulatedRoster()
    {
        var data = MakeSheet(AMember("Alice", "Anderson"), AMember("Bob", "Baker", participated: true));

        var bytes = _renderer.Render(data, "Test Choir");

        Assert.NotNull(bytes);
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public void Render_ReturnsNonEmptyByteArray_ForZeroMemberSheet()
    {
        var data = MakeSheet();

        var bytes = _renderer.Render(data, "Test Choir");

        Assert.NotNull(bytes);
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public void Render_DoesNotThrow_WhenOrganizationNameIsEmpty()
    {
        var data = MakeSheet(AMember("Alice", "Anderson"));

        var exception = Record.Exception(() => _renderer.Render(data, ""));

        Assert.Null(exception);
    }

    // --- Pagination boundaries (must match CheckboxSheetPdfBuilder's private RowsPerColumn constant) ---

    private const int RowsPerColumn = 32;

    private static EventAttendanceSheetMember[] MakeMembers(int count) =>
        Enumerable.Range(1, count).Select(i => AMember($"First{i}", $"Last{i}")).ToArray();

    [Fact]
    public void Render_DoesNotThrow_WhenExactlyOneColumnFull()
    {
        var data = MakeSheet(MakeMembers(RowsPerColumn));

        var bytes = _renderer.Render(data, "Test Choir");

        Assert.NotNull(bytes);
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public void Render_DoesNotThrow_WhenSpillingIntoSecondColumn_SamePage()
    {
        var data = MakeSheet(MakeMembers(RowsPerColumn + 1));

        var bytes = _renderer.Render(data, "Test Choir");

        Assert.NotNull(bytes);
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public void Render_DoesNotThrow_WhenSpillingOntoSecondPage()
    {
        var data = MakeSheet(MakeMembers(2 * RowsPerColumn + 1));

        var bytes = _renderer.Render(data, "Test Choir");

        Assert.NotNull(bytes);
        Assert.NotEmpty(bytes);
    }
}
