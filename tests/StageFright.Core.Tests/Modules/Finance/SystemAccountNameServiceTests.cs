using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Tests.Fixtures;

namespace StageFright.Core.Tests.Modules.Finance;

/// <summary>
/// Unit tests for <see cref="SystemAccountNameService"/> (issue #394): system accounts are renamed
/// to the current language, already-correct names are left alone, organisation accounts are never
/// touched, a translated name already in use is not reused, and a failure never escapes.
/// </summary>
public class SystemAccountNameServiceTests : TestBase
{
    private readonly IAccountRepository _repo = Substitute.For<IAccountRepository>();
    private readonly SystemAccountNameService _sut;

    public SystemAccountNameServiceTests()
    {
        _sut = new SystemAccountNameService(_repo, RealLocalizer.Instance, NullLogger<SystemAccountNameService>.Instance);
    }

    private static Account SystemAccount(Guid id, string name) => new()
    {
        Id = id,
        Name = name,
        Type = AccountType.Asset,
        AccountNumber = "1100",
        IsSystem = true,
    };

    private void GivenAccounts(params Account[] accounts) =>
        _repo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(accounts.ToList());

    private static async Task InCultureAsync(string culture, Func<Task> action)
    {
        var original = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            await action();
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }

    [Fact]
    public async Task Should_RenameEverySystemAccount_When_NamesDifferFromCurrentLanguage()
    {
        var accounts = SystemAccountNameService.NameKeys.Keys.Select(id => SystemAccount(id, "Old name")).ToArray();
        GivenAccounts(accounts);

        await _sut.ApplyAsync(Ct);

        Assert.Equal("Petty Cash", accounts.Single(a => a.Id == SystemAccounts.CashId).Name);
        Assert.Equal("Undeposited Funds", accounts.Single(a => a.Id == SystemAccounts.UndepositedFundsId).Name);
        Assert.Equal("Member Receivable", accounts.Single(a => a.Id == SystemAccounts.MemberReceivableId).Name);
        Assert.Equal("Tax Collected", accounts.Single(a => a.Id == SystemAccounts.TaxCollectedId).Name);
        Assert.Equal("Tax Receivable", accounts.Single(a => a.Id == SystemAccounts.TaxPaidId).Name);
        Assert.Equal("Opening Balance Equity", accounts.Single(a => a.Id == SystemAccounts.OpeningBalanceEquityId).Name);
        Assert.Equal("Accumulated Surplus", accounts.Single(a => a.Id == SystemAccounts.AccumulatedSurplusId).Name);
        Assert.Equal("Bad Debt Expense", accounts.Single(a => a.Id == SystemAccounts.BadDebtId).Name);
        await _repo.Received(accounts.Length).UpdateAsync(Arg.Any<Account>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_UseTranslatedName_When_CultureIsFrench()
    {
        var pettyCash = SystemAccount(SystemAccounts.CashId, "Petty Cash");
        GivenAccounts(pettyCash);

        await InCultureAsync("fr-FR", () => _sut.ApplyAsync(Ct));

        Assert.Equal("Petite caisse", pettyCash.Name);
        await _repo.Received(1).UpdateAsync(pettyCash, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_NotUpdate_When_NameAlreadyMatchesCurrentLanguage()
    {
        GivenAccounts(SystemAccount(SystemAccounts.CashId, "Petty Cash"));

        await _sut.ApplyAsync(Ct);

        await _repo.DidNotReceive().UpdateAsync(Arg.Any<Account>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_LeaveOrganisationAccountsAlone_When_Applied()
    {
        var own = new Account { Id = SystemAccounts.CashId, Name = "Float tin", IsSystem = false };
        GivenAccounts(own);

        await _sut.ApplyAsync(Ct);

        Assert.Equal("Float tin", own.Name);
        await _repo.DidNotReceive().UpdateAsync(Arg.Any<Account>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_LeaveSystemAccountAlone_When_ItHasNoNameKey()
    {
        var unknown = SystemAccount(Guid.NewGuid(), "Plugin account");
        GivenAccounts(unknown);

        await _sut.ApplyAsync(Ct);

        Assert.Equal("Plugin account", unknown.Name);
        await _repo.DidNotReceive().UpdateAsync(Arg.Any<Account>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_KeepCurrentName_When_AnotherAccountAlreadyUsesTranslatedName()
    {
        var pettyCash = SystemAccount(SystemAccounts.CashId, "Petty Cash");
        var own = new Account { Id = Guid.NewGuid(), Name = "petite caisse", IsSystem = false };
        GivenAccounts(pettyCash, own);

        await InCultureAsync("fr-FR", () => _sut.ApplyAsync(Ct));

        Assert.Equal("Petty Cash", pettyCash.Name);
        await _repo.DidNotReceive().UpdateAsync(Arg.Any<Account>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_NotThrow_When_RepositoryFails()
    {
        _repo.GetAllAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new DataAccessException("db down", nameof(Account), "GetAllAsync"));

        var exception = await Record.ExceptionAsync(() => _sut.ApplyAsync(Ct));

        Assert.Null(exception);
    }

    [Fact]
    public async Task Should_Throw_When_Cancelled()
    {
        _repo.GetAllAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() => _sut.ApplyAsync(Ct));
    }
}
