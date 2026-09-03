using System.Text.RegularExpressions;
using Microsoft.Extensions.Localization;
using StageFright.Core.Modules.Localization.Resources;

namespace StageFright.Core.Localization;

/// <summary>
/// Default <see cref="ILocalizer"/> implementation. Resolves every lookup through the
/// registered <see cref="IStringLocalizerFactory"/> — decorated in composition with
/// <see cref="MissingKeyLoggingLocalizerFactory"/>, so a missing key is logged and falls back
/// to the neutral (en-AU) value automatically (FR-008/FR-009).
/// </summary>
public class Localizer : ILocalizer
{
    private static readonly Regex NamedPlaceholderPattern = new(@"\{([A-Za-z][A-Za-z0-9]*)\}", RegexOptions.Compiled);

    private readonly IStringLocalizerFactory _factory;

    public Localizer(IStringLocalizerFactory factory)
    {
        _factory = factory;
    }

    public string Get<TResource>(string key) => _factory.Create(typeof(TResource))[key];

    public string Get<TResource>(string key, params object[] args)
    {
        var template = _factory.Create(typeof(TResource))[key].Value;
        return FormatNamedPlaceholders(template, args);
    }

    public string Plural<TResource>(string key, int count, params object[] args)
    {
        var pluralKey = count == 1 ? key + "_One" : key + "_Other";
        var allArgs = new object[args.Length + 1];
        allArgs[0] = count;
        Array.Copy(args, 0, allArgs, 1, args.Length);
        return Get<TResource>(pluralKey, allArgs);
    }

    public string Enum(System.Enum value) =>
        Get<EnumsResource>(EnumLocalizationExtensions.EnumResourceKey(value));

    /// <summary>
    /// Substitutes each named token (e.g. <c>{OrganisationName}</c>, <c>{Count}</c>) with a value
    /// from <paramref name="args"/>. Each <em>distinct</em> token binds to an arg by the order the
    /// token first appears in <paramref name="template"/>; every occurrence of that token then
    /// renders the bound value, so a token repeated in the text renders its value each time rather
    /// than leaking the literal <c>{Token}</c>. Once every arg is bound, further distinct tokens
    /// are left as literals. A translation may move surrounding words but must keep each key's
    /// token set — and their first-appearance order — consistent across cultures; the
    /// placeholder-parity guard (resource-key-catalog.md §3) enforces the set.
    /// </summary>
    private static string FormatNamedPlaceholders(string template, object[] args)
    {
        if (args.Length == 0)
            return template;

        var boundValues = new Dictionary<string, string>(StringComparer.Ordinal);
        return NamedPlaceholderPattern.Replace(template, match =>
        {
            var token = match.Groups[1].Value;
            if (!boundValues.TryGetValue(token, out var value))
            {
                value = boundValues.Count < args.Length
                    ? Convert.ToString(args[boundValues.Count]) ?? string.Empty
                    : match.Value;
                boundValues[token] = value;
            }

            return value;
        });
    }
}
