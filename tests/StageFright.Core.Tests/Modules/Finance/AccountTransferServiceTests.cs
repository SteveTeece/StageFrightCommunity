using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Tests.Fixtures;

namespace StageFright.Core.Tests.Modules.Finance;

/// <summary>
/// Unit tests for AccountTransferService — bank-to-bank transfers with a journal
/// entry + balanced GL set (DR To / CR From), bank-only + from≠to validation,
/// and audit logging.
/// </summary>
public class AccountTransferServiceTests : TestBase
{
    private readonly IAccountRepository _accountRepo = Substitute.For<IAccountRepository>();
    private readonly IGLRepository _glRepo = Substitute.For<IGLRepository>();
    private readonly IJournalEntryRepository _journalRepo = Substitute.For<IJournalEntryRepository>();
    private readonly IAuditTrailService _audit = Substitute.For<IAuditTrailService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static readonly Guid CashAccountId = SystemAccounts.CashId;
    private static readonly Guid BankAccountId = Guid.NewGuid();
    private static readonly Guid NonBankAssetAccountId = Guid.NewGuid();

    private readonly AccountTransferService _sut;

    public AccountTransferServiceTests()
    {
        _unitOfWork
            .ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<Func<CancellationToken, Task>>(0)(ci.ArgAt<CancellationToken>(1)));

        _journalRepo.AddAsync(Arg.Any<JournalEntry>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<JournalEntry>(0));

        _accountRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Account>
            {
                MakeAccount(CashAccountId, "Cash on Hand", "1100", isBank: true, isSystem: true),
                MakeAccount(BankAccountId, "Operating Account", "1110", isBank: true),
                MakeAccount(NonBankAssetAccountId, "Equipment", "1300")
            });

        _sut = new AccountTransferService(_accountRepo, _glRepo, _journalRepo, _audit, _unitOfWork);
    }

    // --- Validation ---

    [Fact]
    public async Task Should_ThrowValidation_When_AmountIsZero()
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => _sut.RecordTransferAsync(MakeRequest(0m), Ct));
    }

    [Fact]
    public async Task Should_ThrowValidation_When_AmountIsNegative()
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => _sut.RecordTransferAsync(MakeRequest(-10m), Ct));
    }

    [Fact]
    public async Task Should_ThrowValidation_When_SourceEqualsDestination()
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => _sut.RecordTransferAsync(MakeRequest(50m, from: BankAccountId, to: BankAccountId), Ct));
    }

    [Fact]
    public async Task Should_ThrowEntityNotFound_When_SourceAccountDoesNotExist()
    {
        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _sut.RecordTransferAsync(MakeRequest(50m, from: Guid.NewGuid()), Ct));
    }

    [Fact]
    public async Task Should_ThrowEntityNotFound_When_DestinationAccountDoesNotExist()
    {
        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _sut.RecordTransferAsync(MakeRequest(50m, to: Guid.NewGuid()), Ct));
    }

    [Fact]
    public async Task Should_ThrowValidation_When_SourceIsNotABankAccount()
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => _sut.RecordTransferAsync(MakeRequest(50m, from: NonBankAssetAccountId), Ct));
    }

    [Fact]
    public async Task Should_ThrowValidation_When_DestinationIsNotABankAccount()
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => _sut.RecordTransferAsync(MakeRequest(50m, to: NonBankAssetAccountId), Ct));
    }

    // --- Posting ---

    [Fact]
    public async Task Should_PostDebitDestinationCreditSource_When_TransferRecorded()
    {
        await _sut.RecordTransferAsync(MakeRequest(200m), Ct);

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines =>
                lines.Count == 2
                && lines.Any(t => t.DebitAmount == 200m && t.AccountId == BankAccountId && t.GLAccount == "1110")
                && lines.Any(t => t.CreditAmount == 200m && t.AccountId == CashAccountId && t.GLAccount == "1100")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_CreateTransferJournalEntry_When_TransferRecorded()
    {
        var request = MakeRequest(200m);

        await _sut.RecordTransferAsync(request, Ct);

        await _journalRepo.Received(1).AddAsync(
            Arg.Is<JournalEntry>(j => j.Type == JournalEntryType.Transfer && j.Date == request.Date),
            Arg.Any<CancellationToken>());

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines => lines.All(t => t.JournalEntryId != null)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_DefaultDescriptionToAccountNames_When_NoDescriptionProvided()
    {
        await _sut.RecordTransferAsync(MakeRequest(200m), Ct);

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines =>
                lines.All(t => t.Description!.Contains("Cash on Hand") && t.Description.Contains("Operating Account"))),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_RunInsideUnitOfWork_When_TransferRecorded()
    {
        await _sut.RecordTransferAsync(MakeRequest(200m), Ct);

        await _unitOfWork.Received(1).ExecuteInTransactionAsync(
            Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_WriteAuditEntry_When_TransferRecorded()
    {
        await _sut.RecordTransferAsync(MakeRequest(200m), Ct);

        await _audit.Received(1).LogAsync(
            Arg.Any<string>(), Arg.Any<Guid>(), AuditAction.Create,
            Arg.Any<string?>(), Arg.Any<string?>(),
            ct: Arg.Any<CancellationToken>());
    }

    // --- Helpers ---

    private static RecordTransferRequest MakeRequest(decimal amount, Guid? from = null, Guid? to = null) =>
        new()
        {
            Date = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            Amount = amount,
            FromAccountId = from ?? CashAccountId,
            ToAccountId = to ?? BankAccountId
        };

    private static Account MakeAccount(
        Guid id, string name, string number, bool isBank = false, bool isSystem = false) => new()
    {
        Id = id, Name = name, Type = AccountType.Asset,
        AccountNumber = number, IsSystem = isSystem, IsBankAccount = isBank, SortOrder = 0,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };
}
