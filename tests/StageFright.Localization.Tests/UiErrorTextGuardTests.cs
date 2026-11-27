using System.Text.RegularExpressions;

namespace StageFright.Localization.Tests;

/// <summary>
/// Error text a user sees in the UI is always app-authored and translated:
/// a catch-all handler never shows the exception's own text (framework or database messages are English and
/// can leak internals) and logs what it caught, and a form model never carries an English validation literal.
/// </summary>
public class UiErrorTextGuardTests
{
    private static readonly Regex CatchAll = new(@"catch \(Exception (\w+)\)(?! when)\s*\{", RegexOptions.Compiled);
    private static readonly Regex ValidationLiteral = new(@"ErrorMessage\s*=\s*""|new ValidationResult\(\s*""", RegexOptions.Compiled);

    [Fact]
    public void Should_NeverShowExceptionText_When_CatchAllHandlerScanned()
    {
        var findings = CatchAllBlocks()
            .Where(b => b.Body.Contains($"{b.Variable}.Message", StringComparison.Ordinal))
            .Select(b => b.Location)
            .ToList();

        Assert.True(findings.Count == 0,
            "A catch-all handler shows the exception's own text; use ErrorDetail.For(ex, Loc) instead:\n"
            + string.Join("\n", findings));
    }

    [Fact]
    public void Should_LogTheException_When_CatchAllHandlerShowsAnError()
    {
        var findings = CatchAllBlocks()
            .Where(b => b.Body.Contains("ErrorDetail.For", StringComparison.Ordinal)
                        && !b.Body.Contains("Logger.Log", StringComparison.Ordinal))
            .Select(b => b.Location)
            .ToList();

        Assert.True(findings.Count == 0,
            "A catch-all handler shows a generic error without logging the exception it caught:\n"
            + string.Join("\n", findings));
    }

    [Fact]
    public void Should_UseResourceMessages_When_FormModelValidationScanned()
    {
        var findings = UiSourceFiles()
            .SelectMany(f => File.ReadAllLines(f).Select((line, i) => (f, line, i)))
            .Where(x => !x.line.TrimStart().StartsWith("//", StringComparison.Ordinal) && ValidationLiteral.IsMatch(x.line))
            .Select(x => $"{Relative(x.f)}:{x.i + 1}  {x.line.Trim()}")
            .ToList();

        Assert.True(findings.Count == 0,
            "A validation message is an English literal; point the attribute at FormValidationMessages instead:\n"
            + string.Join("\n", findings));
    }

    private sealed record CatchBlock(string Variable, string Body, string Location);

    private static IEnumerable<CatchBlock> CatchAllBlocks()
    {
        foreach (var file in UiSourceFiles())
        {
            var text = File.ReadAllText(file);
            foreach (Match m in CatchAll.Matches(text))
            {
                int i = m.Index + m.Length, depth = 1;
                while (depth > 0 && i < text.Length)
                {
                    depth += text[i] switch { '{' => 1, '}' => -1, _ => 0 };
                    i++;
                }
                var line = text[..m.Index].Count(c => c == '\n') + 1;
                yield return new CatchBlock(m.Groups[1].Value, text[(m.Index + m.Length)..i], $"{Relative(file)}:{line}");
            }
        }
    }

    private static IEnumerable<string> UiSourceFiles() =>
        Directory.EnumerateFiles(Path.Combine(RepoRoot(), "src", "StageFright.UI"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static string Relative(string file) => Path.GetRelativePath(RepoRoot(), file).Replace('\\', '/');

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "StageFrightCommunity.slnx")))
            dir = dir.Parent;
        return dir?.FullName
            ?? throw new InvalidOperationException("Could not locate StageFrightCommunity.slnx above " + AppContext.BaseDirectory);
    }
}
