using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StageFright.Core.Localization;

namespace StageFright.Reports.Tests;

/// <summary>
/// Real <c>.resx</c>-backed <see cref="ILocalizer"/> for report tests that construct a service
/// taking the <see cref="ILocalizer"/> facade directly (spec 027) — e.g. <c>AgeCalculationService</c>
/// used by <c>MemberListReportProvider</c>.
/// </summary>
internal static class RealLocalizer
{
    public static ILocalizer Instance { get; } = new Localizer(
        new ResourceManagerStringLocalizerFactory(
            Options.Create(new LocalizationOptions()), NullLoggerFactory.Instance));
}
