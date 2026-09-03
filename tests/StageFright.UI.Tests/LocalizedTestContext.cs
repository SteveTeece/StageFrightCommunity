using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Localization;
using StageFright.Core.Modules.Members;

namespace StageFright.UI.Tests;

/// <summary>
/// Shared bUnit base for components that render localized text. Registers the real
/// .resx-backed <c>IStringLocalizer&lt;T&gt;</c> (via <c>AddLocalization()</c>) so component
/// tests assert through resource keys / the localizer, never hardcoded English (FR-018), and
/// wires <see cref="EnumLocalizationExtensions"/> so <c>@value.LocalizeEnum()</c> also resolves
/// in tests (FR-024). Extends <see cref="RadzenGridTestContext"/> so localized component tests
/// keep the same RadzenDataGrid JS interop mocking as every other bUnit test in this project.
/// </summary>
/// <remarks>
/// Also registers the spec 027 US3 language services (<see cref="ISupportedLanguagesCatalog"/>,
/// <see cref="ISystemCultureProvider"/>, <see cref="ILanguageProvider"/>) plus a null-returning
/// <see cref="ISettingsService"/> so the Settings / Setup language pickers render. A subclass
/// that registers its own <see cref="ISettingsService"/> substitute overrides the stub — the
/// last registration wins.
/// </remarks>
public abstract class LocalizedTestContext : RadzenGridTestContext
{
    protected LocalizedTestContext()
    {
        Services.AddLocalization();
        Services.AddScoped<ILocalizer, Localizer>();
        Services.AddScoped<AgeCalculationService>();

        Services.AddSingleton<ISupportedLanguagesCatalog, SupportedLanguagesCatalog>();
        Services.AddSingleton<ISystemCultureProvider, EnAuSystemCultureProvider>();
        Services.AddSingleton<ILanguagePreferenceStore, NullLanguagePreferenceStore>();
        Services.AddScoped<ISettingsService, NullSettingsService>();
        Services.AddScoped<ILanguageProvider, LanguageProvider>();

        var factory = Services.BuildServiceProvider().GetRequiredService<IStringLocalizerFactory>();
        EnumLocalizationExtensions.UseFactory(factory);
    }

    /// <summary>Reports <c>en-AU</c> as the OS UI culture so the resolution ladder is deterministic in tests.</summary>
    private sealed class EnAuSystemCultureProvider : ISystemCultureProvider
    {
        public CultureInfo GetUiCulture() => CultureInfo.GetCultureInfo("en-AU");
    }

    /// <summary>Stand-in for tests that do not register their own <see cref="ISettingsService"/>.</summary>
    private sealed class NullSettingsService : ISettingsService
    {
        public Task<Settings?> GetAsync(CancellationToken ct = default) => Task.FromResult<Settings?>(null);

        public Task SaveAsync(Settings settings, CancellationToken ct = default) => Task.CompletedTask;
    }

    /// <summary>Stand-in for tests that do not register their own <see cref="ILanguagePreferenceStore"/> — reports no recorded preference.</summary>
    private sealed class NullLanguagePreferenceStore : ILanguagePreferenceStore
    {
        public string? Get() => null;

        public void Set(string cultureCode)
        {
        }
    }
}
