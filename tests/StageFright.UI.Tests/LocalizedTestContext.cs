using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using StageFright.Core.Localization;

namespace StageFright.UI.Tests;

/// <summary>
/// Shared bUnit base for components that render localized text. Registers the real
/// .resx-backed <c>IStringLocalizer&lt;T&gt;</c> (via <c>AddLocalization()</c>) so component
/// tests assert through resource keys / the localizer, never hardcoded English (FR-018), and
/// wires <see cref="EnumLocalizationExtensions"/> so <c>@value.LocalizeEnum()</c> also resolves
/// in tests (FR-024). Extends <see cref="RadzenGridTestContext"/> so localized component tests
/// keep the same RadzenDataGrid JS interop mocking as every other bUnit test in this project.
/// </summary>
public abstract class LocalizedTestContext : RadzenGridTestContext
{
    protected LocalizedTestContext()
    {
        Services.AddLocalization();
        var factory = Services.BuildServiceProvider().GetRequiredService<IStringLocalizerFactory>();
        EnumLocalizationExtensions.UseFactory(factory);
    }
}
