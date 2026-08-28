using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Tests.Fixtures;

namespace StageFright.Core.Tests.Modules.Finance;

/// <summary>
/// Unit tests for GeneralJournalService — manual multi-line journals posted verbatim,
/// balance/line validation, Member Receivable blocking, and audit logging.
/// </summary>
public class GeneralJournalServiceTests : TestBase
{
    private readonly IAccountRepository _accountRepo = Substitute.For<IAccountRepository>();
    private readonly IGLRepository _glRepo = Substitute.For<IGLRepository>();
    private readonly IJournalEntryRepository _journalRepo = Substitute.For<IJournalEntryRepository>();
    private readonly IAuditTrailService _audit = Substitute.For<IAuditTrailService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static readonly Guid CashAccountId = SystemAccounts.CashId;
    private static readonly Guid ExpenseAccountId = Guid.NewGuid();
    private static readonly Guid IncomeAccountId = Guid.NewGuid();

    private readonly GeneralJournalService _sut;

    public GeneralJournalServiceTests()
    {
        _unitOfWork
            .ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<Func<CancellationToken, Task>>(0)(ci.ArgAt<CancellationToken>(1)));

        _journalRepo.AddAsync(Arg.Any<JournalEntry>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<JournalEntry>(0));

        _accountRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Account>
            {
                MakeAccount(CashAccountId, "Cash on Hand", AccountType.Asset, "1100", isSystem: true, isBank: true),
                MakeAccount(SystemAccounts.MemberReceivableId, "Member Receivable", AccountType.Asset, "1200", isSystem: true),
                MakeAccount(ExpenseAccountId, "Hall Hire", AccountType.Expense, "6000"),
                MakeAccount(IncomeAccountId, "Raffle Income", AccountType.Income, "4000")
            });

        _sut = new GeneralJournalService(_accountRepo, _glRepo, _journalRepo, _audit, _unitOfWork, RealLocalizer.Instance);
    }

    // --- GetJournalAccountsAsync ---

    [Fact]
    public async Task Should_ExcludeMemberReceivable_When_GettingJournalAccounts()
    {
        var result = await _sut.GetJournalAccountsAsync(Ct);

        Assert.Equal(3, result.Count);
        Assert.DoesNotContain(result, a => a.Id == SystemAccounts.MemberReceivableId);
    }

    // --- Validation ---

    [Fact]
    public async Task Should_ThrowValidation_When_DescriptionMissing()
    {
        var request = MakeBalancedRequest();
        request.Description = "  ";

        await Assert.ThrowsAsync<ValidationException>(
            () => _sut.RecordJournalAsync(request, Ct));
    }

    [Fact]
    public async Task Should_ThrowValidation_When_FewerThanTwoLines()
    {
        var request = MakeRequest(new JournalLine { AccountId = ExpenseAccountId, DebitAmount = 100m });

        await Assert.ThrowsAsync<ValidationException>(
            () => _sut.RecordJournalAsync(request, Ct));
    }

    [Fact]
    public async Task Should_ThrowValidation_When_LineHasBothSidesNonZero()
    {
        var request = MakeRequest(
            new JournalLine { AccountId = ExpenseAccountId, DebitAmount = 100m, CreditAmount = 50m },
            new JournalLine { AccountId = CashAccountId, CreditAmount = 50m });

        await Assert.ThrowsAsync<ValidationException>(
            () => _sut.RecordJournalAsync(request, Ct));
    }

    [Fact]
    public async Task Should_ThrowValidation_When_LineHasBothSidesZero()
    {
        var request = MakeRequest(
            new JournalLine { AccountId = ExpenseAccountId, DebitAmount = 100m },
            new JournalLine { AccountId = CashAccountId, CreditAmount = 100m },
            new JournalLine { AccountId = IncomeAccountId });

        await Assert.ThrowsAsync<ValidationException>(
            () => _sut.RecordJournalAsync(request, Ct));
    }

    [Fact]
    public async Task Should_ThrowValidation_When_LineIsNegative()
    {
        var request = MakeRequest(
            new JournalLine { AccountId = ExpenseAccountId, DebitAmount = -100m },
            new JournalLine { AccountId = CashAccountId, CreditAmount = -100m });

        await Assert.ThrowsAsync<ValidationException>(
            () => _sut.RecordJournalAsync(request, Ct));
    }

    [Fact]
    public async Task Should_ThrowValidation_When_JournalIsOutOfBalance()
    {
        var request = MakeRequest(
            new JournalLine { AccountId = ExpenseAccountId, DebitAmount = 100m },
            new JournalLine { AccountId = CashAccountId, CreditAmount = 90m });

        await Assert.ThrowsAsync<ValidationException>(
            () => _sut.RecordJournalAsync(request, Ct));
    }

    [Fact]
    public async Task Should_ThrowValidation_When_LinePostsToMemberReceivable()
    {
        var request = MakeRequest(
            new JournalLine { AccountId = SystemAccounts.MemberReceivableId, DebitAmount = 100m },
            new JournalLine { AccountId = IncomeAccountId, CreditAmount = 100m });

        await Assert.ThrowsAsync<ValidationException>(
            () => _sut.RecordJournalAsync(request, Ct));
    }

    [Fact]
    public async Task Should_ThrowEntityNotFound_When_LineAccountDoesNotExist()
    {
        var request = MakeRequest(
            new JournalLine { AccountId = Guid.NewGuid(), DebitAmount = 100m },
            new JournalLine { AccountId = CashAccountId, CreditAmount = 100m });

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _sut.RecordJournalAsync(request, Ct));
    }

    // --- Posting ---

    [Fact]
    public async Task Should_PostLinesVerbatim_When_JournalIsBalanced()
    {
        await _sut.RecordJournalAsync(MakeBalancedRequest(), Ct);

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines =>
                lines!.Count == 2
                && lines.Any(t => t.DebitAmount == 100m && t.AccountId == ExpenseAccountId && t.GLAccount == "6000")
                && lines.Any(t => t.CreditAmount == 100m && t.AccountId == CashAccountId && t.GLAccount == "1100")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_PostMultiLineJournal_When_MoreThanTwoLinesBalance()
    {
        var request = MakeRequest(
            new JournalLine { AccountId = ExpenseAccountId, DebitAmount = 60m },
            new JournalLine { AccountId = IncomeAccountId, DebitAmount = 40m },
            new JournalLine { AccountId = CashAccountId, CreditAmount = 100m });

        await _sut.RecordJournalAsync(request, Ct);

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines => lines!.Count == 3),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_CreateGeneralJournalEntry_When_JournalPosted()
    {
        var request = MakeBalancedRequest();

        await _sut.RecordJournalAsync(request, Ct);

        await _journalRepo.Received(1).AddAsync(
            Arg.Is<JournalEntry>(j =>
                j!.Type == JournalEntryType.GeneralJournal
                && j.Date == request.Date
                && j.Description == "Correction"),
            Arg.Any<CancellationToken>());

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines => lines!.All(t => t.JournalEntryId != null)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_RunInsideUnitOfWork_When_JournalPosted()
    {
        await _sut.RecordJournalAsync(MakeBalancedRequest(), Ct);

        await _unitOfWork.Received(1).ExecuteInTransactionAsync(
            Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_WriteAuditEntry_When_JournalPosted()
    {
        await _sut.RecordJournalAsync(MakeBalancedRequest(), Ct);

        await _audit.Received(1).LogAsync(
            Arg.Any<string>(), Arg.Any<Guid>(), AuditAction.Create,
            Arg.Any<string?>(), Arg.Any<string?>(),
            ct: Arg.Any<CancellationToken>());
    }

    // --- Helpers ---

    private static RecordJournalRequest MakeBalancedRequest() =>
        MakeRequest(
            new JournalLine { AccountId = ExpenseAccountId, DebitAmount = 100m },
            new JournalLine { AccountId = CashAccountId, CreditAmount = 100m });

    private static RecordJournalRequest MakeRequest(params JournalLine[] lines) =>
        new()
        {
            Date = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            Description = "Correction",
            Lines = lines.ToList()
        };

    private static Account MakeAccount(
        Guid id, string name, AccountType type, string number,
        bool isSystem = false, bool isBank = false) => new()
    {
        Id = id, Name = name, Type = type,
        AccountNumber = number, IsSystem = isSystem, IsBankAccount = isBank, SortOrder = 0,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };
}
