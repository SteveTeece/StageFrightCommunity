using StageFright.Core.Modules.Rehearsals;
using StageFright.Reports.Rendering;

namespace StageFright.Reports.Tests;

/// <summary>
/// Tests for AttendanceRollPdfRenderer: byte[] output is non-empty/non-null and no exception
/// is thrown for populated, zero-member, or empty-organization-name input. QuestPDF's raw PDF
/// bytes can't be asserted on visual content, mirroring PdfAndCsvRendererTests.cs's convention.
/// </summary>
public class AttendanceRollPdfRendererTests
{
    private readonly IAttendanceRollPdfRenderer _renderer = new AttendanceRollPdfRenderer();

    private static AttendanceRollData MakeRoll(params AttendanceRollMember[] members) => new()
    {
        RehearsalDate = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc),
        RehearsalTime = TimeSpan.FromHours(19),
        Members = members
    };

    private static AttendanceRollMember AMember(string firstName, string lastName, bool attended = false, bool rehearsalFeePaid = false) => new()
    {
        FirstName = firstName,
        LastName = lastName,
        Attended = attended,
        RehearsalFeePaid = rehearsalFeePaid
    };

    [Fact]
    public void Render_ReturnsNonEmptyByteArray_ForPopulatedRoster()
    {
        var data = MakeRoll(AMember("Alice", "Anderson"), AMember("Bob", "Baker"));

        var bytes = _renderer.Render(data, "Test Choir");

        Assert.NotNull(bytes);
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public void Render_ReturnsNonEmptyByteArray_ForZeroMemberRoll()
    {
        var data = MakeRoll();

        var bytes = _renderer.Render(data, "Test Choir");

        Assert.NotNull(bytes);
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public void Render_DoesNotThrow_WhenOrganizationNameIsEmpty()
    {
        var data = MakeRoll(AMember("Alice", "Anderson"));

        var exception = Record.Exception(() => _renderer.Render(data, ""));

        Assert.Null(exception);
    }

    [Fact]
    public void Render_ReturnsNonEmptyByteArray_ForMixOfPresentAndFeePaidStatuses()
    {
        var data = MakeRoll(
            AMember("Alice", "Anderson", attended: true, rehearsalFeePaid: true),
            AMember("Bob", "Baker", attended: true, rehearsalFeePaid: false));

        var bytes = _renderer.Render(data, "Test Choir");

        Assert.NotNull(bytes);
        Assert.NotEmpty(bytes);
    }

    // --- Pagination boundaries (must match the renderer's private RowsPerColumn constant) ---

    private const int RowsPerColumn = 32;

    private static AttendanceRollMember[] MakeMembers(int count) =>
        Enumerable.Range(1, count).Select(i => AMember($"First{i}", $"Last{i}")).ToArray();

    [Fact]
    public void Render_DoesNotThrow_WhenExactlyOneColumnFull()
    {
        var data = MakeRoll(MakeMembers(RowsPerColumn));

        var bytes = _renderer.Render(data, "Test Choir");

        Assert.NotNull(bytes);
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public void Render_DoesNotThrow_WhenSpillingIntoSecondColumn_SamePage()
    {
        var data = MakeRoll(MakeMembers(RowsPerColumn + 1));

        var bytes = _renderer.Render(data, "Test Choir");

        Assert.NotNull(bytes);
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public void Render_DoesNotThrow_WhenSpillingOntoSecondPage()
    {
        var data = MakeRoll(MakeMembers(2 * RowsPerColumn + 1));

        var bytes = _renderer.Render(data, "Test Choir");

        Assert.NotNull(bytes);
        Assert.NotEmpty(bytes);
    }
}
