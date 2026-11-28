using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Modules.Localization.Resources;
using StageFright.Data.Repositories;
using StageFright.Data.Tests.Infrastructure;

namespace StageFright.Data.Tests.Repositories;

/// <summary>
/// Integration tests for <see cref="SystemAccountNameService"/> against the seeded system accounts
/// (issue #394): every seeded system account has a translatable name whose en-AU text is its seeded
/// name, and applying another language renames the stored accounts.
/// </summary>
public class SystemAccountNameIntegrationTests : IDisposable
{
    private readonly DbContextFactory _factory = new();

    private SystemAccountNameService CreateService(StageFrightDbContext db) =>
        new(new AccountRepository(db), RealLocalizer.Instance, NullLogger<SystemAccountNameService>.Instance);

    [Fact]
    public async Task Should_HaveNameKeyMatchingSeededName_When_SystemAccountSeeded_Integration()
    {
        using var db = _factory.CreateContext();
        var systemAccounts = await db.Accounts.Where(a => a.IsSystem)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(SystemAccountNameService.NameKeys.Count, systemAccounts.Count);
        foreach (var account in systemAccounts)
        {
            Assert.True(SystemAccountNameService.NameKeys.TryGetValue(account.Id, out var key),
                $"System account {account.AccountNumber} has no name key.");
            Assert.Equal(account.Name, RealLocalizer.Instance.Get<SystemAccountsResource>(key));
        }
    }

    [Fact]
    public async Task Should_RenameStoredSystemAccounts_When_AppliedInGerman_Integration()
    {
        var original = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");
        try
        {
            using (var db = _factory.CreateContext())
                await CreateService(db).ApplyAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }

        using var verify = _factory.CreateContext();
        var pettyCash = await verify.Accounts.SingleAsync(a => a.Id == SystemAccounts.CashId, TestContext.Current.CancellationToken);
        var badDebt = await verify.Accounts.SingleAsync(a => a.Id == SystemAccounts.BadDebtId, TestContext.Current.CancellationToken);
        Assert.Equal("Handkasse", pettyCash.Name);
        Assert.Equal("Forderungsausfälle", badDebt.Name);
    }

    public void Dispose() => _factory.Dispose();
}
