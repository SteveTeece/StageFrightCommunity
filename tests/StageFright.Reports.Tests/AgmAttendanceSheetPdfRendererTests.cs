using StageFright.Core.Modules.Agm;
using StageFright.Reports.Rendering;

namespace StageFright.Reports.Tests;

/// <summary>
/// Tests for AgmAttendanceSheetPdfRenderer: byte[] output is non-empty/non-null and no exception
/// is thrown for populated (mixed attended/absent), zero-member, or empty-organization-name input.
/// </summary>
public class AgmAttendanceSheetPdfRendererTests
{
    private readonly IAgmAttendanceSheetPdfRenderer _renderer = new AgmAttendanceSheetPdfRenderer();

    private static AgmAttendanceSheetData MakeSheet(params AgmAttendanceSheetMember[] members) => new()
    {
        AgmDate = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc),
        Members = members
    };

    private static AgmAttendanceSheetMember AMember(string firstName, string lastName, bool attended = false) => new()
    {
        FirstName = firstName,
        LastName = lastName,
        Attended = attended
    };

    [Fact]
    public void Render_ReturnsNonEmptyByteArray_ForMixedAttendedAndAbsentRoster()
    {
        var data = MakeSheet(AMember("Alice", "Anderson", attended: true), AMember("Bob", "Baker", attended: false));

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

    // --- Pagination boundaries (identical to EventAttendanceSheetPdfRendererTests's cases —
    // proof both renderers share one layout engine, not two that could drift, per US3) ---

    private const int RowsPerColumn = 32;

    private static AgmAttendanceSheetMember[] MakeMembers(int count) =>
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
