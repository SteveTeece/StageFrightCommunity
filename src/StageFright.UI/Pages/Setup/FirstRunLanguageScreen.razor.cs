using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.Core.Contracts;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Settings;
using StageFright.UI.Layout;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Pages.Setup;

/// <summary>
/// First-run language-selection screen (spec 029, US1) — shown before the setup wizard on a
/// clean install (routed here by <c>App.razor.cs</c> whenever setup is incomplete and no
/// language preference has been recorded yet). Confirming records the choice via
/// <see cref="ILanguagePreferenceStore"/>, applies it to the running session immediately via
/// <see cref="CultureProvider.Switch"/> (no restart), then continues into the setup wizard. In
/// Debug builds (US3), a "Load sample data" option is shown instead — ticking it seeds the
/// database with the full sample dataset and opens straight on the dashboard, skipping the
/// wizard entirely.
/// </summary>
public partial class FirstRunLanguageScreen : ComponentBase
{
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private ISupportedLanguagesCatalog Languages { get; set; } = null!;
    [Inject] private ILanguageProvider LanguageProvider { get; set; } = null!;
    [Inject] private ILanguagePreferenceStore LanguagePreferenceStore { get; set; } = null!;
    [Inject] private ISetupService SetupService { get; set; } = null!;
    [Inject] private IServiceProvider ServiceProvider { get; set; } = null!;
    [Inject] private IStringLocalizer<SetupResource> L { get; set; } = null!;

    [CascadingParameter] private CultureProvider? CultureProvider { get; set; }
    [CascadingParameter] private ThemeProvider? ThemeProvider { get; set; }

    private string _selectedLanguageCode = string.Empty;
    private bool _confirming;
    private string? _errorMessage;

    // IDebugDataSeeder is only registered in Debug builds (MauiProgram.cs) — there is never a
    // database seed in Release, so resolve it optionally rather than requiring it via [Inject],
    // and hide the "Load sample data" switch entirely when it's unavailable.
    private IDebugDataSeeder? _debugSeeder;
    private bool _seedWithTestData;
    private bool _seedingInProgress;
    private string? _seedingProgress;

    protected override async Task OnInitializedAsync()
    {
        _debugSeeder = ServiceProvider.GetService(typeof(IDebugDataSeeder)) as IDebugDataSeeder;

        // Pre-select the FR-002 default (explicit → OS language → en-AU). Pre-setup, this
        // resolution ladder has no database or recorded preference to consult, so it reduces to
        // "OS language when shipped, else en-AU" — identical to the deleted LanguageSelectionTab.
        string resolved;
        try
        {
            var culture = await LanguageProvider.ResolveStartupCultureAsync();
            resolved = Languages.Find(culture.Name)?.CultureCode ?? Languages.Default.CultureCode;
        }
        catch
        {
            resolved = Languages.Default.CultureCode;
        }

        _selectedLanguageCode = resolved;
    }

    private void HandleLanguageChanged(ChangeEventArgs e)
    {
        var selected = e.Value?.ToString();
        if (string.IsNullOrWhiteSpace(selected))
            return;

        _selectedLanguageCode = selected;
    }

    private void HandleSeedWithTestDataChanged(bool value)
    {
        _seedWithTestData = value;
    }

    private async Task HandleConfirmAsync()
    {
        _errorMessage = null;
        _confirming = true;

        try
        {
            LanguagePreferenceStore.Set(_selectedLanguageCode);
            CultureProvider?.Switch(CultureInfo.GetCultureInfo(_selectedLanguageCode));

            if (_seedWithTestData && _debugSeeder is not null)
            {
                await SeedSampleDataAndNavigateAsync();
            }
            else
            {
                Nav.NavigateTo("/setup");
            }
        }
        finally
        {
            _confirming = false;
        }
    }

    private async Task SeedSampleDataAndNavigateAsync()
    {
        try
        {
            // InitializeAsync marks setup complete and rejects a second call. If an earlier
            // attempt got past it but SeedAsync then threw, setup is already done — skip straight
            // to (re)seeding so re-pressing Confirm recovers, instead of dead-ending forever on
            // "Setup has already been completed".
            if (!await SetupService.IsSetupCompleteAsync())
            {
                // A placeholder request — DebugDataSeeder overwrites organisation name and fee
                // schedule with its own generated values once it runs (see its own doc comment),
                // so these figures only need to satisfy SetupRequest/Settings validation, not
                // reflect anything real. Language and theme follow what was actually chosen here.
                var request = new SetupRequest(
                    OrganizationName: L["Setup_FirstRun_SampleOrganizationPlaceholder"],
                    AnnualFee: 0,
                    AttendanceFee: 0,
                    MembershipRenewalMonth: 1,
                    IsTaxApplicable: false,
                    TaxRate: null,
                    AnnualFeeTaxCode: null,
                    AttendanceFeeTaxCode: null,
                    Theme: ThemeProvider?.CurrentTheme ?? Theme.Dark,
                    LanguageCode: _selectedLanguageCode,
                    CurrencyCode: "AUD");

                await SetupService.InitializeAsync(request);
            }

            _seedingInProgress = true;
            try
            {
                var progress = new Progress<string>(msg =>
                {
                    _seedingProgress = msg;
                    InvokeAsync(StateHasChanged);
                });
                await Task.Run(() => _debugSeeder!.SeedAsync(progress));
            }
            finally
            {
                _seedingInProgress = false;
            }

            Nav.NavigateTo("/dashboard");
        }
        catch
        {
            _errorMessage = L["Setup_FirstRun_SeedingError"];
        }
    }
}
