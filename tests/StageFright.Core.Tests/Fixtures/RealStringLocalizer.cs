using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace StageFright.Core.Tests.Fixtures;

/// <summary>
/// Builds a real <c>.resx</c>-backed <see cref="IStringLocalizer{T}"/> for tests that construct a
/// service or provider taking a localizer directly and still assert on the extracted (en-AU)
/// wording — e.g. menu-item providers whose <c>Title</c>/<c>ShortLabel</c> now come from
/// <c>NavigationResource</c> (spec 027). Lookups resolve to the neutral baseline.
/// </summary>
internal static class RealStringLocalizer
{
    private static readonly IStringLocalizerFactory Factory =
        new ResourceManagerStringLocalizerFactory(
            Options.Create(new LocalizationOptions()), NullLoggerFactory.Instance);

    public static IStringLocalizer<T> For<T>() => new StringLocalizer<T>(Factory);
}
