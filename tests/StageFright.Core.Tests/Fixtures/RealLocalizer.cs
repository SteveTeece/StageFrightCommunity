using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StageFright.Core.Localization;

namespace StageFright.Core.Tests.Fixtures;

/// <summary>
/// Real <c>.resx</c>-backed <see cref="ILocalizer"/> for tests that construct a service taking
/// the <see cref="ILocalizer"/> facade directly (no DI container) and still assert on the
/// extracted (en-AU) wording — e.g. <c>AgeCalculationService</c> DOB/age messages (spec 027).
/// </summary>
internal static class RealLocalizer
{
    public static ILocalizer Instance { get; } = new Localizer(
        new ResourceManagerStringLocalizerFactory(
            Options.Create(new LocalizationOptions()), NullLoggerFactory.Instance));
}
