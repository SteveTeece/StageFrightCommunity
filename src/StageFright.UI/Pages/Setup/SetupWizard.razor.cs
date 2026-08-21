using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using StageFright.Core.Contracts;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;
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
    private readonly List<bool> _tabShown = new() { true, false, false, false, false, false, false };
    private int _currentTabIndex;

    // Chart of Accounts queue (US4): accounts added here are never persisted until Finish
    // (FR-013) — held in memory only, so a failed Finish leaves the queue untouched
    // (Edge Cases) simply by never being cleared outside a successful submit. The same
    // list is passed to the Opening Balances tab so its rows stay in sync (FR-020) —
    // OpeningBalanceEntryForm already reconciles its own rows against whatever Accounts
    // it's given, so adding/removing here is all that's needed for that sync.
    private readonly List<QueuedAccountRequest> _queuedAccounts = new();

    // Opening Balances queue (US2): replaced wholesale each time the tab's "Queue
    // Balances" action fires (it always reports the full current non-zero row set, not
    // one entry at a time) — held in memory only, same untouched-on-failed-Finish
    // guarantee as the account queue above.
    private readonly List<OpeningBalanceEntry> _queuedOpeningBalances = new();
    private DateTime _openingBalanceAsAtDate = DateTime.Today;

    // Committee office-holder role queue (US5): added/removed one at a time via the
    // Committee tab's +/− widget. Held in memory only, same as the other two queues.
    private readonly List<string> _queuedCommitteeTitles = new();

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

    // Chart of Accounts tab (US4) queue handlers — the tab itself owns duplicate/blank
    // validation via AddAccountForm; this just holds what's been queued so far.
    private void HandleQueueAccount(QueuedAccountRequest account)
    {
        _queuedAccounts.Add(account);
    }

    private void HandleRemoveQueuedAccount(QueuedAccountRequest account)
    {
        _queuedAccounts.Remove(account);
        // FR-020: removing a queued account also drops any balance already entered for it —
        // OpeningBalanceEntryForm's own row reconciliation drops the row visually once
        // QueuedAccounts no longer includes it, but a balance already queued via a prior
        // "Queue Balances" click lives here and needs its own cleanup.
        _queuedOpeningBalances.RemoveAll(e => e.AccountId == account.ClientId);
    }

    // Opening Balances tab (US2) queue handler — the tab's "Queue Balances" action always
    // reports its full current non-zero row set, so this replaces rather than appends.
    private void HandleQueueOpeningBalances(RecordOpeningBalancesRequest request)
    {
        _queuedOpeningBalances.Clear();
        _queuedOpeningBalances.AddRange(request.Entries);
        _openingBalanceAsAtDate = request.AsAtDate;
    }

    // Committee tab (US5) queue handlers — the tab itself owns blank/duplicate validation.
    private void HandleQueueCommitteeTitle(string title)
    {
        _queuedCommitteeTitles.Add(title);
    }

    private void HandleRemoveQueuedCommitteeTitle(string title)
    {
        _queuedCommitteeTitles.Remove(title);
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

        // FR-021: Finish requires either a real opening balance or an explicit opt-in to
        // sample data instead — checked before any orchestration starts so a rejected
        // Finish leaves every queue untouched (Edge Cases), same as an EditContext failure.
        if (_queuedOpeningBalances.Count == 0 && !_seedWithTestData)
        {
            _errorMessage = "Enter at least one opening balance, or select \"Load sample data\" on the Review tab, before finishing.";
            _submitting = false;
            return;
        }

        try
        {
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
                CommitteeOfficeHolderTitles: _queuedCommitteeTitles,
                GeneralCommitteeSeatCountTarget: _model.GeneralCommitteeSeatCountTarget,
                AuditRetentionYears: _model.AuditRetentionYears,
                QueuedAccounts: _queuedAccounts.Count > 0 ? _queuedAccounts : null,
                QueuedOpeningBalances: _queuedOpeningBalances.Count > 0 ? _queuedOpeningBalances : null,
                OpeningBalanceAsAtDate: _openingBalanceAsAtDate);

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
