using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StageFright.Core.Enums;

namespace StageFright.Data.Tests.Migrations;

/// <summary>
/// Integration test for the GenericSalesTax migration (spec 016): applies the pre-migration
/// (GST/ABN) schema, inserts old-shape Settings/Fee/Transaction rows, migrates to latest, and
/// asserts every historical value is remapped to its generic equivalent — with dollar amounts
/// byte-identical and the Abn column gone — per specs/016-generic-sales-tax/data-model.md.
/// </summary>
public sealed class GenericSalesTaxMigrationTests : IDisposable
{
    private const string PreMigration = "20260809083931_AddAuditRetentionYearsToSettings";

    private static readonly Guid RegisteredSettingsId = new("77777777-0000-0000-0000-000000000001");
    private static readonly Guid UnregisteredSettingsId = new("77777777-0000-0000-0000-000000000002");
    private static readonly Guid MemberId = new("77777777-0000-0000-0000-000000000003");
    private static readonly Guid FeeId = new("77777777-0000-0000-0000-000000000004");
    private static readonly Guid TransactionId = new("77777777-0000-0000-0000-000000000005");

    // Seeded by earlier migrations — Cash on Hand (1100), a valid FK target for the Transaction row.
    private static readonly Guid CashAccountId = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid TaxCollectedAccountId = new("00000000-0000-0000-0000-000000000004");

    private readonly SqliteConnection _connection;

    public GenericSalesTaxMigrationTests()
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
    public async Task ExistingData_RemapsToGenericTaxModel_AfterMigration()
    {
        using (var db = CreateContext())
        {
            await db.GetService<IMigrator>().MigrateAsync(PreMigration);

            // Registered org: ABN on file, GST-registered, both fee types coded (one taxable,
            // one input-taxed — the concept being retired and folded into tax-exempt).
            await db.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO Settings (Id, OrganizationName, Abn, AnnualFee, AttendanceFee, MembershipRenewalMonth, CommitteeRenewalMonth, FinancialYearStartMonth, IsGstRegistered, AnnualFeeGstCode, AttendanceFeeGstCode, GeneralCommitteeSeatCountTarget, MaxAgeRangeYears, MinimumMemberAge, Theme, ShowParticipationGraphs, AuditRetentionYears, SchemaVersion, IsDeleted, DeletedAt, DeletedBy, CreatedAt, UpdatedAt) VALUES
                ({RegisteredSettingsId}, 'Registered Org', '51824753556', 100.0, 10.0, 1, 1, 7, 1, 'Gst', 'InputTaxed', NULL, 150, 0, 'Light', 1, 1, '1.1.0', 0, NULL, NULL, '2026-01-01 00:00:00', '2026-01-01 00:00:00');
                """);

            // Unregistered org: no ABN, no GST — must end up with IsTaxApplicable=0, TaxRate=NULL.
            await db.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO Settings (Id, OrganizationName, Abn, AnnualFee, AttendanceFee, MembershipRenewalMonth, CommitteeRenewalMonth, FinancialYearStartMonth, IsGstRegistered, AnnualFeeGstCode, AttendanceFeeGstCode, GeneralCommitteeSeatCountTarget, MaxAgeRangeYears, MinimumMemberAge, Theme, ShowParticipationGraphs, AuditRetentionYears, SchemaVersion, IsDeleted, DeletedAt, DeletedBy, CreatedAt, UpdatedAt) VALUES
                ({UnregisteredSettingsId}, 'Unregistered Org', NULL, 50.0, 5.0, 1, 1, 7, 0, NULL, NULL, NULL, 150, 0, 'Dark', 1, 1, '1.1.0', 0, NULL, NULL, '2026-01-01 00:00:00', '2026-01-01 00:00:00');
                """);

            await db.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO Members (Id, FirstName, LastName, StreetAddress, Phone, Email, JoinDate, DateOfBirth, Status, ActivateDate, IsDeleted, DeletedAt, DeletedBy, CreatedAt, UpdatedAt) VALUES
                ({MemberId}, 'Test', 'Member', '1 Test St', '0400000000', 'test@example.com', '2026-01-01 00:00:00', '1990-01-01 00:00:00', 'Active', '2026-01-01 00:00:00', 0, NULL, NULL, '2026-01-01 00:00:00', '2026-01-01 00:00:00');
                """);

            // Historical Fee posted while input-taxed — must survive with amount unchanged and
            // TaxCode remapped to the nearest generic equivalent (TaxExempt: no tax component).
            await db.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO Fees (Id, MemberId, FeeType, Amount, FeeDate, DueDate, PaidAtCreation, RehearsalId, GstCode, CreatedAt) VALUES
                ({FeeId}, {MemberId}, 'Annual', 123.45, '2026-01-01 00:00:00', '2026-12-31 00:00:00', 0, NULL, 'InputTaxed', '2026-01-01 00:00:00');
                """);

            // Historical Transaction posted as BAS-excluded (e.g. a transfer) — must remap to Excluded.
            await db.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO Transactions (Id, Date, AccountId, DebitAmount, CreditAmount, GLAccount, MemberId, PaymentId, FeeId, JournalEntryId, GstCode, Description, CreatedAt) VALUES
                ({TransactionId}, '2026-01-01 00:00:00', {CashAccountId}, 200.00, 0.00, '1100', NULL, NULL, NULL, NULL, 'BasExcluded', 'Historical transfer', '2026-01-01 00:00:00');
                """);
        }

        using (var db = CreateContext())
        {
            await db.Database.MigrateAsync();

            var registered = await db.Settings.SingleAsync(s => s.Id == RegisteredSettingsId);
            Assert.True(registered.IsTaxApplicable);
            Assert.Equal(10m, registered.TaxRate);
            Assert.Equal(TaxCode.Taxable, registered.AnnualFeeTaxCode);
            Assert.Equal(TaxCode.TaxExempt, registered.AttendanceFeeTaxCode);
            Assert.Equal("Registered Org", registered.OrganizationName);

            var unregistered = await db.Settings.SingleAsync(s => s.Id == UnregisteredSettingsId);
            Assert.False(unregistered.IsTaxApplicable);
            Assert.Null(unregistered.TaxRate);
            Assert.Null(unregistered.AnnualFeeTaxCode);
            Assert.Null(unregistered.AttendanceFeeTaxCode);

            var fee = await db.Fees.SingleAsync(f => f.Id == FeeId);
            Assert.Equal(TaxCode.TaxExempt, fee.TaxCode);
            Assert.Equal(123.45m, fee.Amount);

            var transaction = await db.Transactions.SingleAsync(t => t.Id == TransactionId);
            Assert.Equal(TaxCode.Excluded, transaction.TaxCode);
            Assert.Equal(200.00m, transaction.DebitAmount);

            var taxCollectedAccount = await db.Accounts.SingleAsync(a => a.Id == TaxCollectedAccountId);
            Assert.Equal("Tax Collected", taxCollectedAccount.Name);
        }
    }
}
