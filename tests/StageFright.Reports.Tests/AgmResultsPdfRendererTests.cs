using StageFright.Core.Modules.Agm;
using StageFright.Reports.Rendering;

namespace StageFright.Reports.Tests;

/// <summary>
/// Tests for AgmResultsPdfRenderer: byte[] output is non-empty/non-null and no exception is
/// thrown for populated positions, zero positions, and empty-organization-name input (issue #307).
/// </summary>
public class AgmResultsPdfRendererTests
{
    private readonly IAgmResultsPdfRenderer _renderer = new AgmResultsPdfRenderer();

    private static AgmResultsData MakeData(
        IReadOnlyList<AgmResultsPositionLine>? positionLines = null,
        IReadOnlyList<string>? generalCommitteeMemberNames = null) => new()
    {
        AgmDate = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc),
        AttendedCount = 3,
        TotalCount = 5,
        PositionLines = positionLines ?? Array.Empty<AgmResultsPositionLine>(),
        GeneralCommitteeMemberNames = generalCommitteeMemberNames ?? Array.Empty<string>()
    };

    [Fact]
    public void Render_ReturnsNonEmptyByteArray_ForPopulatedPositionsAndGeneralCommittee()
    {
        var data = MakeData(
            positionLines: [new AgmResultsPositionLine { Label = "President", MemberText = "Alice Anderson" }],
            generalCommitteeMemberNames: ["Bob Baker", "Carol Cooper"]);

        var bytes = _renderer.Render(data, "Test Choir");

        Assert.NotNull(bytes);
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public void Render_ReturnsNonEmptyByteArray_WhenNoPositionsRecorded()
    {
        var data = MakeData();

        var bytes = _renderer.Render(data, "Test Choir");

        Assert.NotNull(bytes);
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public void Render_DoesNotThrow_WhenOrganizationNameIsEmpty()
    {
        var data = MakeData(positionLines: [new AgmResultsPositionLine { Label = "Secretary", MemberText = "Alice Anderson" }]);

        var exception = Record.Exception(() => _renderer.Render(data, ""));

        Assert.Null(exception);
    }

    [Fact]
    public void Render_DoesNotThrow_ForManyGeneralCommitteeMembers()
    {
        var names = Enumerable.Range(1, 40).Select(i => $"Member {i}").ToList();
        var data = MakeData(generalCommitteeMemberNames: names);

        var exception = Record.Exception(() => _renderer.Render(data, "Test Choir"));

        Assert.Null(exception);
    }
}
