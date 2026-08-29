using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Modules.Settings;
using StageFright.Core.Tests.Fixtures;

namespace StageFright.Core.Tests.Setup;

/// <summary>
/// Unit tests for SetupService validation and initialization logic.
/// All validation tests assert the exact constraint (required fields, fee ≥ 0, month 1–12).
/// </summary>
public class SetupServiceTests : TestBase
{
    private readonly ISettingsRepository _settingsRepo = Substitute.For<ISettingsRepository>();
    private readonly IAccountRepository _accountRepo = Substitute.For<IAccountRepository>();
    private readonly IEventTypeRepository _eventTypeRepo = Substitute.For<IEventTypeRepository>();
    private readonly ICommitteeOfficeHolderTypeService _officeHolderTypeService = Substitute.For<ICommitteeOfficeHolderTypeService>();
    private readonly IAccountService _accountService = Substitute.For<IAccountService>();
    private readonly IOpeningBalanceService _openingBalanceService = Substitute.For<IOpeningBalanceService>();
    private readonly IAuditTrailService _audit = Substitute.For<IAuditTrailService>();

    public SetupServiceTests()
    {
        _eventTypeRepo.AddAsync(Arg.Any<StageFright.Core.Entities.EventType>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<StageFright.Core.Entities.EventType>(0));
    }

    private SetupService CreateService() => new(
        _settingsRepo, _accountRepo, _eventTypeRepo, _officeHolderTypeService,
        _accountService, _openingBalanceService, _audit, RealLocalizer.Instance);

    // --- IsSetupCompleteAsync ---

    [Fact]
    public async Task IsSetupCompleteAsync_ReturnsFalse_WhenNoSettingsRecord()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);

        var svc = CreateService();
        Assert.False(await svc.IsSetupCompleteAsync(Ct));
    }

    [Fact]
    public async Task IsSetupCompleteAsync_ReturnsTrue_WhenSettingsExist()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns(new Settings { Id = Guid.NewGuid() });

        var svc = CreateService();
        Assert.True(await svc.IsSetupCompleteAsync(Ct));
    }

    // --- InitializeAsync validation ---

    [Fact]
    public async Task InitializeAsync_Throws_WhenOrganizationNameEmpty()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        var svc = CreateService();

        var request = ValidRequest() with { OrganizationName = "" };
        await Assert.ThrowsAsync<ValidationException>(() => svc.InitializeAsync(request, Ct));
    }

    [Fact]
    public async Task InitializeAsync_Throws_WhenTaxApplicableWithoutRate()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        var svc = CreateService();

        var request = ValidRequest() with { IsTaxApplicable = true, TaxRate = null };
        await Assert.ThrowsAsync<ValidationException>(() => svc.InitializeAsync(request, Ct));
    }

    [Fact]
    public async Task InitializeAsync_Throws_WhenTaxApplicableWithNonPositiveRate()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        var svc = CreateService();

        var request = ValidRequest() with { IsTaxApplicable = true, TaxRate = 0m };
        await Assert.ThrowsAsync<ValidationException>(() => svc.InitializeAsync(request, Ct));
    }

    [Fact]
    public async Task InitializeAsync_ForcesTaxFieldsNull_WhenNotApplicable()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        _accountRepo.GetNextAccountNumberAsync(Arg.Any<Core.Enums.AccountType>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns("4000");
        var svc = CreateService();

        var request = ValidRequest() with
        {
            IsTaxApplicable = false,
            TaxRate = 10m,
            AnnualFeeTaxCode = TaxCode.Taxable,
            AttendanceFeeTaxCode = TaxCode.Taxable
        };
        await svc.InitializeAsync(request, Ct);

        await _settingsRepo.Received(1).SaveAsync(
            Arg.Is<Settings>(s => s!.IsTaxApplicable == false && s.TaxRate == null
                && s.AnnualFeeTaxCode == null && s.AttendanceFeeTaxCode == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitializeAsync_PersistsTaxFields_WhenApplicable()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        _accountRepo.GetNextAccountNumberAsync(Arg.Any<Core.Enums.AccountType>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns("4000");
        var svc = CreateService();

        var request = ValidRequest() with
        {
            IsTaxApplicable = true,
            TaxRate = 15m,
            AnnualFeeTaxCode = TaxCode.Taxable,
            AttendanceFeeTaxCode = TaxCode.TaxExempt
        };
        await svc.InitializeAsync(request, Ct);

        await _settingsRepo.Received(1).SaveAsync(
            Arg.Is<Settings>(s => s!.IsTaxApplicable && s.TaxRate == 15m
                && s.AnnualFeeTaxCode == TaxCode.Taxable && s.AttendanceFeeTaxCode == TaxCode.TaxExempt),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitializeAsync_Throws_WhenAnnualFeeNegative()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        var svc = CreateService();

        var request = ValidRequest() with { AnnualFee = -1m };
        await Assert.ThrowsAsync<ValidationException>(() => svc.InitializeAsync(request, Ct));
    }

    [Fact]
    public async Task InitializeAsync_Allows_ZeroAnnualFee()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        _accountRepo.GetNextAccountNumberAsync(Arg.Any<Core.Enums.AccountType>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns("4000");
        var svc = CreateService();

        var request = ValidRequest() with { AnnualFee = 0m };
        await svc.InitializeAsync(request, Ct); // must not throw
    }

    [Fact]
    public async Task InitializeAsync_Throws_WhenAttendanceFeeNegative()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        var svc = CreateService();

        var request = ValidRequest() with { AttendanceFee = -0.01m };
        await Assert.ThrowsAsync<ValidationException>(() => svc.InitializeAsync(request, Ct));
    }

    [Fact]
    public async Task InitializeAsync_Throws_WhenRenewalMonthZero()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        var svc = CreateService();

        var request = ValidRequest() with { MembershipRenewalMonth = 0 };
        await Assert.ThrowsAsync<ValidationException>(() => svc.InitializeAsync(request, Ct));
    }

    [Fact]
    public async Task InitializeAsync_Throws_WhenRenewalMonthThirteen()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        var svc = CreateService();

        var request = ValidRequest() with { MembershipRenewalMonth = 13 };
        await Assert.ThrowsAsync<ValidationException>(() => svc.InitializeAsync(request, Ct));
    }

    [Fact]
    public async Task InitializeAsync_SavesSettings_WithCorrectValues()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        _accountRepo.GetNextAccountNumberAsync(Arg.Any<Core.Enums.AccountType>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns("4000");
        var svc = CreateService();

        var request = ValidRequest();
        await svc.InitializeAsync(request, Ct);

        await _settingsRepo.Received(1).SaveAsync(
            Arg.Is<Settings>(s => s!.OrganizationName == "Test Org" && s.AnnualFee == 75m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitializeAsync_Throws_WhenAlreadySetup()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new Settings { Id = Guid.NewGuid() });
        var svc = CreateService();

        await Assert.ThrowsAsync<ValidationException>(
            () => svc.InitializeAsync(ValidRequest(), Ct));
    }

    [Theory]
    [InlineData(Theme.Light)]
    [InlineData(Theme.Dark)]
    public async Task InitializeAsync_PersistsRequestedTheme(Theme requestedTheme)
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        _accountRepo.GetNextAccountNumberAsync(Arg.Any<Core.Enums.AccountType>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns("4000");
        var svc = CreateService();

        var request = ValidRequest() with { Theme = requestedTheme };
        await svc.InitializeAsync(request, Ct);

        await _settingsRepo.Received(1).SaveAsync(
            Arg.Is<Settings>(s => s!.Theme == requestedTheme),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    public async Task InitializeAsync_Throws_WhenAuditRetentionYearsOutOfRange(int years)
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        var svc = CreateService();

        var request = ValidRequest() with { AuditRetentionYears = years };
        await Assert.ThrowsAsync<ValidationException>(() => svc.InitializeAsync(request, Ct));
    }

    [Fact]
    public async Task InitializeAsync_PersistsDefaultAuditRetentionYears_WhenNotSpecified()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        _accountRepo.GetNextAccountNumberAsync(Arg.Any<Core.Enums.AccountType>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns("4000");
        var svc = CreateService();

        await svc.InitializeAsync(ValidRequest(), Ct);

        await _settingsRepo.Received(1).SaveAsync(
            Arg.Is<Settings>(s => s!.AuditRetentionYears == 1),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    public async Task InitializeAsync_PersistsRequestedAuditRetentionYears(int years)
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        _accountRepo.GetNextAccountNumberAsync(Arg.Any<Core.Enums.AccountType>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns("4000");
        var svc = CreateService();

        var request = ValidRequest() with { AuditRetentionYears = years };
        await svc.InitializeAsync(request, Ct);

        await _settingsRepo.Received(1).SaveAsync(
            Arg.Is<Settings>(s => s!.AuditRetentionYears == years),
            Arg.Any<CancellationToken>());
    }

    // --- Currency (spec 028, US1 / FR-001) ---

    [Fact]
    public async Task InitializeAsync_PersistsDefaultCurrency_WhenNotSpecified()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        _accountRepo.GetNextAccountNumberAsync(Arg.Any<Core.Enums.AccountType>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns("4000");
        var svc = CreateService();

        await svc.InitializeAsync(ValidRequest(), Ct);

        await _settingsRepo.Received(1).SaveAsync(
            Arg.Is<Settings>(s => s!.CurrencyCode == "AUD"),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("USD", "USD")]
    [InlineData("jpy", "JPY")]
    [InlineData(" eur ", "EUR")]
    public async Task InitializeAsync_PersistsRequestedCurrency_Normalised(string requested, string persisted)
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        _accountRepo.GetNextAccountNumberAsync(Arg.Any<Core.Enums.AccountType>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns("4000");
        var svc = CreateService();

        var request = ValidRequest() with { CurrencyCode = requested };
        await svc.InitializeAsync(request, Ct);

        await _settingsRepo.Received(1).SaveAsync(
            Arg.Is<Settings>(s => s!.CurrencyCode == persisted),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("ZZZ")]
    [InlineData("")]
    [InlineData("dollars")]
    public async Task InitializeAsync_Throws_WhenCurrencyUnknown(string code)
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        var svc = CreateService();

        var request = ValidRequest() with { CurrencyCode = code };
        await Assert.ThrowsAsync<ValidationException>(() => svc.InitializeAsync(request, Ct));
        await _settingsRepo.DidNotReceive().SaveAsync(Arg.Any<Settings>(), Arg.Any<CancellationToken>());
    }

    // --- Queued accounts / opening balances (spec 017) ---

    [Fact]
    public async Task InitializeAsync_CreatesNoAccounts_WhenQueuedAccountsEmpty()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        _accountRepo.GetNextAccountNumberAsync(Arg.Any<Core.Enums.AccountType>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns("4000");
        var svc = CreateService();

        await svc.InitializeAsync(ValidRequest(), Ct);

        await _accountService.DidNotReceive().CreateAsync(
            Arg.Any<string>(), Arg.Any<Core.Enums.AccountType>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitializeAsync_PostsNoOpeningBalances_WhenQueuedOpeningBalancesEmpty()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        _accountRepo.GetNextAccountNumberAsync(Arg.Any<Core.Enums.AccountType>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns("4000");
        var svc = CreateService();

        await svc.InitializeAsync(ValidRequest(), Ct);

        await _openingBalanceService.DidNotReceive().RecordOpeningBalancesAsync(
            Arg.Any<RecordOpeningBalancesRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitializeAsync_CreatesEachQueuedAccount()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        _accountRepo.GetNextAccountNumberAsync(Arg.Any<Core.Enums.AccountType>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns("4000");
        _accountService.CreateAsync(Arg.Any<string>(), Arg.Any<Core.Enums.AccountType>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(ci => new Account
            {
                Id = Guid.NewGuid(),
                Name = ci.ArgAt<string>(0),
                Type = ci.ArgAt<Core.Enums.AccountType>(1),
                IsBankAccount = ci.ArgAt<bool>(2)
            });
        var svc = CreateService();

        var request = ValidRequest() with
        {
            QueuedAccounts =
            [
                new QueuedAccountRequest(Guid.NewGuid(), "Community Bank Account", Core.Enums.AccountType.Asset, true),
                new QueuedAccountRequest(Guid.NewGuid(), "Merchandise Income", Core.Enums.AccountType.Income, false)
            ]
        };
        await svc.InitializeAsync(request, Ct);

        await _accountService.Received(1).CreateAsync("Community Bank Account", Core.Enums.AccountType.Asset, true, Arg.Any<CancellationToken>());
        await _accountService.Received(1).CreateAsync("Merchandise Income", Core.Enums.AccountType.Income, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitializeAsync_ResolvesQueuedAccountClientId_ToItsRealAccountId_BeforePosting()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        _accountRepo.GetNextAccountNumberAsync(Arg.Any<Core.Enums.AccountType>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns("4000");
        var realAccountId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        _accountService.CreateAsync("Community Bank Account", Core.Enums.AccountType.Asset, true, Arg.Any<CancellationToken>())
            .Returns(new Account { Id = realAccountId, Name = "Community Bank Account", Type = Core.Enums.AccountType.Asset, IsBankAccount = true });
        var svc = CreateService();

        var request = ValidRequest() with
        {
            QueuedAccounts = [new QueuedAccountRequest(clientId, "Community Bank Account", Core.Enums.AccountType.Asset, true)],
            QueuedOpeningBalances = [new OpeningBalanceEntry { AccountId = clientId, Amount = 500m }],
            OpeningBalanceAsAtDate = new DateTime(2026, 7, 1)
        };
        await svc.InitializeAsync(request, Ct);

        await _openingBalanceService.Received(1).RecordOpeningBalancesAsync(
            Arg.Is<RecordOpeningBalancesRequest>(r =>
                r!.AsAtDate == new DateTime(2026, 7, 1)
                && r.Entries.Count == 1
                && r.Entries[0].AccountId == realAccountId // resolved from clientId, not the raw ClientId
                && r.Entries[0].Amount == 500m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitializeAsync_PassesThroughExistingAccountReference_Unchanged()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        _accountRepo.GetNextAccountNumberAsync(Arg.Any<Core.Enums.AccountType>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns("4000");
        var existingAccountId = Guid.NewGuid(); // not a QueuedAccounts ClientId — an already-real account
        var svc = CreateService();

        var request = ValidRequest() with
        {
            QueuedOpeningBalances = [new OpeningBalanceEntry { AccountId = existingAccountId, Amount = 200m }]
        };
        await svc.InitializeAsync(request, Ct);

        await _openingBalanceService.Received(1).RecordOpeningBalancesAsync(
            Arg.Is<RecordOpeningBalancesRequest>(r =>
                r!.Entries.Count == 1 && r.Entries[0].AccountId == existingAccountId && r.Entries[0].Amount == 200m),
            Arg.Any<CancellationToken>());
    }

    // spec 028, US6 / FR-018: opening balances entered during first-run setup are always
    // accepted. SetupService itself has no closed-through-date concept, and the real
    // ClosedPeriodGuard is a no-op here because no Settings row exists yet.
    [Fact]
    public async Task InitializeAsync_PostsQueuedOpeningBalances_RegardlessOfClosedPeriod()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        _accountRepo.GetNextAccountNumberAsync(Arg.Any<Core.Enums.AccountType>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns("4000");
        var accountId = Guid.NewGuid();
        var svc = CreateService();

        var request = ValidRequest() with
        {
            QueuedOpeningBalances = [new OpeningBalanceEntry { AccountId = accountId, Amount = 1000m }],
            OpeningBalanceAsAtDate = new DateTime(2000, 1, 1) // deliberately far in the past
        };

        await svc.InitializeAsync(request, Ct); // must not throw

        await _openingBalanceService.Received(1).RecordOpeningBalancesAsync(
            Arg.Is<RecordOpeningBalancesRequest>(r =>
                r!.Entries.Count == 1 && r.Entries[0].AccountId == accountId && r.Entries[0].Amount == 1000m),
            Arg.Any<CancellationToken>());
    }

    private static SetupRequest ValidRequest() => new(
        OrganizationName: "Test Org",
        AnnualFee: 75m,
        AttendanceFee: 5m,
        MembershipRenewalMonth: 1,
        IsTaxApplicable: false,
        TaxRate: null,
        AnnualFeeTaxCode: null,
        AttendanceFeeTaxCode: null,
        Theme: Theme.Dark);
}
