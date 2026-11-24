using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;
using StageFright.UI.Pages.Finance;
using AppSettings = StageFright.Core.Entities.Settings;

namespace StageFright.UI.Tests.Pages.Finance;

/// <summary>
/// bUnit tests for ExpensePaymentPage's organisation picker (spec 032 US3, FR-010): it lists only
/// active organisations, choosing one fills an empty payee, a typed payee is never overwritten, the
/// filled payee stays editable and stays put when the choice is cleared, and the chosen id (or none)
/// reaches the RecordExpenseRequest.
/// </summary>
public class ExpensePaymentPageTests : LocalizedTestContext
{
    private readonly IExpensePaymentService _expenseService = Substitute.For<IExpensePaymentService>();
    private readonly IAccountService _accountService = Substitute.For<IAccountService>();
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();
    private readonly IExternalOrganisationService _organisationService = Substitute.For<IExternalOrganisationService>();

    private static readonly Guid TownHallId = Guid.NewGuid();
    private static readonly Guid AcmeId = Guid.NewGuid();

    public ExpensePaymentPageTests()
    {
        Services.AddSingleton(_expenseService);
        Services.AddSingleton(_accountService);
        Services.AddSingleton(_settingsService);
        Services.AddSingleton(_organisationService);

        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(new AppSettings
        {
            Id = Guid.NewGuid(), OrganizationName = "Test Choir",
            AnnualFee = 50m, AttendanceFee = 10m,
            MembershipRenewalMonth = 1, MaxAgeRangeYears = 150,
            MinimumMemberAge = 0, SchemaVersion = "1.1.0",
            IsTaxApplicable = false,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });

        // One expense and one bank account, so both are pre-selected and the form can submit.
        _expenseService.GetExpenseAccountsAsync(Arg.Any<CancellationToken>()).Returns(new List<Account>
        {
            MakeAccount(Guid.NewGuid(), "Hall Hire", AccountType.Expense, "6000")
        });
        _accountService.GetBankAccountsAsync(Arg.Any<CancellationToken>()).Returns(new List<Account>
        {
            MakeAccount(Guid.NewGuid(), "Operating Account", AccountType.Asset, "1110", isBank: true)
        });

        _organisationService.GetActiveAsync(Arg.Any<CancellationToken>()).Returns(new List<ExternalOrganisation>
        {
            MakeOrganisation(AcmeId, "Acme Supplies"),
            MakeOrganisation(TownHallId, "Town Hall")
        });
    }

    private IRenderedComponent<ExpensePaymentPage> RenderPage() => Render<ExpensePaymentPage>();

    private static string? PayeeValue(IRenderedComponent<ExpensePaymentPage> cut) =>
        cut.Find("#payee").GetAttribute("value");

    private static async Task SubmitAsync(IRenderedComponent<ExpensePaymentPage> cut)
    {
        await cut.Find("#expenseAmount").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "500" });
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
    }

    [Fact]
    public void Should_ListOnlyActiveOrganisationsAfterNoneOption_When_PageLoads()
    {
        var cut = RenderPage();

        var options = cut.FindAll("#expenseOrganisation option");
        Assert.Equal(3, options.Count);
        Assert.Equal(string.Empty, options[0].GetAttribute("value"));
        Assert.Equal(new[] { "Acme Supplies", "Town Hall" }, options.Skip(1).Select(o => o.TextContent.Trim()));
        _organisationService.DidNotReceiveWithAnyArgs().GetArchivedAsync(Xunit.TestContext.Current.CancellationToken);
    }

    [Fact]
    public void Should_FillEmptyPayeeWithOrganisationName_When_OrganisationChosen()
    {
        var cut = RenderPage();

        cut.Find("#expenseOrganisation").Change(TownHallId.ToString());

        Assert.Equal("Town Hall", PayeeValue(cut));
    }

    [Fact]
    public void Should_KeepTypedPayee_When_OrganisationChosenAfterwards()
    {
        var cut = RenderPage();

        cut.Find("#payee").Change("Town Hall Café");
        cut.Find("#expenseOrganisation").Change(TownHallId.ToString());

        Assert.Equal("Town Hall Café", PayeeValue(cut));
    }

    [Fact]
    public void Should_KeepFilledPayeeEditableAndInPlace_When_ChoiceClearedOrChanged()
    {
        var cut = RenderPage();

        cut.Find("#expenseOrganisation").Change(TownHallId.ToString());
        cut.Find("#expenseOrganisation").Change(string.Empty);
        Assert.Equal("Town Hall", PayeeValue(cut));

        cut.Find("#expenseOrganisation").Change(AcmeId.ToString());
        Assert.Equal("Town Hall", PayeeValue(cut));

        Assert.False(cut.Find("#payee").HasAttribute("readonly"));
        Assert.False(cut.Find("#payee").HasAttribute("disabled"));
        cut.Find("#payee").Change("Town Hall Annex");
        Assert.Equal("Town Hall Annex", PayeeValue(cut));
    }

    [Fact]
    public async Task Should_SendChosenOrganisationIdAndPayee_When_Submitted()
    {
        var cut = RenderPage();

        cut.Find("#expenseOrganisation").Change(TownHallId.ToString());
        await SubmitAsync(cut);

        await _expenseService.Received(1).RecordExpenseAsync(
            Arg.Is<RecordExpenseRequest>(r => r!.ExternalOrganisationId == TownHallId && r.Payee == "Town Hall"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_SendNullOrganisation_When_NoneChosen()
    {
        var cut = RenderPage();

        await SubmitAsync(cut);

        await _expenseService.Received(1).RecordExpenseAsync(
            Arg.Is<RecordExpenseRequest>(r => r!.ExternalOrganisationId == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_ResetOrganisationChoice_When_RecordAnotherClicked()
    {
        var cut = RenderPage();
        cut.Find("#expenseOrganisation").Change(TownHallId.ToString());
        await SubmitAsync(cut);

        await cut.Find("button.btn-outline-success").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.Equal(string.Empty, cut.Find("#expenseOrganisation").GetAttribute("value") ?? string.Empty);
        Assert.True(string.IsNullOrEmpty(PayeeValue(cut)));
    }

    // --- Helpers ---

    private static Account MakeAccount(Guid id, string name, AccountType type, string number, bool isBank = false) => new()
    {
        Id = id, Name = name, Type = type, AccountNumber = number, IsBankAccount = isBank,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static ExternalOrganisation MakeOrganisation(Guid id, string name) => new()
    {
        Id = id, Name = name, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };
}
