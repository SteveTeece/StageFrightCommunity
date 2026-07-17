using Microsoft.AspNetCore.Components;
using StageFright.Core.Contracts;
using StageFright.Core.Modules.Finance;

namespace StageFright.UI.Pages.Finance;

public partial class PaymentForm : ComponentBase
{
    [Parameter] public Guid MemberId { get; set; }

    [Inject] private IPaymentService PaymentService { get; set; } = null!;
    [Inject] private IMemberService MemberService { get; set; } = null!;
    [Inject] private IMemberBalanceService MemberBalanceService { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;

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
            _memberName = member?.Name;
            _outstandingFees = await MemberBalanceService.GetOutstandingFeesAsync(MemberId);
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to load member: {ex.Message}";
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
            _errors["Amount"] = "Amount must be greater than zero.";
            return;
        }

        var selectedFeeIds = _feeGrid?.GetSelectedFeeIds() ?? [];

        if (selectedFeeIds.Count == 0)
        {
            _errors["Amount"] = "At least one outstanding fee must be selected.";
            return;
        }

        var selectedTotal = _outstandingFees
            .Where(f => selectedFeeIds.Contains(f.FeeId))
            .Sum(f => f.RemainingAmount);

        if (_form.Amount > selectedTotal)
        {
            _errors["Amount"] = "Amount exceeds the selected fees' remaining total.";
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
            _successMessage = $"Payment of {request.Amount:C} recorded successfully.";
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to record payment: {ex.Message}";
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
            _successMessage = "Notes updated.";
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to update notes: {ex.Message}";
        }
        finally
        {
            _saving = false;
        }
    }

    private void Cancel() => Nav.NavigateTo("/finance?tab=outstanding");
}
