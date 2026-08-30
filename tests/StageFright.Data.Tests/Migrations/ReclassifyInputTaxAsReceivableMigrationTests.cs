using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StageFright.Core.Enums;

namespace StageFright.Data.Tests.Migrations;

/// <summary>
/// Integration test for the ReclassifyInputTaxAsReceivable migration (spec 028 Phase 16,
/// issue #355): the seeded system account <c>2320</c> is re-typed from
/// <see cref="AccountType.Liability"/> "Tax Paid" to <see cref="AccountType.Asset"/>
/// "Tax Receivable" — recoverable input tax is an asset. Classification only: the account
/// keeps its number and no monetary/<c>TaxCode</c> value moves. Account <c>2310</c>
/// "Tax Collected" stays a liability.
/// </summary>
public sealed class ReclassifyInputTaxAsReceivableMigrationTests : IDisposable
{
    private const string PreMigration = "20260830021225_AddTaxEntryMode";

    private static readonly Guid TaxCollectedId = new("00000000-0000-0000-0000-000000000004");
    private static readonly Guid TaxReceivableId = new("00000000-0000-0000-0000-000000000005");

    private readonly SqliteConnection _connection;

    public ReclassifyInputTaxAsReceivableMigrationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    private StageFrightDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<StageFrightDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new StageFrightDbContext(options);
    }

    [Fact]
    public async Task Account2320_IsLiabilityBeforeMigration_AndAssetTaxReceivableAfter()
    {
        using (var db = CreateContext())
        {
            await db.GetService<IMigrator>().MigrateAsync(PreMigration, TestContext.Current.CancellationToken);

            var before = await db.Accounts.AsNoTracking()
                .SingleAsync(a => a.Id == TaxReceivableId, TestContext.Current.CancellationToken);
            Assert.Equal(AccountType.Liability, before.Type);
            Assert.Equal("Tax Paid", before.Name);
            Assert.Equal("2320", before.AccountNumber);
        }

        using (var db = CreateContext())
        {
            await db.Database.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);

            var after = await db.Accounts.AsNoTracking()
                .SingleAsync(a => a.Id == TaxReceivableId, TestContext.Current.CancellationToken);
            Assert.Equal(AccountType.Asset, after.Type);
            Assert.Equal("Tax Receivable", after.Name);
            Assert.Equal("2320", after.AccountNumber);   // number kept as a documented asset exception
            Assert.True(after.IsSystem);

            // 2310 "Tax Collected" is owed to the authority — still a liability, untouched.
            var taxCollected = await db.Accounts.AsNoTracking()
                .SingleAsync(a => a.Id == TaxCollectedId, TestContext.Current.CancellationToken);
            Assert.Equal(AccountType.Liability, taxCollected.Type);
            Assert.Equal("Tax Collected", taxCollected.Name);
        }
    }
}
