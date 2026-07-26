using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;
using StageFright.UI.Pages.Finance;

namespace StageFright.UI.Tests.Pages.Finance;

/// <summary>
/// bUnit tests for PaymentForm:
/// - Method defaults to Cash
/// - Date, Amount, Method, Type fields are disabled after save (immutability)
/// - Notes field remains editable after save
/// - Amount validation enforces > 0
/// - Outstanding-fees grid renders, drives Amount auto-calc, and gates Save (FR-006/FR-007/FR-013)
/// - Submit calls PaymentService.RecordAsync
/// </summary>
public class PaymentFormTests : BunitContext
{
    private readonly IPaymentService _paymentService = Substitute.For<IPaymentService>();
    private readonly IMemberService _memberService = Substitute.For<IMemberService>();
    private readonly IMemberBalanceService _memberBalanceService = Substitute.For<IMemberBalanceService>();
    private static readonly Guid MemberId = Guid.NewGuid();
    private static readonly Guid PaymentId = Guid.NewGuid();
    private static readonly Guid FeeId = Guid.NewGuid();

    public PaymentFormTests()
    {
        Services.AddSingleton(_paymentService);
        Services.AddSingleton(_memberService);
        Services.AddSingleton(_memberBalanceService);

        _memberService.GetByIdAsync(MemberId, Arg.Any<CancellationToken>())
            .Returns(MakeMember(MemberId));

        _memberBalanceService.GetOutstandingFeesAsync(MemberId, Arg.Any<CancellationToken>())
            .Returns(new List<OutstandingFee> { MakeOutstandingFee(FeeId, 50m) });

        _paymentService.RecordAsync(Arg.Any<RecordPaymentRequest>(), Arg.Any<CancellationToken>())
            .Returns(MakePayment(MemberId, PaymentId, 50m));
    }

    // --- Default field values ---

    [Fact]
    public void Renders_PaymentMethodSelect_DefaultingToCash()
    {
        var cut = RenderWithMemberId();

        var select = cut.Find("#method");
        // The selected option should be Cash
        Assert.Equal(PaymentMethod.Cash.ToString(), select.GetAttribute("value"));
    }

    [Fact]
    public void Renders_AmountInput_Present()
    {
        var cut = RenderWithMemberId();

        cut.Find("#amount");
    }

    [Fact]
    public void Renders_DateInput_Present()
    {
        var cut = RenderWithMemberId();

        cut.Find("#payDate");
    }

    [Fact]
    public void Renders_NotesTextarea_Present()
    {
        var cut = RenderWithMemberId();

        cut.Find("#notes");
    }

    // --- Outstanding-fees grid (FR-001, FR-004, FR-013) ---

    [Fact]
    public void Renders_OutstandingFeesGrid_ForMemberWithOutstandingFees()
    {
        var cut = RenderWithMemberId();

        cut.Find($"#fee-{FeeId}");
    }

    [Fact]
    public void SelectingFee_UpdatesAmountField_ToRemainingAmount()
    {
        var cut = RenderWithMemberId();

        cut.Find($"#fee-{FeeId}").Change(true);

        var amountInput = cut.Find("#amount");
        Assert.Equal(50m, decimal.Parse(amountInput.GetAttribute("value")!));
    }

    [Fact]
    public void MemberWithZeroOutstandingFees_RendersEmptyStateAndDisablesSave()
    {
        var emptyMemberId = Guid.NewGuid();
        _memberService.GetByIdAsync(emptyMemberId, Arg.Any<CancellationToken>())
            .Returns(MakeMember(emptyMemberId));
        _memberBalanceService.GetOutstandingFeesAsync(emptyMemberId, Arg.Any<CancellationToken>())
            .Returns(new List<OutstandingFee>());

        var cut = Render<PaymentForm>(p => p.Add(x => x.MemberId, emptyMemberId));

        Assert.Contains("no outstanding fees", cut.Markup, StringComparison.OrdinalIgnoreCase);
        var saveButton = cut.Find("button[type=submit]");
        Assert.True(saveButton.HasAttribute("disabled"), "Save should be disabled for a member with zero outstanding fees");
    }

    // --- Immutability after save ---

    [Fact]
    public async Task AfterSave_DateField_IsDisabled()
    {
        var cut = RenderWithMemberId();

        await SubmitValidPayment(cut);

        var dateInput = cut.Find("#payDate");
        Assert.True(dateInput.HasAttribute("disabled"), "Date should be disabled after save");
    }

    [Fact]
    public async Task AfterSave_AmountField_IsDisabled()
    {
        var cut = RenderWithMemberId();

        await SubmitValidPayment(cut);

        var amountInput = cut.Find("#amount");
        Assert.True(amountInput.HasAttribute("disabled"), "Amount should be disabled after save");
    }

    [Fact]
    public async Task AfterSave_MethodField_IsDisabled()
    {
        var cut = RenderWithMemberId();

        await SubmitValidPayment(cut);

        var methodSelect = cut.Find("#method");
        Assert.True(methodSelect.HasAttribute("disabled"), "Method should be disabled after save");
    }

    [Fact]
    public async Task AfterSave_TypeField_IsDisabled()
    {
        var cut = RenderWithMemberId();

        await SubmitValidPayment(cut);

        var typeSelect = cut.Find("#type");
        Assert.True(typeSelect.HasAttribute("disabled"), "Type should be disabled after save");
    }

    [Fact]
    public async Task AfterSave_NotesField_IsStillEditable()
    {
        var cut = RenderWithMemberId();

        await SubmitValidPayment(cut);

        var notesInput = cut.Find("#notes");
        Assert.False(notesInput.HasAttribute("disabled"), "Notes should remain editable after save");
    }

    [Fact]
    public async Task AfterSave_OutstandingFeesGridCheckboxes_BecomeDisabled()
    {
        var cut = RenderWithMemberId();

        await SubmitValidPayment(cut);

        var feeCheckbox = cut.Find($"#fee-{FeeId}");
        Assert.True(feeCheckbox.HasAttribute("disabled"), "Fee checkboxes should be disabled after save (FR-010)");
    }

    // --- Validation ---

    [Fact]
    public async Task Submit_WithZeroAmount_ShowsValidationError()
    {
        var cut = RenderWithMemberId();

        // Leave amount at default 0 and submit
        await cut.Find("form").SubmitAsync();

        Assert.Contains("must be greater than zero", cut.Markup, StringComparison.OrdinalIgnoreCase);
        await _paymentService.DidNotReceive().RecordAsync(
            Arg.Any<RecordPaymentRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_WithPositiveAmount_ButNoFeeChecked_ShowsValidationError()
    {
        var cut = RenderWithMemberId();

        var amountInput = cut.Find("#amount");
        await amountInput.ChangeAsync(new ChangeEventArgs { Value = "50" });

        await cut.Find("form").SubmitAsync();

        Assert.Contains("at least one outstanding fee must be selected", cut.Markup, StringComparison.OrdinalIgnoreCase);
        await _paymentService.DidNotReceive().RecordAsync(
            Arg.Any<RecordPaymentRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_WithAmountExceedingCheckedTotal_ShowsValidationError()
    {
        var cut = RenderWithMemberId();

        // Check the fee (auto-sets Amount to 50), then manually overwrite Amount above the checked total.
        cut.Find($"#fee-{FeeId}").Change(true);
        var amountInput = cut.Find("#amount");
        await amountInput.ChangeAsync(new ChangeEventArgs { Value = "80" });

        await cut.Find("form").SubmitAsync();

        Assert.Contains("exceeds the selected fees", cut.Markup, StringComparison.OrdinalIgnoreCase);
        await _paymentService.DidNotReceive().RecordAsync(
            Arg.Any<RecordPaymentRequest>(), Arg.Any<CancellationToken>());
    }

    // --- Service call ---

    [Fact]
    public async Task Submit_WithValidAmount_CallsPaymentService()
    {
        var cut = RenderWithMemberId();

        await SubmitValidPayment(cut);

        await _paymentService.Received(1).RecordAsync(
            Arg.Is<RecordPaymentRequest>(r => r.MemberId == MemberId && r.Amount == 50m
                && r.SelectedFeeIds != null && r.SelectedFeeIds.Contains(FeeId)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_WithValidAmount_ShowsSuccessMessage()
    {
        var cut = RenderWithMemberId();

        await SubmitValidPayment(cut);

        Assert.Contains("recorded successfully", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    // --- Helpers ---

    private IRenderedComponent<PaymentForm> RenderWithMemberId()
    {
        return Render<PaymentForm>(p => p.Add(x => x.MemberId, MemberId));
    }

    private static async Task SubmitValidPayment(IRenderedComponent<PaymentForm> cut)
    {
        // Checking the outstanding fee auto-fills Amount via SelectionChanged (FR-004).
        cut.Find($"#fee-{FeeId}").Change(true);

        await cut.Find("form").SubmitAsync();
    }

    private static Member MakeMember(Guid id) => new()
    {
        Id = id, FirstName = "Test", LastName = "Member", StreetAddress = "1 Test St",
        Status = MemberStatus.Active,
        JoinDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        ActivateDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static Payment MakePayment(Guid memberId, Guid paymentId, decimal amount) => new()
    {
        Id = paymentId, MemberId = memberId,
        Date = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
        Amount = amount, PaymentMethod = PaymentMethod.Cash, PaymentType = PaymentType.Annual,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static OutstandingFee MakeOutstandingFee(Guid feeId, decimal remainingAmount) => new()
    {
        FeeId = feeId, FeeType = FeeType.Annual,
        FeeDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        DueDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
        RemainingAmount = remainingAmount
    };
}
