using System.Text.RegularExpressions;

namespace StageFright.UI.Tests.Layout;

/// <summary>
/// Guards the global layout rules from issue #385 in <c>StageFright.App/wwwroot/app.css</c>:
/// every page sits at the top of the main pane (never centred vertically), and every
/// tab in a BlazorBootstrap tab group takes the height of the tallest tab, so switching
/// tabs never makes the content jump. bUnit cannot apply CSS, so these read the rules directly.
/// </summary>
public class LayoutAlignmentCssGuardTests
{
    private static readonly string AppCss = File.ReadAllText(Path.Combine(
        RepoRoot(), "src", "StageFright.App", "wwwroot", "app.css"));

    [Fact]
    public void Should_TopAlignEveryPage_When_ShellContentInnerIsStyled()
    {
        var rule = RuleBody(".shell-content-inner");

        // "margin: 0 auto" centres horizontally only; "margin: auto" inside the flex column
        // also centred the page vertically (the issue #383 behaviour #385 removed).
        Assert.Contains("margin: 0 auto;", rule);
        Assert.DoesNotContain("margin: auto;", rule);
    }

    [Fact]
    public void Should_StackEveryTabPaneInOneGridCell_When_TabContentIsStyled()
    {
        Assert.Contains("display: grid;", RuleBody(".tab-content"));

        var pane = RuleBody(".tab-content > .tab-pane");
        Assert.Contains("display: block;", pane);
        Assert.Contains("grid-area: 1 / 1;", pane);
        Assert.Contains("visibility: hidden;", pane);
    }

    [Fact]
    public void Should_ShowOnlyTheActiveTabPane_When_PanesShareOneCell()
    {
        Assert.Contains("visibility: visible;", RuleBody(".tab-content > .tab-pane.active"));
    }

    private static string RuleBody(string selector)
    {
        var match = Regex.Match(AppCss, @"(?m)^" + Regex.Escape(selector) + @"\s*\{([^}]*)\}");
        Assert.True(match.Success, $"app.css has no top-level rule for '{selector}'.");
        return match.Groups[1].Value;
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "StageFrightCommunity.slnx")))
            dir = dir.Parent;
        return dir?.FullName
            ?? throw new InvalidOperationException("Could not locate StageFrightCommunity.slnx above " + AppContext.BaseDirectory);
    }
}
