using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using StageFright.Core.Contracts;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Settings;
using StageFright.UI.Layout;

namespace StageFright.UI.Pages.Setup;

public partial class SetupWizard : ComponentBase
{
    [Inject] private IServiceProvider ServiceProvider { get; set; } = null!;

    [CascadingParameter] private ThemeProvider? ThemeProvider { get; set; }

    private readonly SetupFormModel _model = new();
    private EditContext _editContext = null!;
    private IDebugDataSeeder? _debugSeeder;

    // Fully qualified — a bare "Tabs" is ambiguous with our own sibling
    // StageFright.UI.Pages.Setup.Tabs namespace (nested namespaces are reachable by
    // simple name from their enclosing namespace, no `using` required).
    private BlazorBootstrap.Tabs? _tabsRef;

    // Lazy-render flags: a tab's content is only instantiated once it has been shown, so
    // multiple tabs never touch the shared DbContext concurrently (see SettingsPage's own
    // precedent for this exact MAUI WebView gotcha). Grows as later stories add tabs.
    private readonly List<bool> _tabShown = new() { true, false, false, false, false };
    private int _currentTabIndex;

    private bool _submitting;
    private bool _seedingInProgress;
    private bool _seedWithTestData;
    private string? _errorMessage;
    private string? _seedingProgress;

    protected override void OnInitialized()
    {
        _editContext = new EditContext(_model);

        // IDebugDataSeeder is only registered in Debug builds (MauiProgram.cs) — there is
        // never a database seed in Release, so resolve it optionally rather than requiring
        // it via [Inject], and hide the "Load sample data" checkbox when it's unavailable.
        _debugSeeder = ServiceProvider.GetService(typeof(IDebugDataSeeder)) as IDebugDataSeeder;
    }

    // Called both by a direct tab-header click and by Next (which already knows the target
    // index) — a single place that keeps _currentTabIndex and the lazy-render flag in sync
    // regardless of which triggered the move (FR-003).
    private void SetActiveTab(int index)
    {
        _tabShown[index] = true;
        _currentTabIndex = index;
    }

    private async Task HandleNextAsync()
    {
        // Matches today's actual Next behavior (validates the whole model, not just the
        // current tab) — every other field already has a valid default, so in practice
        // only the current tab's own required fields can ever fail this (FR-004).
        if (!_editContext.Validate())
            return;

        var nextIndex = _currentTabIndex + 1;
        if (nextIndex >= _tabShown.Count)
            return;

        SetActiveTab(nextIndex);
        if (_tabsRef is not null)
            await _tabsRef.ShowTabByIndexAsync(nextIndex);
    }

    // EditForm only calls OnValidSubmit once the whole model is valid; if Finish is clicked
    // while an earlier tab still has an invalid field, this fires instead (Edge Cases: the
    // coordinator must be able to tell something needs attention, even from the Review tab).
    private void HandleInvalidSubmit()
    {
        _errorMessage = "Some required fields are incomplete — check every tab before finishing.";
    }

    private async Task HandleValidSubmitAsync()
    {
        _submitting = true;
        _errorMessage = null;

        try
        {
            var officeHolderTitles = (_model.CommitteeOfficeHolderTitlesText ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            var request = new SetupRequest(
                OrganizationName: _model.OrganizationName!,
                AnnualFee: _model.AnnualFee,
                AttendanceFee: _model.AttendanceFee,
                MembershipRenewalMonth: _model.MembershipRenewalMonth,
                IsTaxApplicable: _model.IsTaxApplicable,
                TaxRate: _model.TaxRate,
                AnnualFeeTaxCode: _model.AnnualFeeTaxCode,
                AttendanceFeeTaxCode: _model.AttendanceFeeTaxCode,
                Theme: ThemeProvider?.CurrentTheme ?? Theme.Dark,
                CommitteeRenewalMonth: _model.CommitteeRenewalMonth,
                CommitteeOfficeHolderTitles: officeHolderTitles,
                GeneralCommitteeSeatCountTarget: _model.GeneralCommitteeSeatCountTarget,
                AuditRetentionYears: _model.AuditRetentionYears);

            await SetupService.InitializeAsync(request);

            if (_seedWithTestData && _debugSeeder is not null)
            {
                _seedingInProgress = true;
                try
                {
                    var progress = new Progress<string>(msg =>
                    {
                        _seedingProgress = msg;
                        InvokeAsync(StateHasChanged);
                    });
                    await Task.Run(() => _debugSeeder.SeedAsync(progress));
                }
                finally
                {
                    _seedingInProgress = false;
                }
            }

            Nav.NavigateTo("/dashboard");
        }
        catch (Core.Exceptions.ValidationException ex)
        {
            _errorMessage = ex.Message;
        }
        catch
        {
            _errorMessage = "An unexpected error occurred. Please try again.";
        }
        finally
        {
            _submitting = false;
        }
    }
}
