using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace StageFright.UI.Tests;

/// <summary>
/// Builds a real <c>.resx</c>-backed <see cref="IStringLocalizer{T}"/> for tests that construct a
/// service or provider taking a localizer directly (no bUnit <c>TestContext</c> / DI container).
/// Lookups resolve to the neutral (en-AU) baseline, so assertions on the extracted wording keep
/// matching (spec 027).
/// </summary>
internal static class RealStringLocalizer
{
    private static readonly IStringLocalizerFactory Factory =
        new ResourceManagerStringLocalizerFactory(
            Options.Create(new LocalizationOptions()), NullLoggerFactory.Instance);

    public static IStringLocalizer<T> For<T>() => new StringLocalizer<T>(Factory);
}
