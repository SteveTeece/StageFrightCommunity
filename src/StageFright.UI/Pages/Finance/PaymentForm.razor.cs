using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.Core.Contracts;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Finance;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Pages.Finance;

public partial class PaymentForm : ComponentBase
{
    [Parameter] public Guid MemberId { get; set; }

    [Inject] private IPaymentService PaymentService { get; set; } = null!;
    [Inject] private IMemberService MemberService { get; set; } = null!;
    [Inject] private IMemberBalanceService MemberBalanceService { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private IStringLocalizer<FinanceResource> L { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResource> Shared { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;

    private readonly PaymentFormModel _form = new();
    private readonly Dictionary<string, string> _errors = new();
    private bool _loading = true;
    private bool _saving;
    private bool _saved;
    private string? _memberName;
    private Guid? _savedPaymentId;
    private string? _errorMessage;
    private string? _successMessage;
    private IReadOnlyList<OutstandingFee> _outstandingFees = [];
    private OutstandingFeeSelectionGrid? _feeGrid;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var member = await MemberService.GetByIdAsync(MemberId);
            _memberName = member?.FullName;
            _outstandingFees = await MemberBalanceService.GetOutstandingFeesAsync(MemberId);
        }
        catch (Exception ex)
        {
            _errorMessage = Loc.Get<FinanceResource>("Finance_Payment_LoadError", ex.Message);
        }
        finally
        {
            _loading = false;
        }
    }

    private void OutstandingFeesSelectionChanged(decimal selectedTotal)
    {
        _form.Amount = selectedTotal;
    }

    private async Task SaveAsync()
    {
        _errors.Clear();
        _errorMessage = null;

        if (_saved && _savedPaymentId.HasValue)
        {
            await UpdateNotesAsync();
            return;
        }

        if (_form.Amount <= 0m)
        {
            _errors["Amount"] = L["Finance_Common_AmountPositiveError"];
            return;
        }

        var selectedFeeIds = _feeGrid?.GetSelectedFeeIds() ?? [];

        if (selectedFeeIds.Count == 0)
        {
            _errors["Amount"] = L["Finance_Payment_NoFeeSelectedError"];
            return;
        }

        var selectedTotal = _outstandingFees
            .Where(f => selectedFeeIds.Contains(f.FeeId))
            .Sum(f => f.RemainingAmount);

        if (_form.Amount > selectedTotal)
        {
            _errors["Amount"] = L["Finance_Payment_AmountExceedsError"];
            return;
        }

        _saving = true;
        try
        {
            var request = new RecordPaymentRequest
            {
                MemberId = MemberId,
                Date = _form.Date,
                Amount = _form.Amount,
                PaymentMethod = _form.PaymentMethod,
                PaymentType = _form.PaymentType,
                Notes = string.IsNullOrWhiteSpace(_form.Notes) ? null : _form.Notes.Trim(),
                SelectedFeeIds = selectedFeeIds
            };

            var payment = await PaymentService.RecordAsync(request);
            _savedPaymentId = payment.Id;
            _saved = true;
            _successMessage = Loc.Get<FinanceResource>("Finance_Payment_SuccessMessage",
                MoneyFormatter.Format(request.Amount));
        }
        catch (Exception ex)
        {
            _errorMessage = Loc.Get<FinanceResource>("Finance_Payment_RecordError", ex.Message);
        }
        finally
        {
            _saving = false;
        }
    }

    private async Task UpdateNotesAsync()
    {
        if (!_savedPaymentId.HasValue) return;

        _saving = true;
        try
        {
            var notes = string.IsNullOrWhiteSpace(_form.Notes) ? null : _form.Notes.Trim();
            await PaymentService.UpdateNotesAsync(_savedPaymentId.Value, notes);
            _successMessage = L["Finance_Payment_NotesUpdated"];
        }
        catch (Exception ex)
        {
            _errorMessage = Loc.Get<FinanceResource>("Finance_Payment_UpdateNotesError", ex.Message);
        }
        finally
        {
            _saving = false;
        }
    }

    private void Cancel() => Nav.NavigateTo("/finance?tab=outstanding");
}
