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
/// - Submit calls PaymentService.RecordAsync
/// </summary>
public class PaymentFormTests : BunitContext
{
    private readonly IPaymentService _paymentService = Substitute.For<IPaymentService>();
    private readonly IMemberService _memberService = Substitute.For<IMemberService>();
    private static readonly Guid MemberId = Guid.NewGuid();
    private static readonly Guid PaymentId = Guid.NewGuid();

    public PaymentFormTests()
    {
        Services.AddSingleton(_paymentService);
        Services.AddSingleton(_memberService);

        _memberService.GetByIdAsync(MemberId, Arg.Any<CancellationToken>())
            .Returns(MakeMember(MemberId));

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

    // --- Service call ---

    [Fact]
    public async Task Submit_WithValidAmount_CallsPaymentService()
    {
        var cut = RenderWithMemberId();

        await SubmitValidPayment(cut);

        await _paymentService.Received(1).RecordAsync(
            Arg.Is<RecordPaymentRequest>(r => r.MemberId == MemberId && r.Amount == 50m),
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
        // Set a valid amount
        var amountInput = cut.Find("#amount");
        await amountInput.ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "50" });

        await cut.Find("form").SubmitAsync();
    }

    private static Member MakeMember(Guid id) => new()
    {
        Id = id, Name = "Test Member", StreetAddress = "1 Test St",
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
}
