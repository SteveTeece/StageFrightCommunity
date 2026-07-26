using StageFright.Core.Modules.Members;

namespace StageFright.Core.Tests.Modules.Members;

/// <summary>
/// Unit tests for MemberNameSplitter — trim/collapse-whitespace/split-on-first-space/truncate rule (FR-006, FR-008).
/// </summary>
public class MemberNameSplitterTests
{
    [Fact]
    public void Split_NormalTwoWordName_SplitsOnFirstSpace()
    {
        var (firstName, lastName) = MemberNameSplitter.Split("Jane Smith");

        Assert.Equal("Jane", firstName);
        Assert.Equal("Smith", lastName);
    }

    [Fact]
    public void Split_LeadingAndTrailingWhitespace_IsTrimmed()
    {
        var (firstName, lastName) = MemberNameSplitter.Split("  Jane Smith  ");

        Assert.Equal("Jane", firstName);
        Assert.Equal("Smith", lastName);
    }

    [Fact]
    public void Split_MultipleInternalSpaces_AreCollapsed()
    {
        var (firstName, lastName) = MemberNameSplitter.Split("Jane    Smith");

        Assert.Equal("Jane", firstName);
        Assert.Equal("Smith", lastName);
    }

    [Fact]
    public void Split_Mononym_LeavesLastNameBlank()
    {
        var (firstName, lastName) = MemberNameSplitter.Split("Cher");

        Assert.Equal("Cher", firstName);
        Assert.Equal(string.Empty, lastName);
    }

    [Fact]
    public void Split_MoreThanTwoWords_SplitsOnFirstSpaceOnly()
    {
        var (firstName, lastName) = MemberNameSplitter.Split("Mary Jane Watson");

        Assert.Equal("Mary", firstName);
        Assert.Equal("Jane Watson", lastName);
    }

    [Fact]
    public void Split_SideExceeding100Characters_IsTruncated()
    {
        var longFirst = new string('A', 150);
        var longLast = new string('B', 150);

        var (firstName, lastName) = MemberNameSplitter.Split($"{longFirst} {longLast}");

        Assert.Equal(100, firstName.Length);
        Assert.Equal(100, lastName.Length);
        Assert.Equal(new string('A', 100), firstName);
        Assert.Equal(new string('B', 100), lastName);
    }

    [Fact]
    public void Split_MononymExceeding100Characters_TruncatesFirstNameWithBlankLastName()
    {
        var longName = new string('A', 150);

        var (firstName, lastName) = MemberNameSplitter.Split(longName);

        Assert.Equal(100, firstName.Length);
        Assert.Equal(string.Empty, lastName);
    }
}
