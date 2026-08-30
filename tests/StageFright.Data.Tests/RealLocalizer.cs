using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StageFright.Core.Localization;

namespace StageFright.Data.Tests;

/// <summary>
/// Real <c>.resx</c>-backed <see cref="ILocalizer"/> for Data.Tests integration tests that
/// construct a Core service taking the <see cref="ILocalizer"/> facade directly (spec 027,
/// T041) — e.g. <c>SetupService</c>, <c>BackupService</c> — and still assert on the extracted
/// (en-AU) wording of the exception messages those services raise.
/// </summary>
internal static class RealLocalizer
{
    public static ILocalizer Instance { get; } = new Localizer(
        new ResourceManagerStringLocalizerFactory(
            Options.Create(new LocalizationOptions()), NullLoggerFactory.Instance));
}
