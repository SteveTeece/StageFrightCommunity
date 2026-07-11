using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Finance;
using StageFright.UI.Pages.Finance;
using AppSettings = StageFright.Core.Entities.Settings;

namespace StageFright.UI.Tests.Pages.Finance;

/// <summary>
/// bUnit tests for OpeningBalancesWizard — 3-step flow (as-at date → balances grid
/// with live plug preview → confirm), rerun warning, posting, and error handling.
/// </summary>
public class OpeningBalancesWizardTests : BunitContext
{
    private readonly IOpeningBalanceService _openingService = Substitute.For<IOpeningBalanceService>();
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();

    private static readonly Guid CashAccountId = Guid.NewGuid();
    private static readonly Guid LoanAccountId = Guid.NewGuid();

    public OpeningBalancesWizardTests()
    {
        Services.AddSingleton(_openingService);
        Services.AddSingleton(_settingsService);

        _openingService.GetOpeningBalanceAccountsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Account>
            {
                new()
                {
                    Id = CashAccountId, Name = "Cash on Hand", Type = AccountType.Asset,
                    AccountNumber = "1100", IsSystem = true, IsBankAccount = true,
                    CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Id = LoanAccountId, Name = "Committee Loan", Type = AccountType.Liability,
                    AccountNumber = "2000",
                    CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
                }
            });

        _openingService.HasExistingOpeningBalancesAsync(Arg.Any<CancellationToken>())
            .Returns(false);

        _openingService.ComputePlug(Arg.Any<IReadOnlyList<OpeningBalanceEntry>>(), Arg.Any<IReadOnlyList<Account>>())
            .Returns(ci => ci.ArgAt<IReadOnlyList<OpeningBalanceEntry>>(0)
                .Where(e => e.AccountId == CashAccountId).Sum(e => e.Amount)
                - ci.ArgAt<IReadOnlyList<OpeningBalanceEntry>>(0)
                    .Where(e => e.AccountId == LoanAccountId).Sum(e => e.Amount));

        _settingsService.GetAsync(Arg.Any<CancellationToken>())
            .Returns((AppSettings?)null);
    }

    // --- Step 1 ---

    [Fact]
    public void Should_RenderStepOneWithAsAtDate_When_WizardLoads()
    {
        var cut = Render<OpeningBalancesWizard>();

        Assert.Contains("Step 1", cut.Markup);
        Assert.NotNull(cut.Find("#asAtDate"));
    }

    [Fact]
    public void Should_DefaultAsAtDateToFinancialYearStart_When_WizardLoads()
    {
        var cut = Render<OpeningBalancesWizard>();

        var expected = FinancialYearCalculator
            .GetRange(DateTime.Today, FinancialYearCalculator.DefaultStartMonth).From;

        Assert.Equal(expected.ToString("yyyy-MM-dd"), cut.Find("#asAtDate").GetAttribute("value"));
    }

    [Fact]
    public void Should_ShowRerunWarning_When_OpeningBalancesAlreadyPosted()
    {
        _openingService.HasExistingOpeningBalancesAsync(Arg.Any<CancellationToken>())
            .Returns(true);

        var cut = Render<OpeningBalancesWizard>();

        Assert.NotNull(cut.Find(".opening-balances-warning"));
    }

    [Fact]
    public void Should_NotShowRerunWarning_When_NoOpeningBalancesExist()
    {
        var cut = Render<OpeningBalancesWizard>();

        Assert.Empty(cut.FindAll(".opening-balances-warning"));
    }

    // --- Step 2 ---

    [Fact]
    public void Should_ShowAccountGrid_When_AdvancedToStepTwo()
    {
        var cut = Render<OpeningBalancesWizard>();

        cut.Find(".wizard-next").Click();

        Assert.Contains("Step 2", cut.Markup);
        Assert.Equal(2, cut.FindAll(".opening-balance-amount").Count);
        Assert.Contains("Cash on Hand", cut.Markup);
        Assert.Contains("Committee Loan", cut.Markup);
    }

    [Fact]
    public void Should_UpdatePlugPreview_When_BalanceEntered()
    {
        var cut = Render<OpeningBalancesWizard>();
        cut.Find(".wizard-next").Click();

        cut.FindAll(".opening-balance-amount")[0].Change("5000");

        Assert.Contains("5,000.00", cut.Find(".opening-balances-plug").TextContent);
    }

    [Fact]
    public void Should_DisableNext_When_NoBalancesEntered()
    {
        var cut = Render<OpeningBalancesWizard>();
        cut.Find(".wizard-next").Click();

        Assert.True(cut.Find(".wizard-next").HasAttribute("disabled"));
    }

    [Fact]
    public void Should_ReturnToStepOne_When_BackClicked()
    {
        var cut = Render<OpeningBalancesWizard>();
        cut.Find(".wizard-next").Click();

        cut.Find(".wizard-back").Click();

        Assert.Contains("Step 1", cut.Markup);
    }

    // --- Step 3 ---

    [Fact]
    public void Should_ShowConfirmationWithNonZeroRowsAndPlug_When_AdvancedToStepThree()
    {
        var cut = Render<OpeningBalancesWizard>();
        cut.Find(".wizard-next").Click();
        cut.FindAll(".opening-balance-amount")[0].Change("5000");
        cut.Find(".wizard-next").Click();

        Assert.Contains("Step 3", cut.Markup);
        Assert.Contains("Cash on Hand", cut.Markup);
        Assert.DoesNotContain("Committee Loan", cut.Markup);
        Assert.Contains("Opening Balance Equity", cut.Markup);
    }

    [Fact]
    public void Should_PostOpeningBalances_When_PostClicked()
    {
        var cut = Render<OpeningBalancesWizard>();
        cut.Find(".wizard-next").Click();
        cut.FindAll(".opening-balance-amount")[0].Change("5000");
        cut.Find(".wizard-next").Click();

        cut.Find(".wizard-post").Click();

        _openingService.Received(1).RecordOpeningBalancesAsync(
            Arg.Is<RecordOpeningBalancesRequest>(r =>
                r.Entries.Count == 1
                && r.Entries[0].AccountId == CashAccountId
                && r.Entries[0].Amount == 5000m),
            Arg.Any<CancellationToken>());
        Assert.Contains("posted successfully", cut.Markup);
    }

    [Fact]
    public void Should_ShowError_When_PostingFails()
    {
        _openingService.RecordOpeningBalancesAsync(Arg.Any<RecordOpeningBalancesRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new ValidationException(
                "At least one non-zero opening balance is required.", "JournalEntry", "RecordOpeningBalancesAsync"));

        var cut = Render<OpeningBalancesWizard>();
        cut.Find(".wizard-next").Click();
        cut.FindAll(".opening-balance-amount")[0].Change("5000");
        cut.Find(".wizard-next").Click();

        cut.Find(".wizard-post").Click();

        Assert.Contains("Failed to post opening balances", cut.Markup);
    }

    [Fact]
    public void Should_ShowErrorAlert_When_AccountLoadFails()
    {
        _openingService.GetOpeningBalanceAccountsAsync(Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<Account>>>(_ => throw new InvalidOperationException("boom"));

        var cut = Render<OpeningBalancesWizard>();

        Assert.Contains("Failed to load accounts", cut.Markup);
    }
}
