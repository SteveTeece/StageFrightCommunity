using System.Reflection;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Plugins.Contracts;
using StageFright.UI.Pages.Settings;
using AppSettings = StageFright.Core.Entities.Settings;

namespace StageFright.UI.Tests.Pages.Settings;

/// <summary>
/// Tests for SettingsPage after inserting the "Sales Tax" tab (FR-118): tab order in the
/// rendered markup, and the DefaultTabIndex/lazy-render-flag switch logic driven by the
/// `?tab=` query key. The switch logic is verified by invoking OnInitialized directly via
/// reflection, since [SupplyParameterFromQuery] binding requires the full Router pipeline
/// that bUnit doesn't host for a bare component render.
/// </summary>
public class SettingsPageTests : LocalizedTestContext
{
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();

    public SettingsPageTests()
    {
        Services.AddSingleton(_settingsService);
        Services.AddSingleton<IEnumerable<ISettingsTabProvider>>(Array.Empty<ISettingsTabProvider>());
        _settingsService.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new AppSettings
            {
                Id = Guid.NewGuid(),
                OrganizationName = "Test Org",
                AnnualFee = 75m,
                AttendanceFee = 5m,
                MembershipRenewalMonth = 1
            });
    }

    [Fact]
    public void SalesTaxTab_RendersImmediatelyAfterGeneral()
    {
        JSInterop.SetupVoid("window.blazorBootstrap.tabs.initialize", _ => true);

        var cut = Render<SettingsPage>();

        var generalIndex = cut.Markup.IndexOf("General", StringComparison.Ordinal);
        var taxIndex = cut.Markup.IndexOf("Sales Tax", StringComparison.Ordinal);
        var eventTypesIndex = cut.Markup.IndexOf("Event Types", StringComparison.Ordinal);

        Assert.True(generalIndex >= 0);
        Assert.True(taxIndex > generalIndex);
        Assert.True(eventTypesIndex > taxIndex);
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData("general", 0)]
    [InlineData("tax", 1)]
    [InlineData("event-types", 2)]
    [InlineData("backup", 3)]
    public void OnInitialized_SetsDefaultTabIndex_FromTabQuery(string? tabQuery, int expectedIndex)
    {
        var page = CreateUninitializedPage(tabQuery);

        InvokeOnInitialized(page);

        Assert.Equal(expectedIndex, GetPrivateProperty<int>(page, "DefaultTabIndex"));
    }

    [Fact]
    public void OnInitialized_ShowsOnlyGeneralTab_ByDefault()
    {
        var page = CreateUninitializedPage(null);
        InvokeOnInitialized(page);

        Assert.True(GetInternalField(page, "GeneralShown"));
        Assert.False(GetInternalField(page, "TaxShown"));
        Assert.False(GetInternalField(page, "EventTypesShown"));
        Assert.False(GetInternalField(page, "BackupShown"));
    }

    [Fact]
    public void OnInitialized_ShowsOnlyTaxTab_WhenTabQueryIsTax()
    {
        var page = CreateUninitializedPage("tax");
        InvokeOnInitialized(page);

        Assert.False(GetInternalField(page, "GeneralShown"));
        Assert.True(GetInternalField(page, "TaxShown"));
        Assert.False(GetInternalField(page, "EventTypesShown"));
        Assert.False(GetInternalField(page, "BackupShown"));
    }

    [Fact]
    public void OnInitialized_ShowsOnlyEventTypesTab_WhenTabQueryIsEventTypes()
    {
        var page = CreateUninitializedPage("event-types");
        InvokeOnInitialized(page);

        Assert.False(GetInternalField(page, "GeneralShown"));
        Assert.False(GetInternalField(page, "TaxShown"));
        Assert.True(GetInternalField(page, "EventTypesShown"));
        Assert.False(GetInternalField(page, "BackupShown"));
    }

    [Fact]
    public void OnInitialized_ShowsOnlyBackupTab_WhenTabQueryIsBackup()
    {
        var page = CreateUninitializedPage("backup");
        InvokeOnInitialized(page);

        Assert.False(GetInternalField(page, "GeneralShown"));
        Assert.False(GetInternalField(page, "TaxShown"));
        Assert.False(GetInternalField(page, "EventTypesShown"));
        Assert.True(GetInternalField(page, "BackupShown"));
    }

    // --- Reflection helpers: [SupplyParameterFromQuery]/[Inject] properties are private,
    // and there's no Router pipeline in a bare bUnit render to populate TabQuery from the URL. ---

    private static SettingsPage CreateUninitializedPage(string? tabQuery)
    {
        var page = new SettingsPage();
        var type = typeof(SettingsPage);

        type.GetProperty("TabQuery", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(page, tabQuery);
        type.GetProperty("TabProviders", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(page, Array.Empty<ISettingsTabProvider>());
        type.GetProperty("Logger", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(page, NullLogger<SettingsPage>.Instance);

        return page;
    }

    private static void InvokeOnInitialized(SettingsPage page) =>
        typeof(SettingsPage).GetMethod("OnInitialized", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(page, null);

    private static T GetPrivateProperty<T>(SettingsPage page, string name) =>
        (T)typeof(SettingsPage).GetProperty(name, BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(page)!;

    private static bool GetInternalField(SettingsPage page, string name) =>
        (bool)typeof(SettingsPage).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(page)!;
}
