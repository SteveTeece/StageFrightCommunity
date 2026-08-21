using Microsoft.AspNetCore.Components;
using StageFright.Core.Contracts;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Settings;
using StageFright.UI.Shared;

namespace StageFright.UI.Pages.Setup.Tabs;

/// <summary>Chart of Accounts tab (US4) — hosts the shared <see cref="AddAccountForm"/> in
/// queuing mode (its OnSubmit appends to <see cref="QueuedAccounts"/> instead of calling
/// IAccountService.CreateAsync directly) plus a BorderedListBox of what's queued so far.
/// Nothing here is created until Finish (FR-013); the queue itself lives in
/// SetupWizard.razor.cs since the Opening Balances tab and Review tab also need it.</summary>
public partial class ChartOfAccountsTab : ComponentBase
{
    [Inject] private IAccountService AccountService { get; set; } = null!;

    [Parameter, EditorRequired] public IReadOnlyList<QueuedAccountRequest> QueuedAccounts { get; set; } = Array.Empty<QueuedAccountRequest>();
    [Parameter, EditorRequired] public EventCallback<QueuedAccountRequest> OnAdd { get; set; }
    [Parameter, EditorRequired] public EventCallback<QueuedAccountRequest> OnRemove { get; set; }

    private IReadOnlyList<string> _existingAccountNames = Array.Empty<string>();

    /// <summary>Duplicate-check set for AddAccountForm: real accounts (active + archived,
    /// matching ChartOfAccountsPage's own comparison set) union names already queued this
    /// setup session (FR-014).</summary>
    private IReadOnlyList<string> ExistingNames =>
        _existingAccountNames.Concat(QueuedAccounts.Select(a => a.Name)).ToList();

    protected override async Task OnInitializedAsync()
    {
        var active = await AccountService.GetAllAsync();
        var archived = await AccountService.GetArchivedAsync();
        _existingAccountNames = active.Concat(archived).Select(a => a.Name).ToList();
    }

    private async Task HandleAddAsync(NewAccountModel model)
    {
        var queued = new QueuedAccountRequest(
            ClientId: Guid.NewGuid(),
            Name: model.Name!,
            Type: model.Type,
            IsBankAccount: model.Type == AccountType.Asset && model.IsBankAccount);

        await OnAdd.InvokeAsync(queued);
    }

    private async Task HandleRemoveAsync(QueuedAccountRequest account)
    {
        await OnRemove.InvokeAsync(account);
    }
}
