using System.Xml.Linq;

namespace StageFright.Localization.Tests.Scanning;

/// <summary>
/// Parses a <c>.resx</c> file's <c>&lt;data name="..."&gt;</c> entries into key/value pairs.
/// Used by the completeness, orphan-key and placeholder-parity guard tests (Phase 3+) to compare
/// what a neutral or satellite <c>.resx</c> actually defines against what code references.
/// </summary>
public static class ResxKeyScanner
{
    public static IReadOnlyDictionary<string, string> ScanFile(string resxPath)
    {
        var document = XDocument.Load(resxPath);
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var data in document.Root!.Elements("data"))
        {
            var name = (string?)data.Attribute("name");
            if (name is null)
                continue;

            entries[name] = data.Element("value")?.Value ?? string.Empty;
        }

        return entries;
    }
}
