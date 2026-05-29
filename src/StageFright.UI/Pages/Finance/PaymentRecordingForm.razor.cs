using Microsoft.AspNetCore.Components;
using StageFright.Core.Services;
using StageFright.Core.Entities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StageFright.UI.Pages.Finance;

public partial class PaymentRecordingForm
{
    [Inject]
    private IFinanceService FinanceService { get; set; } = null!;

    [Inject]
    private IMemberService MemberService { get; set; } = null!;

    [Parameter]
    public EventCallback OnPaymentRecorded { get; set; }

    [Parameter]
    public EventCallback OnCancel { get; set; }

    private string? ErrorMessage;
    private string? SuccessMessage;
    private string PaymentDateString = DateTime.Today.ToString("yyyy-MM-dd");

    private PaymentFormModel FormModel = new();
    private List<Member> AvailableMembers = new();
    private List<dynamic> AvailableCategories = new();

    protected override async Task OnInitializedAsync()
    {
        try
        {
            // Load available members
            AvailableMembers = (await MemberService.GetActiveMembersAsync()).ToList();

            // Load available categories
            AvailableCategories = (await FinanceService.GetCategoriesAsync()).ToList();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading form data: {ex.Message}";
        }
    }

    private async Task SavePayment()
    {
        try
        {
            ErrorMessage = null;
            SuccessMessage = null;

            // Validate inputs
            if (string.IsNullOrWhiteSpace(FormModel.Amount.ToString()))
                throw new Exception("Amount is required.");

            if (string.IsNullOrWhiteSpace(FormModel.PaymentMethod))
                throw new Exception("Payment method is required.");

            if (string.IsNullOrWhiteSpace(FormModel.PaymentType))
                throw new Exception("Payment type is required.");

            if (FormModel.MemberId == Guid.Empty)
                throw new Exception("Member is required.");

            if (string.IsNullOrWhiteSpace(FormModel.Category))
                throw new Exception("Category is required.");

            if (!DateTime.TryParse(PaymentDateString, out var paymentDate))
                throw new Exception("Invalid payment date.");

            // Record payment
            var paymentId = await FinanceService.RecordPaymentAsync(
                paymentDate,
                FormModel.Amount,
                FormModel.PaymentMethod,
                FormModel.PaymentType,
                FormModel.MemberId,
                FormModel.Category,
                FormModel.Notes);

            SuccessMessage = "Payment recorded successfully.";
            FormModel = new();
            PaymentDateString = DateTime.Today.ToString("yyyy-MM-dd");

            // Notify parent component
            await OnPaymentRecorded.InvokeAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private async Task Cancel()
    {
        FormModel = new();
        PaymentDateString = DateTime.Today.ToString("yyyy-MM-dd");
        ErrorMessage = null;
        SuccessMessage = null;
        await OnCancel.InvokeAsync();
    }

    private class PaymentFormModel
    {
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string PaymentType { get; set; } = string.Empty;
        public Guid MemberId { get; set; }
        public string Category { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }
}
