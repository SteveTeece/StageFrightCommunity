using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.Core.Contracts;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Finance;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Shared;

public partial class ReactivationForgivenessDialog : ComponentBase
{
    [Parameter] public Guid MemberId { get; set; }
    [Parameter] public bool IsVisible { get; set; }
    [Parameter] public EventCallback<bool> IsVisibleChanged { get; set; }
    [Parameter] public EventCallback OnForgivenessApplied { get; set; }

    [Inject] private IReactivationForgivenessService ForgivenessService { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResource> Shared { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;

    private string ApplyingText => Shared["Shared_Forgiveness_ApplyingText"];

    /// <summary>"{date} — {amount}" label for one selectable fee row.</summary>
    private string FeeRowText(ForgivenessItem item) =>
        Loc.Get<SharedResource>("Shared_Forgiveness_FeeRow",
            item.FeeDate.ToString("d MMM yyyy"), MoneyFormatter.Format(item.Amount));

    /// <summary>"Forgive N fee(s)" submit button label, pluralised on the selection count.</summary>
    private string ForgiveButtonText() =>
        Loc.Plural<SharedResource>("Shared_Forgiveness_ForgiveButton", _selected.Count);

    private List<ForgivenessItem> _items = new();
    private HashSet<Guid> _selected = new();
    private bool _loading = true;
    private bool _applying;
    private string? _errorMessage;
    private string? _applyError;

    protected override async Task OnParametersSetAsync()
    {
        if (!IsVisible)
            return;

        if (_items.Count > 0)
            return;

        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        _errorMessage = null;
        try
        {
            var items = await ForgivenessService.GetForgivenessItemsAsync(MemberId);
            _items = items.ToList();

            _selected = _items
                .Where(i => i.IsDefaultForgiven)
                .Select(i => i.FeeId)
                .ToHashSet();
        }
        catch (Exception ex)
        {
            _errorMessage = Loc.Get<SharedResource>("Shared_Forgiveness_LoadError", ex.Message);
        }
        finally
        {
            _loading = false;
        }
    }

    private void OnCheckedChange(Guid feeId, bool isChecked)
    {
        if (isChecked)
            _selected.Add(feeId);
        else
            _selected.Remove(feeId);
    }

    private async Task ApplyAsync()
    {
        _applying = true;
        _applyError = null;
        try
        {
            await ForgivenessService.ApplyForgivenessAsync(MemberId, _selected.ToList());
            await OnForgivenessApplied.InvokeAsync();
            await CloseAsync();
        }
        catch (Exception ex)
        {
            _applyError = Loc.Get<SharedResource>("Shared_Forgiveness_ApplyError", ex.Message);
        }
        finally
        {
            _applying = false;
        }
    }

    private void Close() => _ = CloseAsync();

    private async Task CloseAsync()
    {
        _items.Clear();
        _selected.Clear();
        _loading = true;
        _errorMessage = null;
        _applyError = null;
        await IsVisibleChanged.InvokeAsync(false);
    }
}
