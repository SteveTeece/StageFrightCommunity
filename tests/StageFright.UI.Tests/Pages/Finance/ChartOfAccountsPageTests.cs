using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.UI.Pages.Finance;

namespace StageFright.UI.Tests.Pages.Finance;

/// <summary>
/// bUnit tests for ChartOfAccountsPage — renders accounts across all five types with
/// filter, add form (bank flag only for Asset), rename, archive blocking with error
/// message, restore, and system account read-only enforcement.
/// </summary>
public class ChartOfAccountsPageTests : BunitContext
{
    private readonly IAccountService _accountService = Substitute.For<IAccountService>();

    private static readonly Guid IncomeAccountId = Guid.NewGuid();
    private static readonly Guid BankAccountId = Guid.NewGuid();
    private static readonly Guid SystemAccountId = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid ArchivedAccountId = Guid.NewGuid();

    public ChartOfAccountsPageTests()
    {
        Services.AddSingleton(_accountService);

        _accountService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(DefaultActiveAccounts());

        _accountService.GetArchivedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Account>());

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
        _accountService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<Account>>>(_ => throw new InvalidOperationException("boom"));

        var cut = Render<ChartOfAccountsPage>();

        Assert.Contains("Failed to load accounts", cut.Markup);
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
}
