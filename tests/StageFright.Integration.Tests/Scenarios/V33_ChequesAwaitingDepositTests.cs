using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.AuditTrail;
using StageFright.Core.Modules.Finance;
using StageFright.Data;
using StageFright.Data.Repositories;

namespace StageFright.Integration.Tests.Scenarios;

/// <summary>
/// Acceptance tests for V33 (spec 033, User Story 1): cheques received are held in Undeposited Funds,
/// listed one by one on the Banking screen, while card and EFT receipts go straight to the chosen bank.
/// Real in-memory SQLite with the full EF migration set.
/// </summary>
public sealed class V33_ChequesAwaitingDepositTests : IAsyncLifetime
{
    private StageFrightDbContext _db = null!;

    private static readonly Guid IncomeId = Guid.NewGuid();
    private static readonly Guid BankId = Guid.NewGuid();
    private static readonly Guid OrganisationId = Guid.NewGuid();
    private static readonly DateTime Day1 = new(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Day2 = new(2026, 3, 4, 0, 0, 0, DateTimeKind.Utc);

    public async ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<StageFrightDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _db = new StageFrightDbContext(options);
        await _db.Database.OpenConnectionAsync();
        await _db.Database.MigrateAsync();

        _db.Accounts.AddRange(
            new Account { Id = IncomeId, Name = "Raffle Income", Type = AccountType.Income, AccountNumber = "4000", SortOrder = 0, IsSystem = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Account { Id = BankId, Name = "Community Bank", Type = AccountType.Asset, AccountNumber = "1110", IsBankAccount = true, SortOrder = 0, IsSystem = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _db.Settings.Add(new Settings
        {
            Id = Guid.NewGuid(), OrganizationName = "Test Choir", AnnualFee = 50m, AttendanceFee = 10m,
            MembershipRenewalMonth = 1, MaxAgeRangeYears = 150, MinimumMemberAge = 0, SchemaVersion = "1.1.0",
            FinancialYearStartMonth = 7, FinancialYearStartDay = 1, CurrencyCode = "AUD", IsTaxApplicable = false,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        _db.ExternalOrganisations.Add(new ExternalOrganisation
        {
            Id = OrganisationId, Name = "Town Hall", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    // --- US1-1: cheque member payment ------------------------------------------------

    [Fact]
    public async Task Should_DropMemberBalanceAndRaiseUndepositedFundsAndLeaveBankUntouched_When_MemberPaysByCheque_Integration()
    {
        var memberId = await AddMemberAsync("Alice");
        await SeedOutstandingFeeAsync(memberId, 50m);
        var before = await OutstandingAsync(memberId);

        await RecordChequePaymentAsync(memberId, 50m, "CHQ 101", Day1);

        Assert.Equal(50m, before);
        Assert.Equal(0m, await OutstandingAsync(memberId));
        Assert.Equal(50m, await BalanceAsync(SystemAccounts.UndepositedFundsId));
        Assert.Equal(0m, await BalanceAsync(SystemAccounts.CashId));
        Assert.Equal(0m, await BalanceAsync(BankId));
    }

    // --- US1-2: non-member cheque income ---------------------------------------------

    [Fact]
    public async Task Should_RecogniseIncomeAndHoldInUndepositedFunds_When_ChequeIncomeRecorded_Integration()
    {
        await BuildIncomeService().RecordIncomeAsync(new RecordIncomeRequest
        {
            Date = Day1, Amount = 120m, AccountId = IncomeId, PaymentMethod = PaymentMethod.Check, Reference = "SP 77"
        }, TestContext.Current.CancellationToken);

        Assert.Equal(-120m, await BalanceAsync(IncomeId));
        Assert.Equal(120m, await BalanceAsync(SystemAccounts.UndepositedFundsId));
        Assert.Equal(0m, await BalanceAsync(BankId));
        Assert.Equal(0m, await BalanceAsync(SystemAccounts.CashId));
    }

    // --- US1-4: card and EFT go straight to the bank ----------------------------------

    [Theory]
    [InlineData(PaymentMethod.Card)]
    [InlineData(PaymentMethod.ElectronicTransfer)]
    public async Task Should_PostToChosenBankAndNotListAwaiting_When_PaidByCardOrEft_Integration(PaymentMethod method)
    {
        var memberId = await AddMemberAsync("Bob");
        await SeedOutstandingFeeAsync(memberId, 50m);

        await BuildPaymentService().RecordAsync(new RecordPaymentRequest
        {
            MemberId = memberId, Date = Day1, Amount = 30m, PaymentMethod = method,
            BankAccountId = BankId, PaymentType = PaymentType.Annual,
            SelectedFeeIds = [await _db.Fees.Select(f => f.Id).SingleAsync(TestContext.Current.CancellationToken)]
        }, TestContext.Current.CancellationToken);

        await BuildIncomeService().RecordIncomeAsync(new RecordIncomeRequest
        {
            Date = Day1, Amount = 20m, AccountId = IncomeId, PaymentMethod = method, BankAccountId = BankId
        }, TestContext.Current.CancellationToken);

        Assert.Equal(50m, await BalanceAsync(BankId));
        Assert.Equal(0m, await BalanceAsync(SystemAccounts.UndepositedFundsId));
        var overview = await BuildBankingService().GetOverviewAsync(TestContext.Current.CancellationToken);
        Assert.Empty(overview.AwaitingItems);
        Assert.Equal(0m, overview.UndepositedTotal);
    }

    // --- US1-3: listing with date, payer, reference, amount and total -----------------

    [Fact]
    public async Task Should_ListTwoChequesWithDatePayerReferenceAmountAndTotal_When_BankingOverviewRead_Integration()
    {
        var memberId = await AddMemberAsync("Alice");
        await SeedOutstandingFeeAsync(memberId, 50m);
        await RecordChequePaymentAsync(memberId, 50m, "CHQ 101", Day1);
        await BuildIncomeService().RecordIncomeAsync(new RecordIncomeRequest
        {
            Date = Day2, Amount = 120m, AccountId = IncomeId, PaymentMethod = PaymentMethod.Check,
            Reference = "SP 77", Description = "Sponsor night"
        }, TestContext.Current.CancellationToken);

        var overview = await BuildBankingService().GetOverviewAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, overview.AwaitingItems.Count);
        var member = Assert.Single(overview.AwaitingItems, i => i.Reference == "CHQ 101");
        Assert.Equal(Day1.Date, member.ReceivedDate.Date);
        Assert.Contains("Alice", member.PayerName);
        Assert.Equal(50m, member.Amount);
        var sponsor = Assert.Single(overview.AwaitingItems, i => i.Reference == "SP 77");
        Assert.Equal(Day2.Date, sponsor.ReceivedDate.Date);
        Assert.Equal("Sponsor night", sponsor.PayerName);
        Assert.Equal(120m, sponsor.Amount);
        Assert.Equal(170m, overview.UndepositedTotal);
        Assert.Equal(170m, await BalanceAsync(SystemAccounts.UndepositedFundsId));
    }

    // --- FR-025: organisation-linked cheque -------------------------------------------

    [Fact]
    public async Task Should_ShowOrganisationNameAndKeepLink_When_ChequeIncomeIsOrganisationLinked_Integration()
    {
        await BuildIncomeService().RecordIncomeAsync(new RecordIncomeRequest
        {
            Date = Day1, Amount = 200m, AccountId = IncomeId, PaymentMethod = PaymentMethod.Check,
            Reference = "TH 9", Description = "Hall donation", ExternalOrganisationId = OrganisationId
        }, TestContext.Current.CancellationToken);

        var overview = await BuildBankingService().GetOverviewAsync(TestContext.Current.CancellationToken);

        var item = Assert.Single(overview.AwaitingItems);
        Assert.Equal("Town Hall", item.PayerName);
        var entry = await _db.JournalEntries.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(OrganisationId, entry.ExternalOrganisationId);
    }

    // --- Helpers -----------------------------------------------------------------------

    private async Task RecordChequePaymentAsync(Guid memberId, decimal amount, string reference, DateTime date)
    {
        var feeId = await _db.Fees.Where(f => f.MemberId == memberId).Select(f => f.Id).SingleAsync(TestContext.Current.CancellationToken);
        await BuildPaymentService().RecordAsync(new RecordPaymentRequest
        {
            MemberId = memberId, Date = date, Amount = amount, PaymentMethod = PaymentMethod.Check,
            Reference = reference, PaymentType = PaymentType.Annual, SelectedFeeIds = [feeId]
        }, TestContext.Current.CancellationToken);
    }

    // Debits minus credits on an account (income accounts are therefore negative).
    private async Task<decimal> BalanceAsync(Guid accountId) =>
        await _db.Transactions.Where(t => t.AccountId == accountId).SumAsync(t => t.DebitAmount - t.CreditAmount, TestContext.Current.CancellationToken);

    private async Task<decimal> OutstandingAsync(Guid memberId) =>
        await _db.Transactions.Where(t => t.AccountId == SystemAccounts.MemberReceivableId && t.MemberId == memberId)
            .SumAsync(t => t.DebitAmount - t.CreditAmount, TestContext.Current.CancellationToken);

    private async Task<Guid> AddMemberAsync(string firstName)
    {
        var member = new Member
        {
            Id = Guid.NewGuid(), FirstName = firstName, LastName = "Tester", StreetAddress = "1 Test St",
            Status = MemberStatus.Active,
            ActivateDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            JoinDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        _db.Members.Add(member);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return member.Id;
    }

    private async Task SeedOutstandingFeeAsync(Guid memberId, decimal amount)
    {
        var feeId = Guid.NewGuid();
        var feeDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var now = DateTime.UtcNow;
        _db.Fees.Add(new Fee
        {
            Id = feeId, MemberId = memberId, FeeType = FeeType.Annual, Amount = amount,
            FeeDate = feeDate, DueDate = feeDate.AddYears(1).AddDays(-1), PaidAtCreation = false, CreatedAt = now
        });
        _db.Transactions.AddRange(
            new Transaction { Id = Guid.NewGuid(), Date = feeDate, AccountId = SystemAccounts.MemberReceivableId, DebitAmount = amount, CreditAmount = 0m, GLAccount = SystemAccounts.MemberReceivableNumber, MemberId = memberId, FeeId = feeId, CreatedAt = now },
            new Transaction { Id = Guid.NewGuid(), Date = feeDate, AccountId = IncomeId, DebitAmount = 0m, CreditAmount = amount, GLAccount = "4000", MemberId = null, FeeId = feeId, CreatedAt = now });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private GLRepository Gl() => new(_db, new ClosedPeriodGuard(new SettingsRepository(_db)));

    private static AuditTrailService Audit() =>
        new(Substitute.For<IAuditTrailRepository>(), NullLogger<AuditTrailService>.Instance);

    private IncomeEntryService BuildIncomeService() =>
        new(new AccountRepository(_db), Gl(), new JournalEntryRepository(_db), new SettingsRepository(_db), Audit(), new UnitOfWork(_db), RealLocalizer.Instance, new ExternalOrganisationRepository(_db), new ReceivedItemRepository(_db));

    private PaymentService BuildPaymentService() =>
        new(new FeeRepository(_db), new PaymentRepository(_db, Audit()), Gl(), new MemberRepository(_db), new AccountRepository(_db), new ReceivedItemRepository(_db), Audit(), new UnitOfWork(_db), RealLocalizer.Instance);

    private BankingService BuildBankingService() =>
        new(new AccountRepository(_db), Gl(), new JournalEntryRepository(_db), new ReceivedItemRepository(_db), new BankDepositRepository(_db),
            new BankReconciliationRepository(_db), new ClosedPeriodGuard(new SettingsRepository(_db)), Audit(), new UnitOfWork(_db), RealLocalizer.Instance);
}
