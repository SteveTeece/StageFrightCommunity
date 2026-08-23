using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Finance;
using StageFright.Reports.Models;
using StageFright.Reports.Registry;
using StageFright.Reports.Rendering;
using StageFright.UI.Pages.Finance;
using SettingsEntity = StageFright.Core.Entities.Settings;

namespace StageFright.UI.Tests.Pages.Finance;

/// <summary>
/// bUnit tests for ChartOfAccountsPage — renders accounts across all five types with
/// filter, add form (bank flag only for Asset), rename, archive blocking with error
/// message, restore, system account read-only enforcement, and the Balance column
/// (currency rendering, sorting, and per-row error indicator).
/// </summary>
public class ChartOfAccountsPageTests : RadzenGridTestContext
{
    private readonly IAccountService _accountService = Substitute.For<IAccountService>();
    private readonly IAccountBalanceService _accountBalanceService = Substitute.For<IAccountBalanceService>();
    private readonly IReportProviderRegistry _reportProviderRegistry = Substitute.For<IReportProviderRegistry>();
    private readonly IPdfReportRenderer _pdfRenderer = Substitute.For<IPdfReportRenderer>();
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();

    private static readonly Guid IncomeAccountId = Guid.NewGuid();
    private static readonly Guid BankAccountId = Guid.NewGuid();
    private static readonly Guid SystemAccountId = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid ArchivedAccountId = Guid.NewGuid();
    private static readonly Guid ErrorAccountId = Guid.NewGuid();

    public ChartOfAccountsPageTests()
    {
        Services.AddSingleton(_accountService);
        Services.AddSingleton(_accountBalanceService);
        Services.AddSingleton(_reportProviderRegistry);
        Services.AddSingleton(_pdfRenderer);
        Services.AddSingleton(_settingsService);

        _settingsService.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new SettingsEntity
            {
                Id = Guid.NewGuid(), OrganizationName = "Test Choir",
                AnnualFee = 50m, AttendanceFee = 10m,
                MembershipRenewalMonth = 1, MaxAgeRangeYears = 150,
                MinimumMemberAge = 0, SchemaVersion = "1.0.0",
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });

        _accountService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(DefaultActiveAccounts());

        _accountService.GetArchivedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Account>());

        _accountBalanceService.GetActiveAccountBalancesAsync(Arg.Any<CancellationToken>())
            .Returns(DefaultActiveAccountBalances());

        _accountBalanceService.GetArchivedAccountBalancesAsync(Arg.Any<CancellationToken>())
            .Returns(new List<AccountBalance>());

        _accountService.CreateAsync(Arg.Any<string>(), Arg.Any<AccountType>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(ci => new Account
            {
                Id = Guid.NewGuid(),
                Name = ci.ArgAt<string>(0),
                Type = ci.ArgAt<AccountType>(1),
                AccountNumber = ci.ArgAt<AccountType>(1) == AccountType.Income ? "4001" : "6001",
                IsBankAccount = ci.ArgAt<bool>(2),
                IsSystem = false,
                SortOrder = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
    }

    // --- Rendering ---

    [Fact]
    public void Should_RenderAllActiveAccounts_When_NoFilterApplied()
    {
        var cut = Render<ChartOfAccountsPage>();

        Assert.Contains("Membership Fees", cut.Markup);
        Assert.Contains("Operating Account", cut.Markup);
        Assert.Contains("Cash on Hand", cut.Markup);
    }

    [Fact]
    public void Should_RenderAccountNumbers_When_PageLoads()
    {
        var cut = Render<ChartOfAccountsPage>();

        Assert.Contains("4000", cut.Markup);
        Assert.Contains("1110", cut.Markup);
        Assert.Contains("1100", cut.Markup);
    }

    [Fact]
    public void Should_ShowSystemBadgeAndReadOnly_When_AccountIsSystem()
    {
        var cut = Render<ChartOfAccountsPage>();

        Assert.Contains("System", cut.Markup);
        Assert.Contains("Read-only", cut.Markup);
    }

    [Fact]
    public void Should_ShowBankBadge_When_AccountIsBank()
    {
        var cut = Render<ChartOfAccountsPage>();

        Assert.Contains(">Bank<", cut.Markup);
    }

    [Fact]
    public void Should_FilterByType_When_TypeFilterSelected()
    {
        var cut = Render<ChartOfAccountsPage>();

        cut.Find("#type-filter").Change(AccountType.Income.ToString());

        Assert.Contains("Membership Fees", cut.Markup);
        Assert.DoesNotContain("Operating Account", cut.Markup);
    }

    [Fact]
    public void Should_ShowEmptyMessage_When_FilterMatchesNothing()
    {
        var cut = Render<ChartOfAccountsPage>();

        cut.Find("#type-filter").Change(AccountType.Equity.ToString());

        Assert.Contains("No accounts match the current filter.", cut.Markup);
    }

    [Fact]
    public void Should_ShowErrorAlert_When_LoadFails()
    {
        _accountBalanceService.GetActiveAccountBalancesAsync(Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<AccountBalance>>>(_ => throw new InvalidOperationException("boom"));

        var cut = Render<ChartOfAccountsPage>();

        Assert.Contains("Failed to load accounts", cut.Markup);
    }

    // --- Balance column ---

    [Fact]
    public void Should_RenderCurrencyFormattedBalance_When_PageLoads()
    {
        var cut = Render<ChartOfAccountsPage>();

        Assert.Contains("$500.00", cut.Markup);   // Cash on Hand
        Assert.Contains("$1,000.00", cut.Markup); // Operating Account
        Assert.Contains("$250.00", cut.Markup);   // Membership Fees
    }

    [Fact]
    public void Should_ShowErrorIndicator_When_AccountBalanceHasError()
    {
        var balances = DefaultActiveAccountBalances().ToList();
        balances.Add(new AccountBalance
        {
            AccountId = ErrorAccountId,
            AccountNumber = "6001",
            Name = "Broken Account",
            Type = AccountType.Expense,
            IsSystem = false,
            IsBankAccount = false,
            Balance = null,
            HasError = true
        });
        _accountBalanceService.GetActiveAccountBalancesAsync(Arg.Any<CancellationToken>())
            .Returns(balances);
        _accountService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(DefaultActiveAccounts().Concat(
            [
                new Account
                {
                    Id = ErrorAccountId, Name = "Broken Account", Type = AccountType.Expense,
                    AccountNumber = "6001", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
                }
            ]).ToList());

        var cut = Render<ChartOfAccountsPage>();

        Assert.Contains("Broken Account", cut.Markup);
        Assert.Contains("—", cut.Markup);
        Assert.Contains("Balance could not be calculated for this account", cut.Markup);
        // Other rows are unaffected — their balances still render.
        Assert.Contains("$500.00", cut.Markup);
    }

    [Fact]
    public void Should_ShowBalanceForArchivedAccounts_When_ArchivedListLoads()
    {
        _accountBalanceService.GetArchivedAccountBalancesAsync(Arg.Any<CancellationToken>())
            .Returns(new List<AccountBalance>
            {
                new()
                {
                    AccountId = ArchivedAccountId, AccountNumber = "4009", Name = "Old Fund",
                    Type = AccountType.Income, Balance = 42m, HasError = false
                }
            });
        _accountService.GetArchivedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Account>
            {
                new()
                {
                    Id = ArchivedAccountId, Name = "Old Fund", Type = AccountType.Income,
                    AccountNumber = "4009", IsDeleted = true,
                    CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
                }
            });

        var cut = Render<ChartOfAccountsPage>();

        Assert.Contains("Old Fund", cut.Markup);
        Assert.Contains("$42.00", cut.Markup);
    }

    [Fact]
    public void Should_SortByBalance_When_BalanceColumnHeaderClicked()
    {
        var cut = Render<ChartOfAccountsPage>();

        var header = cut.FindAll("th").First(th => th.TextContent.Contains("Balance"));
        header.QuerySelector("div")!.Click();

        var cells = cut.FindAll("code");
        // Before sort, rows appear in the order the service returned them: 1100, 1110, 4000
        // (Balances 500, 1000, 250). After ascending sort by Balance (250, 500, 1000) the
        // account-number column should reorder to 4000, 1100, 1110.
        Assert.Equal("4000", cells[0].TextContent.Trim());
        Assert.Equal("1100", cells[1].TextContent.Trim());
        Assert.Equal("1110", cells[2].TextContent.Trim());
    }

    // --- Print Chart of Accounts ---

    [Fact]
    public void PrintButton_Renders_WithVerbatimLabel()
    {
        var cut = Render<ChartOfAccountsPage>();

        cut.FindAll("button").First(b => b.TextContent.Trim() == "Print Chart of Accounts");
    }

    [Fact]
    public async Task ClickPrint_ProviderNotFound_ShowsErrorAlert_AndDoesNotRenderPdf()
    {
        _reportProviderRegistry.GetProvider("chart-of-accounts").Returns((IReportProvider?)null);

        var cut = Render<ChartOfAccountsPage>();
        await cut.FindAll("button").First(b => b.TextContent.Trim() == "Print Chart of Accounts")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.Contains("alert-danger", cut.Markup);
        _pdfRenderer.DidNotReceive().Render(Arg.Any<ReportData>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ClickPrint_ProviderThrows_ShowsErrorAlert_AndDoesNotRenderPdf()
    {
        var provider = Substitute.For<IReportProvider>();
        provider.GenerateAsync(Arg.Any<ReportFilterValues>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ReportData>(new InvalidOperationException("boom")));
        _reportProviderRegistry.GetProvider("chart-of-accounts").Returns(provider);

        var cut = Render<ChartOfAccountsPage>();
        await cut.FindAll("button").First(b => b.TextContent.Trim() == "Print Chart of Accounts")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.Contains("alert-danger", cut.Markup);
        _pdfRenderer.DidNotReceive().Render(Arg.Any<ReportData>(), Arg.Any<string>());
    }

    [Fact]
    public void IncludeBalancesSwitch_Renders_DefaultingToUnchecked()
    {
        var cut = Render<ChartOfAccountsPage>();

        Assert.Equal("false", cut.Find("[role=switch]").GetAttribute("aria-checked"));
    }

    [Fact]
    public async Task ClickPrint_SwitchLeftOff_PassesIncludeBalancesFalse()
    {
        var provider = MakeStubbingProvider();

        var cut = Render<ChartOfAccountsPage>();
        await cut.FindAll("button").First(b => b.TextContent.Trim() == "Print Chart of Accounts")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await provider.Received(1).GenerateAsync(
            Arg.Is<ReportFilterValues>(f => f!.Get("includeBalances") == "false"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClickPrint_SwitchTurnedOn_PassesIncludeBalancesTrue()
    {
        var provider = MakeStubbingProvider();

        var cut = Render<ChartOfAccountsPage>();
        cut.Find("[role=switch]").Click();
        await cut.FindAll("button").First(b => b.TextContent.Trim() == "Print Chart of Accounts")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await provider.Received(1).GenerateAsync(
            Arg.Is<ReportFilterValues>(f => f!.Get("includeBalances") == "true"),
            Arg.Any<CancellationToken>());
    }

    private IReportProvider MakeStubbingProvider()
    {
        var provider = Substitute.For<IReportProvider>();
        provider.GenerateAsync(Arg.Any<ReportFilterValues>(), Arg.Any<CancellationToken>())
            .Returns(new ReportData { Title = "Chart of Accounts" });
        _reportProviderRegistry.GetProvider("chart-of-accounts").Returns(provider);
        // Throw once GenerateAsync has been observed, so the flow never reaches
        // File.WriteAllBytes/Process.Start — the same seam-avoidance technique as T005.
        _pdfRenderer.Render(Arg.Any<ReportData>(), Arg.Any<string>())
            .Returns(_ => throw new InvalidOperationException("stop before file/process"));
        return provider;
    }

    // --- Add form ---

    [Fact]
    public void Should_ShowBankCheckbox_When_AssetTypeSelected()
    {
        var cut = Render<ChartOfAccountsPage>();

        cut.Find("#account-type").Change(AccountType.Asset.ToString());

        Assert.NotNull(cut.Find("#account-is-bank"));
    }

    [Fact]
    public void Should_HideBankCheckbox_When_NonAssetTypeSelected()
    {
        var cut = Render<ChartOfAccountsPage>();

        cut.Find("#account-type").Change(AccountType.Income.ToString());

        Assert.Empty(cut.FindAll("#account-is-bank"));
    }

    [Fact]
    public void Should_CreateAccount_When_FormSubmittedWithValidName()
    {
        var cut = Render<ChartOfAccountsPage>();

        cut.Find("#account-name").Change("Donations");
        cut.Find("#account-type").Change(AccountType.Income.ToString());
        cut.Find("form").Submit();

        _accountService.Received(1).CreateAsync("Donations", AccountType.Income, false, Arg.Any<CancellationToken>());
        Assert.Contains("created with number", cut.Markup);
    }

    [Fact]
    public void Should_ShowValidationError_When_CreateThrowsValidationException()
    {
        _accountService.CreateAsync(Arg.Any<string>(), Arg.Any<AccountType>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns<Task<Account>>(_ => throw new ValidationException("An account named 'Donations' already exists.", "Account", "CreateAsync"));

        var cut = Render<ChartOfAccountsPage>();
        cut.Find("#account-name").Change("Donations");
        cut.Find("form").Submit();

        Assert.Contains("already exists", cut.Markup);
    }

    [Fact]
    public void Should_RejectDuplicateName_ClientSide_WithoutCallingCreateAsync()
    {
        // The shared AddAccountForm's own ExistingNames check (fed from the page's active +
        // archived accounts) now catches an obvious duplicate before ever calling the service.
        var cut = Render<ChartOfAccountsPage>();
        cut.Find("#account-name").Change("cash on hand"); // case-insensitive match on an existing account
        cut.Find("form").Submit();

        _accountService.DidNotReceive().CreateAsync(Arg.Any<string>(), Arg.Any<AccountType>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
        Assert.Contains("already exists", cut.Markup);
    }

    // --- Rename ---

    [Fact]
    public void Should_RenameAccount_When_RenameSaved()
    {
        var cut = Render<ChartOfAccountsPage>();

        cut.FindAll("button").First(b => b.TextContent.Contains("Rename")).Click();
        cut.Find("input[aria-label^='New name']").Change("Renamed Account");
        cut.FindAll("button").First(b => b.TextContent.Contains("Save")).Click();

        _accountService.Received(1).UpdateAsync(Arg.Any<Guid>(), "Renamed Account", Arg.Any<CancellationToken>());
        Assert.Contains("Account renamed.", cut.Markup);
    }

    [Fact]
    public void Should_CancelRename_When_CancelClicked()
    {
        var cut = Render<ChartOfAccountsPage>();

        cut.FindAll("button").First(b => b.TextContent.Contains("Rename")).Click();
        cut.FindAll("button").First(b => b.TextContent.Contains("Cancel")).Click();

        _accountService.DidNotReceive().UpdateAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        Assert.Empty(cut.FindAll("input[aria-label^='New name']"));
    }

    // --- Archive / restore ---

    [Fact]
    public void Should_ArchiveAccount_When_ArchiveClicked()
    {
        var cut = Render<ChartOfAccountsPage>();

        cut.FindAll("button").First(b => b.TextContent.Contains("Archive")).Click();

        _accountService.Received(1).ArchiveAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        Assert.Contains("Account archived.", cut.Markup);
    }

    [Fact]
    public void Should_ShowError_When_ArchiveBlocked()
    {
        _accountService.ArchiveAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new ValidationException(
                "This account cannot be archived because it is referenced by one or more transactions.",
                "Account", "ArchiveAsync"));

        var cut = Render<ChartOfAccountsPage>();
        cut.FindAll("button").First(b => b.TextContent.Contains("Archive")).Click();

        Assert.Contains("cannot be archived", cut.Markup);
    }

    [Fact]
    public void Should_RestoreAccount_When_RestoreClicked()
    {
        _accountService.GetArchivedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Account>
            {
                new()
                {
                    Id = ArchivedAccountId, Name = "Old Fund", Type = AccountType.Income,
                    AccountNumber = "4009", IsDeleted = true,
                    CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
                }
            });
        _accountBalanceService.GetArchivedAccountBalancesAsync(Arg.Any<CancellationToken>())
            .Returns(new List<AccountBalance>
            {
                new()
                {
                    AccountId = ArchivedAccountId, AccountNumber = "4009", Name = "Old Fund",
                    Type = AccountType.Income, Balance = 0m, HasError = false
                }
            });

        var cut = Render<ChartOfAccountsPage>();
        cut.FindAll("button").First(b => b.TextContent.Contains("Restore")).Click();

        _accountService.Received(1).RestoreAsync(ArchivedAccountId, Arg.Any<CancellationToken>());
        Assert.Contains("Account restored.", cut.Markup);
    }

    // --- Helpers ---

    private static IReadOnlyList<Account> DefaultActiveAccounts() => new List<Account>
    {
        new()
        {
            Id = SystemAccountId, Name = "Cash on Hand", Type = AccountType.Asset,
            AccountNumber = "1100", IsSystem = true, IsBankAccount = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        },
        new()
        {
            Id = BankAccountId, Name = "Operating Account", Type = AccountType.Asset,
            AccountNumber = "1110", IsBankAccount = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        },
        new()
        {
            Id = IncomeAccountId, Name = "Membership Fees", Type = AccountType.Income,
            AccountNumber = "4000",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        }
    };

    private static IReadOnlyList<AccountBalance> DefaultActiveAccountBalances() => new List<AccountBalance>
    {
        new()
        {
            AccountId = SystemAccountId, AccountNumber = "1100", Name = "Cash on Hand",
            Type = AccountType.Asset, IsSystem = true, IsBankAccount = true, Balance = 500m, HasError = false
        },
        new()
        {
            AccountId = BankAccountId, AccountNumber = "1110", Name = "Operating Account",
            Type = AccountType.Asset, IsBankAccount = true, Balance = 1000m, HasError = false
        },
        new()
        {
            AccountId = IncomeAccountId, AccountNumber = "4000", Name = "Membership Fees",
            Type = AccountType.Income, Balance = 250m, HasError = false
        }
    };
}
