using Microsoft.Extensions.Logging;
using StageFright.Reports.Models;

namespace StageFright.Reports.Registry;

/// <summary>
/// Collects all registered IReportProvider implementations and exposes them
/// grouped into menu sections. Ordering: Members first, Finance second,
/// then plugins alphabetically. Duplicate ReportIds are skipped and logged.
/// </summary>
public class ReportProviderRegistry : IReportProviderRegistry
{
    private readonly IReadOnlyDictionary<string, IReportProvider> _providers;
    private readonly ILogger<ReportProviderRegistry> _logger;

    public ReportProviderRegistry(
        IEnumerable<IReportProvider> providers,
        ILogger<ReportProviderRegistry> logger)
    {
        _logger = logger;

        var dict = new Dictionary<string, IReportProvider>(StringComparer.OrdinalIgnoreCase);
        foreach (var provider in providers)
        {
            if (dict.ContainsKey(provider.ReportId))
            {
                _logger.LogWarning("Duplicate ReportId '{ReportId}' from provider '{ProviderType}' — skipped.",
                    provider.ReportId, provider.GetType().Name);
                continue;
            }
            dict[provider.ReportId] = provider;
        }

        _providers = dict;
    }

    public IReadOnlyList<ReportMenuSection> GetMenuSections()
    {
        var byModule = _providers.Values
            .GroupBy(p => p.ModuleName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderBy(p => p.DisplayOrder).ToList(), StringComparer.OrdinalIgnoreCase);

        var sections = new List<ReportMenuSection>();

        // Members first
        if (byModule.TryGetValue("Members", out var membersProviders))
        {
            sections.Add(MakeSection("Members", membersProviders));
            byModule.Remove("Members");
        }

        // Finance second
        if (byModule.TryGetValue("Finance", out var financeProviders))
        {
            sections.Add(MakeSection("Finance", financeProviders));
            byModule.Remove("Finance");
        }

        // Remaining modules alphabetically
        foreach (var kvp in byModule.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            sections.Add(MakeSection(kvp.Key, kvp.Value));

        return sections;
    }

    public IReportProvider? GetProvider(string reportId)
        => _providers.TryGetValue(reportId, out var p) ? p : null;

    private static ReportMenuSection MakeSection(string module, IEnumerable<IReportProvider> providers)
        => new()
        {
            ModuleName = module,
            Reports = providers
                .Select(p => new ReportMenuEntry { ReportId = p.ReportId, ReportName = p.ReportName })
                .ToList()
        };
}
