namespace StageFright.Reports.Models;

/// <summary>
/// A bag of filter key-value pairs submitted by the user when generating a report.
/// Keys match ReportFilterDefinition.Key values from the provider's Filters list.
/// </summary>
public class ReportFilterValues
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Returns the value for the given filter key, or null if not set.</summary>
    public string? Get(string key) => _values.TryGetValue(key, out var v) ? v : null;

    /// <summary>Sets or overwrites the value for the given filter key.</summary>
    public void Set(string key, string value) => _values[key] = value;

    /// <summary>Returns all current key-value pairs.</summary>
    public IReadOnlyDictionary<string, string> All => _values;
}
