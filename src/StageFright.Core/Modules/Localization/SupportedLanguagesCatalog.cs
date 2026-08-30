using System.Globalization;
using System.Text.RegularExpressions;
using StageFright.Core.Contracts;

namespace StageFright.Core.Modules.Localization;

/// <summary>
/// Default <see cref="ISupportedLanguagesCatalog"/> — builds the shipped-language list at
/// runtime (FR-011, resolved 2026-08-27). It enumerates the satellite-assembly culture folders
/// (<c>&lt;culture&gt;/&lt;assembly&gt;.resources.dll</c>) beside the app for the resource-owning
/// assemblies, adds the always-present neutral <c>en-AU</c> baseline, and drops any culture
/// whose name matches the pseudo-locale pattern <c>qps-*</c> so the test pseudo-locale never
/// reaches the picker or FR-023 matching. There is no hand-maintained list — dropping in a new
/// <c>&lt;Marker&gt;.&lt;culture&gt;.resx</c> set is all it takes for a language to be offered
/// (SC-003). The result is the always-present <c>en-AU</c> baseline plus one entry per shipped
/// <c>&lt;Marker&gt;.&lt;culture&gt;.resx</c> satellite set (e.g. <c>en-US</c>).
/// </summary>
public sealed class SupportedLanguagesCatalog : ISupportedLanguagesCatalog
{
    /// <summary>The neutral / baseline culture. Always present, always <see cref="SupportedLanguage.IsDefault"/>.</summary>
    public const string DefaultCultureCode = "en-AU";

    private static readonly string[] DefaultResourceAssemblyNames =
        ["StageFright.Core", "StageFright.UI", "StageFright.Reports"];

    private static readonly Regex PseudoLocalePattern =
        new("^qps-", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IReadOnlyList<string> _resourceAssemblyNames;
    private readonly Lazy<IReadOnlyList<SupportedLanguage>> _all;

    /// <summary>Probes the three resource-owning assemblies (Core, UI, Reports).</summary>
    public SupportedLanguagesCatalog()
        : this(DefaultResourceAssemblyNames)
    {
    }

    /// <param name="resourceAssemblyNames">
    /// Short names of the assemblies whose satellite folders identify a shipped culture — e.g.
    /// <c>StageFright.UI</c>. A folder counts only if it contains
    /// <c>&lt;name&gt;.resources.dll</c> for one of these, so third-party satellites
    /// (Radzen, the test platform) never register as an app language.
    /// </param>
    public SupportedLanguagesCatalog(IEnumerable<string> resourceAssemblyNames)
    {
        _resourceAssemblyNames = resourceAssemblyNames?.Where(n => !string.IsNullOrWhiteSpace(n)).ToArray()
            ?? DefaultResourceAssemblyNames;
        _all = new Lazy<IReadOnlyList<SupportedLanguage>>(BuildCatalog);
    }

    public IReadOnlyList<SupportedLanguage> All => _all.Value;

    public SupportedLanguage Default => All.First(l => l.IsDefault);

    public SupportedLanguage? Find(string? cultureCode)
    {
        if (string.IsNullOrWhiteSpace(cultureCode))
            return null;

        var trimmed = cultureCode.Trim();
        return All.FirstOrDefault(l => string.Equals(l.CultureCode, trimmed, StringComparison.OrdinalIgnoreCase));
    }

    private IReadOnlyList<SupportedLanguage> BuildCatalog()
    {
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { DefaultCultureCode };

        foreach (var code in DiscoverSatelliteCultures())
            if (!PseudoLocalePattern.IsMatch(code))
                codes.Add(code);

        return codes
            .Select(c => new SupportedLanguage(c, string.Equals(c, DefaultCultureCode, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(l => l.IsDefault)
            .ThenBy(l => l.Endonym, StringComparer.CurrentCulture)
            .ToList();
    }

    private IEnumerable<string> DiscoverSatelliteCultures()
    {
        string baseDir;
        try
        {
            baseDir = AppContext.BaseDirectory;
        }
        catch
        {
            yield break;
        }

        if (string.IsNullOrEmpty(baseDir) || !Directory.Exists(baseDir))
            yield break;

        IEnumerable<string> subDirs;
        try
        {
            subDirs = Directory.EnumerateDirectories(baseDir);
        }
        catch (IOException)
        {
            yield break;
        }
        catch (UnauthorizedAccessException)
        {
            yield break;
        }

        foreach (var dir in subDirs)
        {
            var folderName = Path.GetFileName(dir);
            if (string.IsNullOrEmpty(folderName))
                continue;

            if (!ContainsOurSatellite(dir))
                continue;

            CultureInfo culture;
            try
            {
                culture = CultureInfo.GetCultureInfo(folderName);
            }
            catch (CultureNotFoundException)
            {
                continue;
            }

            yield return culture.Name;
        }
    }

    private bool ContainsOurSatellite(string cultureFolder)
    {
        foreach (var name in _resourceAssemblyNames)
        {
            try
            {
                if (File.Exists(Path.Combine(cultureFolder, name + ".resources.dll")))
                    return true;
            }
            catch (IOException)
            {
                // ignore and keep probing the remaining assemblies
            }
        }

        return false;
    }
}
