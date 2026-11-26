using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Tests.Fixtures;

namespace StageFright.Core.Tests.Modules.Finance;

/// <summary>
/// Unit tests for IncomeEntryService — non-member income recording with a journal
/// entry + balanced GL set, payment-method routing (spec 033: cash to Petty Cash 1100,
/// cheque/other to Undeposited Funds 1105 with a received item, card/EFT to the chosen bank),
/// account validation, amount validation, and audit logging.
/// </summary>
public class IncomeEntryServiceTests : TestBase
{
    private readonly IAccountRepository _accountRepo = Substitute.For<IAccountRepository>();
    private readonly IGLRepository _glRepo = Substitute.For<IGLRepository>();
    private readonly IJournalEntryRepository _journalRepo = Substitute.For<IJournalEntryRepository>();
    private readonly ISettingsRepository _settingsRepo = Substitute.For<ISettingsRepository>();
    private readonly IAuditTrailService _audit = Substitute.For<IAuditTrailService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IExternalOrganisationRepository _organisationRepo = Substitute.For<IExternalOrganisationRepository>();
    private readonly IReceivedItemRepository _receivedItemRepo = Substitute.For<IReceivedItemRepository>();

    private static readonly Guid ActiveOrganisationId = Guid.NewGuid();
    private static readonly Guid ArchivedOrganisationId = Guid.NewGuid();

    private static readonly Guid IncomeAccountId = Guid.NewGuid();
    private static readonly Guid ExpenseAccountId = Guid.NewGuid();
    private static readonly Guid SystemIncomeAccountId = Guid.NewGuid();
    private static readonly Guid BankAccountId = Guid.NewGuid();
    private static readonly Guid CashAccountId = SystemAccounts.CashId;
    private static readonly Guid UndepositedFundsId = SystemAccounts.UndepositedFundsId;

    private readonly IncomeEntryService _sut;

    public IncomeEntryServiceTests()
    {
        _unitOfWork
            .ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<Func<CancellationToken, Task>>(0)(ci.ArgAt<CancellationToken>(1)));

        _journalRepo.AddAsync(Arg.Any<JournalEntry>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<JournalEntry>(0));

        _accountRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Account>
            {
                MakeAccount(IncomeAccountId, "Raffle Income", AccountType.Income, "4000"),
                MakeAccount(ExpenseAccountId, "Hall Hire", AccountType.Expense, "6000"),
                MakeAccount(SystemIncomeAccountId, "System Income", AccountType.Income, "4999", isSystem: true),
                MakeAccount(CashAccountId, "Petty Cash", AccountType.Asset, "1100", isSystem: true, isBank: true),
                MakeAccount(UndepositedFundsId, "Undeposited Funds", AccountType.Asset, "1105", isSystem: true),
                MakeAccount(BankAccountId, "Operating Account", AccountType.Asset, "1110", isBank: true)
            });

        _organisationRepo.GetByIdIncludingArchivedAsync(ActiveOrganisationId, Arg.Any<CancellationToken>())
            .Returns(MakeOrganisation(ActiveOrganisationId, "Town Hall", archived: false));
        _organisationRepo.GetByIdIncludingArchivedAsync(ArchivedOrganisationId, Arg.Any<CancellationToken>())
            .Returns(MakeOrganisation(ArchivedOrganisationId, "Old Venue", archived: true));

        _receivedItemRepo.AddAsync(Arg.Any<ReceivedItem>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<ReceivedItem>(0));

        _sut = new IncomeEntryService(_accountRepo, _glRepo, _journalRepo, _settingsRepo, _audit, _unitOfWork, RealLocalizer.Instance, _organisationRepo, _receivedItemRepo);
    }

    // --- GetIncomeAccountsAsync ---

    [Fact]
    public async Task GetIncomeAccountsAsync_ReturnsOnlyNonSystemIncomeAccounts()
    {
        var result = await _sut.GetIncomeAccountsAsync(Ct);

        Assert.Single(result);
        Assert.Equal(IncomeAccountId, result[0].Id);
    }

    [Fact]
    public async Task GetIncomeAccountsAsync_ExcludesExpenseAccounts()
    {
        var result = await _sut.GetIncomeAccountsAsync(Ct);

        Assert.DoesNotContain(result, c => c.Type == AccountType.Expense);
    }

    [Fact]
    public async Task GetIncomeAccountsAsync_ExcludesSystemAccounts()
    {
        var result = await _sut.GetIncomeAccountsAsync(Ct);

        Assert.DoesNotContain(result, c => c.IsSystem);
    }

    // --- RecordIncomeAsync: validation ---

    [Fact]
    public async Task RecordIncomeAsync_ThrowsValidation_WhenAmountIsZero()
    {
        var request = MakeRequest(0m);

        await Assert.ThrowsAsync<ValidationException>(
            () => _sut.RecordIncomeAsync(request, Ct));
    }

    [Fact]
    public async Task RecordIncomeAsync_ThrowsValidation_WhenAmountIsNegative()
    {
        var request = MakeRequest(-5m);

        await Assert.ThrowsAsync<ValidationException>(
            () => _sut.RecordIncomeAsync(request, Ct));
    }

    [Fact]
    public async Task RecordIncomeAsync_ThrowsEntityNotFound_WhenAccountDoesNotExist()
    {
        var request = MakeRequest(100m, Guid.NewGuid());

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _sut.RecordIncomeAsync(request, Ct));
    }

    [Fact]
    public async Task RecordIncomeAsync_ThrowsValidation_WhenAccountIsExpenseType()
    {
        var request = MakeRequest(100m, ExpenseAccountId);

        await Assert.ThrowsAsync<ValidationException>(
            () => _sut.RecordIncomeAsync(request, Ct));
    }

    [Fact]
    public async Task RecordIncomeAsync_ThrowsValidation_WhenAccountIsSystemAccount()
    {
        var request = MakeRequest(100m, SystemIncomeAccountId);

        await Assert.ThrowsAsync<ValidationException>(
            () => _sut.RecordIncomeAsync(request, Ct));
    }

    // --- RecordIncomeAsync: GL posting ---

    [Fact]
    public async Task RecordIncomeAsync_PostsBalancedSet_DebitCashCreditIncome()
    {
        var request = MakeRequest(250m);

        await _sut.RecordIncomeAsync(request, Ct);

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines =>
                lines!.Count == 2
                && lines.Any(t => t.DebitAmount == 250m && t.AccountId == CashAccountId && t.GLAccount == "1100")
                && lines.Any(t => t.CreditAmount == 250m && t.AccountId == IncomeAccountId)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordIncomeAsync_CreatesIncomeJournalEntry_AndLinksLines()
    {
        var request = MakeRequest(100m);

        await _sut.RecordIncomeAsync(request, Ct);

        await _journalRepo.Received(1).AddAsync(
            Arg.Is<JournalEntry>(j => j!.Type == JournalEntryType.Income && j.Date == request.Date),
            Arg.Any<CancellationToken>());

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines => lines!.All(t => t.JournalEntryId != null)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordIncomeAsync_GLSet_HasNoMemberOrFeeOrPaymentLinks()
    {
        var request = MakeRequest(100m);

        await _sut.RecordIncomeAsync(request, Ct);

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines =>
                lines!.All(t => t.MemberId == null && t.FeeId == null && t.PaymentId == null)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordIncomeAsync_GLSet_UsesRequestDate()
    {
        var date = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc);
        var request = new RecordIncomeRequest
        {
            Date = date, Amount = 100m, AccountId = IncomeAccountId
        };

        await _sut.RecordIncomeAsync(request, Ct);

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines => lines!.All(t => t.Date == date)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordIncomeAsync_GLSet_UsesDescriptionWhenProvided()
    {
        var request = new RecordIncomeRequest
        {
            Date = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            Amount = 100m, AccountId = IncomeAccountId,
            Description = "Christmas Raffle"
        };

        await _sut.RecordIncomeAsync(request, Ct);

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines => lines!.All(t => t.Description == "Christmas Raffle")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordIncomeAsync_GLSet_DefaultsDescriptionToAccount_WhenNotProvided()
    {
        var request = new RecordIncomeRequest
        {
            Date = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            Amount = 100m, AccountId = IncomeAccountId, Description = null
        };

        await _sut.RecordIncomeAsync(request, Ct);

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines => lines!.All(t => t.Description!.Contains("Raffle Income"))),
            Arg.Any<CancellationToken>());
    }

    // --- RecordIncomeAsync: GST ---

    [Fact]
    public async Task RecordIncomeAsync_NotRegistered_PostsTwoLines_WithNullTaxCode_EvenWhenTaxCodeRequested()
    {
        // Toggle-off byte-identical regression: unregistered orgs must always get the
        // pre-GST 2-line posting with TaxCode == null, regardless of what the caller asks for.
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        var request = MakeRequest(110m);
        request.TaxCode = TaxCode.Taxable;

        await _sut.RecordIncomeAsync(request, Ct);

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines =>
                lines!.Count == 2
                && lines.All(t => t.TaxCode == null)
                && lines.Any(t => t.DebitAmount == 110m)
                && lines.Any(t => t.CreditAmount == 110m)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordIncomeAsync_RegisteredAndTaxable_PostsThreeLines_SplittingTaxToClearing()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>())
            .Returns(MakeSettings(isTaxApplicable: true));
        var request = MakeRequest(110m);
        request.TaxCode = TaxCode.Taxable;

        await _sut.RecordIncomeAsync(request, Ct);

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines =>
                lines!.Count == 3
                && lines.Any(t => t.DebitAmount == 110m && t.AccountId == CashAccountId)
                && lines.Any(t => t.CreditAmount == 100m && t.AccountId == IncomeAccountId)
                && lines.Any(t => t.CreditAmount == 10m && t.AccountId == SystemAccounts.TaxCollectedId)
                && lines.All(t => t.TaxCode == TaxCode.Taxable)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordIncomeAsync_TaxableAndExclusiveMode_AddsTaxOnTop_BankTakesGross_IncomeTakesNet()
    {
        // issue #354: in Exclusive mode request.Amount is the net; tax is added on top so the
        // bank line carries the gross while the income line keeps the entered net.
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>())
            .Returns(MakeSettings(isTaxApplicable: true, taxEntryMode: TaxEntryMode.Exclusive));
        var request = MakeRequest(100m);
        request.TaxCode = TaxCode.Taxable;

        await _sut.RecordIncomeAsync(request, Ct);

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines =>
                lines!.Count == 3
                && lines.Any(t => t.DebitAmount == 110m && t.AccountId == CashAccountId)
                && lines.Any(t => t.CreditAmount == 100m && t.AccountId == IncomeAccountId)
                && lines.Any(t => t.CreditAmount == 10m && t.AccountId == SystemAccounts.TaxCollectedId)
                && lines.Sum(t => t.DebitAmount) == lines.Sum(t => t.CreditAmount)
                && lines.All(t => t.TaxCode == TaxCode.Taxable)),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(TaxCode.TaxExempt)]
    [InlineData(TaxCode.Excluded)]
    public async Task RecordIncomeAsync_RegisteredButNotTaxable_PostsTwoLines_NoTaxSplit(TaxCode code)
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>())
            .Returns(MakeSettings(isTaxApplicable: true));
        var request = MakeRequest(110m);
        request.TaxCode = code;

        await _sut.RecordIncomeAsync(request, Ct);

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines =>
                lines!.Count == 2
                && lines.Any(t => t.DebitAmount == 110m)
                && lines.Any(t => t.CreditAmount == 110m)
                && lines.All(t => t.TaxCode == code)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordIncomeAsync_RegisteredNoTaxCodeProvided_DefaultsToTaxFree()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>())
            .Returns(MakeSettings(isTaxApplicable: true));
        var request = MakeRequest(110m); // TaxCode left null

        await _sut.RecordIncomeAsync(request, Ct);

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines =>
                lines!.Count == 2 && lines.All(t => t.TaxCode == TaxCode.TaxExempt)),
            Arg.Any<CancellationToken>());
    }

    // --- RecordIncomeAsync: unit of work + audit ---

    [Fact]
    public async Task RecordIncomeAsync_RunsInsideUnitOfWork()
    {
        await _sut.RecordIncomeAsync(MakeRequest(100m), Ct);

        await _unitOfWork.Received(1).ExecuteInTransactionAsync(
            Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordIncomeAsync_WritesAuditEntry()
    {
        await _sut.RecordIncomeAsync(MakeRequest(100m), Ct);

        await _audit.Received(1).LogAsync(
            Arg.Any<string>(), Arg.Any<Guid>(), AuditAction.Create,
            Arg.Any<string?>(), Arg.Any<string?>(),
            ct: Arg.Any<CancellationToken>());
    }

    // --- External organisation link (spec 032 US3) ---

    [Fact]
    public async Task Should_StampOrganisationOnJournalEntry_When_ActiveOrganisationChosen()
    {
        var request = MakeRequest(80m);
        request.ExternalOrganisationId = ActiveOrganisationId;

        await _sut.RecordIncomeAsync(request, Ct);

        await _journalRepo.Received(1).AddAsync(
            Arg.Is<JournalEntry>(j => j!.Type == JournalEntryType.Income && j.ExternalOrganisationId == ActiveOrganisationId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_LeaveJournalEntryUnlinkedAndSkipLookup_When_NoOrganisationChosen()
    {
        await _sut.RecordIncomeAsync(MakeRequest(80m), Ct);

        await _journalRepo.Received(1).AddAsync(
            Arg.Is<JournalEntry>(j => j!.ExternalOrganisationId == null),
            Arg.Any<CancellationToken>());
        await _organisationRepo.DidNotReceiveWithAnyArgs().GetByIdIncludingArchivedAsync(default, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(TaxCode.Taxable)]
    [InlineData(TaxCode.TaxExempt)]
    public async Task Should_PostIdenticalLines_When_OrganisationChosenOrNot(TaxCode code)
    {
        // SC-004: choosing an organisation changes no amount, account, tax code or description.
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns(MakeSettings(isTaxApplicable: true));
        var posted = new List<IReadOnlyList<Transaction>>();
        _glRepo.When(r => r.AddBalancedSetAsync(Arg.Any<IReadOnlyList<Transaction>>(), Arg.Any<CancellationToken>()))
            .Do(ci => posted.Add(ci.ArgAt<IReadOnlyList<Transaction>>(0)));

        var unlinked = MakeRequest(110m);
        unlinked.TaxCode = code;
        var linked = MakeRequest(110m);
        linked.TaxCode = code;
        linked.ExternalOrganisationId = ActiveOrganisationId;

        await _sut.RecordIncomeAsync(unlinked, Ct);
        await _sut.RecordIncomeAsync(linked, Ct);

        Assert.Equal(2, posted.Count);
        Assert.Equal(Project(posted[0]), Project(posted[1]));
    }

    [Fact]
    public async Task Should_ThrowValidationAndPostNothing_When_OrganisationArchived()
    {
        var request = MakeRequest(80m);
        request.ExternalOrganisationId = ArchivedOrganisationId;

        await Assert.ThrowsAsync<ValidationException>(() => _sut.RecordIncomeAsync(request, Ct));

        await _unitOfWork.DidNotReceiveWithAnyArgs().ExecuteInTransactionAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Should_ThrowEntityNotFoundAndPostNothing_When_OrganisationUnknown()
    {
        var request = MakeRequest(80m);
        request.ExternalOrganisationId = Guid.NewGuid();

        await Assert.ThrowsAsync<EntityNotFoundException>(() => _sut.RecordIncomeAsync(request, Ct));

        await _unitOfWork.DidNotReceiveWithAnyArgs().ExecuteInTransactionAsync(default!, TestContext.Current.CancellationToken);
    }

    // --- Payment-method routing (spec 033 research §6) ---

    [Theory]
    [InlineData(PaymentMethod.Cash, TaxEntryMode.Inclusive, 110)]
    [InlineData(PaymentMethod.Cash, TaxEntryMode.Exclusive, 100)]
    [InlineData(PaymentMethod.Check, TaxEntryMode.Inclusive, 110)]
    [InlineData(PaymentMethod.Check, TaxEntryMode.Exclusive, 100)]
    [InlineData(PaymentMethod.Other, TaxEntryMode.Inclusive, 110)]
    [InlineData(PaymentMethod.Other, TaxEntryMode.Exclusive, 100)]
    [InlineData(PaymentMethod.Card, TaxEntryMode.Inclusive, 110)]
    [InlineData(PaymentMethod.Card, TaxEntryMode.Exclusive, 100)]
    [InlineData(PaymentMethod.ElectronicTransfer, TaxEntryMode.Inclusive, 110)]
    [InlineData(PaymentMethod.ElectronicTransfer, TaxEntryMode.Exclusive, 100)]
    public async Task Should_DebitRoutedAccountWithGross_When_TaxableIncomeRecordedByMethod(
        PaymentMethod method, TaxEntryMode mode, int entered)
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>())
            .Returns(MakeSettings(isTaxApplicable: true, taxEntryMode: mode));
        var request = MakeRequest(entered, method: method, bankAccountId: BankAccountId);
        request.TaxCode = TaxCode.Taxable;
        var (expectedAccount, expectedNumber) = ExpectedDestination(method);

        await _sut.RecordIncomeAsync(request, Ct);

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines =>
                lines!.Count == 3
                && lines.Any(t => t.DebitAmount == 110m && t.AccountId == expectedAccount && t.GLAccount == expectedNumber)
                && lines.Any(t => t.CreditAmount == 100m && t.AccountId == IncomeAccountId)
                && lines.Any(t => t.CreditAmount == 10m && t.AccountId == SystemAccounts.TaxCollectedId)
                && lines.Sum(t => t.DebitAmount) == lines.Sum(t => t.CreditAmount)),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(PaymentMethod.Check, TaxEntryMode.Inclusive, 110)]
    [InlineData(PaymentMethod.Check, TaxEntryMode.Exclusive, 100)]
    [InlineData(PaymentMethod.Other, TaxEntryMode.Inclusive, 110)]
    [InlineData(PaymentMethod.Other, TaxEntryMode.Exclusive, 100)]
    public async Task Should_AddOneReceivedItemForGross_When_ChequeOrOtherTaxableIncome(
        PaymentMethod method, TaxEntryMode mode, int entered)
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>())
            .Returns(MakeSettings(isTaxApplicable: true, taxEntryMode: mode));
        var request = MakeRequest(entered, method: method);
        request.TaxCode = TaxCode.Taxable;

        await _sut.RecordIncomeAsync(request, Ct);

        await _receivedItemRepo.Received(1).AddAsync(
            Arg.Is<ReceivedItem>(i => i!.Amount == 110m && i.Method == method),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(PaymentMethod.Cash)]
    [InlineData(PaymentMethod.Card)]
    [InlineData(PaymentMethod.ElectronicTransfer)]
    public async Task Should_AddNoReceivedItem_When_MethodIsCashCardOrTransfer(PaymentMethod method)
    {
        await _sut.RecordIncomeAsync(MakeRequest(100m, method: method, bankAccountId: BankAccountId, reference: "123"), Ct);

        await _receivedItemRepo.DidNotReceiveWithAnyArgs().AddAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Should_DebitPettyCash_When_PaymentMethodLeftAtDefault()
    {
        var request = new RecordIncomeRequest
        {
            Date = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            Amount = 40m, AccountId = IncomeAccountId
        };

        await _sut.RecordIncomeAsync(request, Ct);

        Assert.Equal(PaymentMethod.Cash, request.PaymentMethod);
        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines =>
                lines!.Any(t => t.DebitAmount == 40m && t.AccountId == CashAccountId && t.GLAccount == "1100")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_LinkReceivedItemToJournalEntry_When_ChequeIncomeRecorded()
    {
        JournalEntry? entry = null;
        _journalRepo.AddAsync(Arg.Any<JournalEntry>(), Arg.Any<CancellationToken>())
            .Returns(ci => entry = ci.ArgAt<JournalEntry>(0));
        var request = MakeRequest(75m, method: PaymentMethod.Check, reference: "  004512  ");

        await _sut.RecordIncomeAsync(request, Ct);

        Assert.NotNull(entry);
        await _receivedItemRepo.Received(1).AddAsync(
            Arg.Is<ReceivedItem>(i =>
                i!.JournalEntryId == entry!.Id
                && i.PaymentId == null
                && i.Date == request.Date
                && i.Amount == 75m
                && i.Method == PaymentMethod.Check
                && i.Reference == "004512"),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Should_StoreNullReference_When_ReferenceBlank(string? reference)
    {
        await _sut.RecordIncomeAsync(MakeRequest(75m, method: PaymentMethod.Other, reference: reference), Ct);

        await _receivedItemRepo.Received(1).AddAsync(
            Arg.Is<ReceivedItem>(i => i!.Reference == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_AcceptReference_When_ReferenceIsExactly50Characters()
    {
        var reference = new string('7', 50);

        await _sut.RecordIncomeAsync(MakeRequest(75m, method: PaymentMethod.Check, reference: reference), Ct);

        await _receivedItemRepo.Received(1).AddAsync(
            Arg.Is<ReceivedItem>(i => i!.Reference == reference),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_ThrowValidationAndPostNothing_When_ChequeReferenceLongerThan50()
    {
        var request = MakeRequest(75m, method: PaymentMethod.Check, reference: new string('7', 51));

        await Assert.ThrowsAsync<ValidationException>(() => _sut.RecordIncomeAsync(request, Ct));

        await _unitOfWork.DidNotReceiveWithAnyArgs().ExecuteInTransactionAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Should_IgnoreLongReference_When_MethodIsCash()
    {
        await _sut.RecordIncomeAsync(MakeRequest(75m, reference: new string('7', 51)), Ct);

        await _glRepo.Received(1).AddBalancedSetAsync(Arg.Any<IReadOnlyList<Transaction>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_IgnoreBankAccount_When_MethodIsCheque()
    {
        await _sut.RecordIncomeAsync(MakeRequest(60m, method: PaymentMethod.Check, bankAccountId: BankAccountId), Ct);

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines =>
                lines!.Any(t => t.DebitAmount == 60m && t.AccountId == UndepositedFundsId && t.GLAccount == "1105")
                && lines!.All(t => t.AccountId != BankAccountId)),
            Arg.Any<CancellationToken>());
    }

    public static TheoryData<PaymentMethod, Guid?> InvalidBankAccounts() => new()
    {
        { PaymentMethod.Card, null },
        { PaymentMethod.ElectronicTransfer, null },
        { PaymentMethod.Card, Guid.NewGuid() },
        { PaymentMethod.ElectronicTransfer, Guid.NewGuid() },
        { PaymentMethod.Card, ExpenseAccountId },
        { PaymentMethod.ElectronicTransfer, UndepositedFundsId },
        { PaymentMethod.Card, CashAccountId },
        { PaymentMethod.ElectronicTransfer, CashAccountId }
    };

    [Theory]
    [MemberData(nameof(InvalidBankAccounts))]
    public async Task Should_ThrowValidationAndPostNothing_When_CardOrTransferBankAccountMissingOrInvalid(
        PaymentMethod method, Guid? bankAccountId)
    {
        var request = MakeRequest(60m, method: method, bankAccountId: bankAccountId);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _sut.RecordIncomeAsync(request, Ct));

        Assert.Equal(RealLocalizer.Instance.Get<StageFright.Core.Modules.Localization.Resources.ValidationResource>(
            "Validation_Receipt_BankAccountRequired"), ex.Message);
        await _unitOfWork.DidNotReceiveWithAnyArgs().ExecuteInTransactionAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Should_AuditAgainstJournalEntry_When_IncomeRecorded()
    {
        JournalEntry? entry = null;
        _journalRepo.AddAsync(Arg.Any<JournalEntry>(), Arg.Any<CancellationToken>())
            .Returns(ci => entry = ci.ArgAt<JournalEntry>(0));

        await _sut.RecordIncomeAsync(MakeRequest(60m, method: PaymentMethod.ElectronicTransfer, bankAccountId: BankAccountId), Ct);

        Assert.NotNull(entry);
        await _audit.Received(1).LogAsync(
            nameof(JournalEntry), entry!.Id, AuditAction.Create,
            Arg.Any<string?>(),
            Arg.Is<string?>(v => v!.Contains("Operating Account") && v.Contains("Raffle Income")),
            ct: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_KeepOrganisationOnEntry_When_ChequeIncomeFromOrganisation()
    {
        // FR-025: the organisation link stays on the journal entry; the item points at that entry.
        JournalEntry? entry = null;
        _journalRepo.AddAsync(Arg.Any<JournalEntry>(), Arg.Any<CancellationToken>())
            .Returns(ci => entry = ci.ArgAt<JournalEntry>(0));
        var request = MakeRequest(300m, method: PaymentMethod.Check, reference: "889");
        request.ExternalOrganisationId = ActiveOrganisationId;

        await _sut.RecordIncomeAsync(request, Ct);

        Assert.Equal(ActiveOrganisationId, entry!.ExternalOrganisationId);
        await _receivedItemRepo.Received(1).AddAsync(
            Arg.Is<ReceivedItem>(i => i!.JournalEntryId == entry.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_AddNoReceivedItem_When_GLPostingFails()
    {
        _glRepo.AddBalancedSetAsync(Arg.Any<IReadOnlyList<Transaction>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new GLBalanceException("unbalanced", nameof(Transaction), "test")));

        await Assert.ThrowsAsync<GLBalanceException>(
            () => _sut.RecordIncomeAsync(MakeRequest(60m, method: PaymentMethod.Check), Ct));

        await _receivedItemRepo.DidNotReceiveWithAnyArgs().AddAsync(default!, TestContext.Current.CancellationToken);
    }

    // --- Helpers ---

    private static (Guid AccountId, string Number) ExpectedDestination(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => (CashAccountId, "1100"),
        PaymentMethod.Check or PaymentMethod.Other => (UndepositedFundsId, "1105"),
        _ => (BankAccountId, "1110")
    };

    private static List<string> Project(IReadOnlyList<Transaction> lines) => lines
        .Select(t => $"{t.AccountId}|{t.GLAccount}|{t.DebitAmount}|{t.CreditAmount}|{t.TaxCode}|{t.Description}|{t.Date:O}")
        .ToList();

    private static ExternalOrganisation MakeOrganisation(Guid id, string name, bool archived) => new()
    {
        Id = id, Name = name, IsDeleted = archived,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static RecordIncomeRequest MakeRequest(
        decimal amount, Guid? accountId = null,
        PaymentMethod method = PaymentMethod.Cash, Guid? bankAccountId = null, string? reference = null) =>
        new()
        {
            Date = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            Amount = amount,
            AccountId = accountId ?? IncomeAccountId,
            PaymentMethod = method,
            BankAccountId = bankAccountId,
            Reference = reference,
            Description = null
        };

    private static Account MakeAccount(
        Guid id, string name, AccountType type, string number,
        bool isSystem = false, bool isBank = false) => new()
    {
        Id = id, Name = name, Type = type,
        AccountNumber = number, IsSystem = isSystem, IsBankAccount = isBank, SortOrder = 0,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static Settings MakeSettings(bool isTaxApplicable, TaxEntryMode taxEntryMode = TaxEntryMode.Inclusive) => new()
    {
        Id = Guid.NewGuid(), OrganizationName = "Test Choir",
        AnnualFee = 50m, AttendanceFee = 10m,
        MembershipRenewalMonth = 1, MaxAgeRangeYears = 150,
        MinimumMemberAge = 0, SchemaVersion = "1.1.0",
        IsTaxApplicable = isTaxApplicable, TaxRate = isTaxApplicable ? 10m : null,
        TaxEntryMode = taxEntryMode,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };
}
