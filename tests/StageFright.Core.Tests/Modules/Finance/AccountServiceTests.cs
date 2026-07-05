using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Modules.Settings;
using StageFright.Core.Tests.Fixtures;

namespace StageFright.Core.Tests.Modules.Finance;

/// <summary>
/// Unit tests for AccountService — creation with GL assignment, archive blocking (referenced or system),
/// restore, reorder, and audit entries.
/// </summary>
public class AccountServiceTests : TestBase
{
    private readonly IAccountRepository _repo = Substitute.For<IAccountRepository>();
    private readonly IAuditTrailService _audit = Substitute.For<IAuditTrailService>();
    private readonly AccountNumberAssignmentService _glAssignment;
    private readonly AccountService _sut;

    private static readonly Guid IncomeAccountId = Guid.NewGuid();
    private static readonly Guid ExpenseAccountId = Guid.NewGuid();
    private static readonly Guid SystemAccountId = new("00000000-0000-0000-0000-000000000001");

    public AccountServiceTests()
    {
        var accountRepoForGl = Substitute.For<IAccountRepository>();
        accountRepoForGl.GetNextAccountNumberAsync(AccountType.Income, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns("4000");
        accountRepoForGl.GetNextAccountNumberAsync(AccountType.Expense, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns("6000");
        accountRepoForGl.GetNextAccountNumberAsync(AccountType.Asset, true, Arg.Any<CancellationToken>())
            .Returns("1110");
        accountRepoForGl.GetNextAccountNumberAsync(AccountType.Asset, false, Arg.Any<CancellationToken>())
            .Returns("1300");
        _glAssignment = new AccountNumberAssignmentService(accountRepoForGl);

        _repo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Account>());
        _repo.AddAsync(Arg.Any<Account>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<Account>(0));

        _sut = new AccountService(_repo, _glAssignment, _audit);
    }

    // --- CreateAsync ---

    [Fact]
    public async Task CreateAsync_Income_AssignsNextAccountNumber()
    {
        var result = await _sut.CreateAsync("Membership Fees", AccountType.Income, ct: Ct);

        Assert.Equal("4000", result.AccountNumber);
        Assert.Equal(AccountType.Income, result.Type);
    }

    [Fact]
    public async Task CreateAsync_Expense_AssignsExpenseAccountNumber()
    {
        var result = await _sut.CreateAsync("Hall Rental", AccountType.Expense, ct: Ct);

        Assert.Equal("6000", result.AccountNumber);
        Assert.Equal(AccountType.Expense, result.Type);
    }

    [Fact]
    public async Task CreateAsync_SetsIsSystemFalse()
    {
        var result = await _sut.CreateAsync("Donations", AccountType.Income, ct: Ct);

        Assert.False(result.IsSystem);
    }

    [Fact]
    public async Task CreateAsync_WritesAuditEntry()
    {
        await _sut.CreateAsync("Raffles", AccountType.Income, ct: Ct);

        await _audit.Received(1).LogAsync(
            Arg.Any<string>(), Arg.Any<Guid>(), Arg.Is(AuditAction.Create),
            Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_EmptyName_ThrowsValidationException()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _sut.CreateAsync("", AccountType.Income, ct: Ct));
    }

    [Fact]
    public async Task CreateAsync_WhitespaceName_ThrowsValidationException()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _sut.CreateAsync("   ", AccountType.Income, ct: Ct));
    }

    [Fact]
    public async Task Should_AssignBankNumber_When_CreatingAssetBankAccount()
    {
        var result = await _sut.CreateAsync("Operating Account", AccountType.Asset, isBankAccount: true, ct: Ct);

        Assert.Equal("1110", result.AccountNumber);
        Assert.True(result.IsBankAccount);
    }

    [Fact]
    public async Task Should_AssignNonBankAssetNumber_When_CreatingAssetAccount()
    {
        var result = await _sut.CreateAsync("Equipment", AccountType.Asset, ct: Ct);

        Assert.Equal("1300", result.AccountNumber);
        Assert.False(result.IsBankAccount);
    }

    [Theory]
    [InlineData(AccountType.Income)]
    [InlineData(AccountType.Expense)]
    [InlineData(AccountType.Liability)]
    [InlineData(AccountType.Equity)]
    public async Task Should_ThrowValidationException_When_BankFlagOnNonAssetType(AccountType type)
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _sut.CreateAsync("Bad Bank", type, isBankAccount: true, ct: Ct));
    }

    [Fact]
    public async Task Should_ThrowValidationException_When_CreatingDuplicateName()
    {
        var existing = MakeAccount(IncomeAccountId, isSystem: false);
        existing.Name = "Membership Fees";
        _repo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Account> { existing });

        await Assert.ThrowsAsync<ValidationException>(() =>
            _sut.CreateAsync("membership fees", AccountType.Income, ct: Ct));
    }

    // --- UpdateAsync ---

    [Fact]
    public async Task Should_RenameAccount_When_UpdatingUserAccount()
    {
        var account = MakeAccount(IncomeAccountId, isSystem: false);
        _repo.GetByIdAsync(IncomeAccountId, Arg.Any<CancellationToken>()).Returns(account);

        await _sut.UpdateAsync(IncomeAccountId, "New Name", Ct);

        await _repo.Received(1).UpdateAsync(
            Arg.Is<Account>(a => a.Id == IncomeAccountId && a.Name == "New Name"),
            Arg.Any<CancellationToken>());
        await _audit.Received(1).LogAsync(
            Arg.Any<string>(), IncomeAccountId, Arg.Is(AuditAction.Update),
            Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_ThrowValidationException_When_UpdatingSystemAccount()
    {
        var account = MakeAccount(SystemAccountId, isSystem: true);
        _repo.GetByIdAsync(SystemAccountId, Arg.Any<CancellationToken>()).Returns(account);

        await Assert.ThrowsAsync<ValidationException>(() =>
            _sut.UpdateAsync(SystemAccountId, "Renamed System", Ct));
    }

    [Fact]
    public async Task Should_ThrowValidationException_When_UpdatingToEmptyName()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _sut.UpdateAsync(IncomeAccountId, " ", Ct));
    }

    [Fact]
    public async Task Should_ThrowValidationException_When_UpdatingToDuplicateName()
    {
        var account = MakeAccount(IncomeAccountId, isSystem: false);
        var other = MakeAccount(ExpenseAccountId, isSystem: false);
        other.Name = "Taken";
        _repo.GetByIdAsync(IncomeAccountId, Arg.Any<CancellationToken>()).Returns(account);
        _repo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Account> { account, other });

        await Assert.ThrowsAsync<ValidationException>(() =>
            _sut.UpdateAsync(IncomeAccountId, "taken", Ct));
    }

    [Fact]
    public async Task Should_ThrowEntityNotFoundException_When_UpdatingMissingAccount()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Account?)null);

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _sut.UpdateAsync(Guid.NewGuid(), "Whatever", Ct));
    }

    // --- ArchiveAsync ---

    [Fact]
    public async Task ArchiveAsync_ReferencedByTransaction_ThrowsValidationException()
    {
        var account = MakeAccount(IncomeAccountId, isSystem: false);
        _repo.GetByIdAsync(IncomeAccountId, Arg.Any<CancellationToken>()).Returns(account);
        _repo.IsReferencedByTransactionsAsync(IncomeAccountId, Arg.Any<CancellationToken>()).Returns(true);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _sut.ArchiveAsync(IncomeAccountId, Ct));

        Assert.Contains("referenced by one or more transactions", ex.Message);
    }

    [Fact]
    public async Task ArchiveAsync_UnreferencedAccount_Succeeds()
    {
        var account = MakeAccount(IncomeAccountId, isSystem: false);
        _repo.GetByIdAsync(IncomeAccountId, Arg.Any<CancellationToken>()).Returns(account);
        _repo.IsReferencedByTransactionsAsync(IncomeAccountId, Arg.Any<CancellationToken>()).Returns(false);

        await _sut.ArchiveAsync(IncomeAccountId, Ct);

        await _repo.Received(1).ArchiveAsync(IncomeAccountId, "system", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ArchiveAsync_SystemAccount_ThrowsValidationException()
    {
        var sysAccount = MakeAccount(SystemAccountId, isSystem: true);
        _repo.GetByIdAsync(SystemAccountId, Arg.Any<CancellationToken>()).Returns(sysAccount);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _sut.ArchiveAsync(SystemAccountId, Ct));

        Assert.Contains("System accounts cannot be archived", ex.Message);
    }

    [Fact]
    public async Task ArchiveAsync_WritesAuditEntry_OnSuccess()
    {
        var account = MakeAccount(IncomeAccountId, isSystem: false);
        _repo.GetByIdAsync(IncomeAccountId, Arg.Any<CancellationToken>()).Returns(account);
        _repo.IsReferencedByTransactionsAsync(IncomeAccountId, Arg.Any<CancellationToken>()).Returns(false);

        await _sut.ArchiveAsync(IncomeAccountId, Ct);

        await _audit.Received(1).LogAsync(
            nameof(Account), IncomeAccountId, AuditAction.Delete,
            Arg.Any<string?>(), null, "system", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ArchiveAsync_NotFound_ThrowsEntityNotFoundException()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Account?)null);

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _sut.ArchiveAsync(Guid.NewGuid(), Ct));
    }

    // --- RestoreAsync ---

    [Fact]
    public async Task RestoreAsync_CallsRepository_RestoreAsync()
    {
        await _sut.RestoreAsync(IncomeAccountId, Ct);

        await _repo.Received(1).RestoreAsync(IncomeAccountId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RestoreAsync_WritesAuditEntry()
    {
        await _sut.RestoreAsync(IncomeAccountId, Ct);

        await _audit.Received(1).LogAsync(
            nameof(Account), IncomeAccountId, AuditAction.Restore,
            null, null, "system", Arg.Any<CancellationToken>());
    }

    // --- ReorderAsync ---

    [Fact]
    public async Task ReorderAsync_DelegatesToRepository()
    {
        var order = new[] { (IncomeAccountId, 0), (ExpenseAccountId, 1) };

        await _sut.ReorderAsync(order, Ct);

        await _repo.Received(1).ReorderAsync(order, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReorderAsync_EmptyList_DoesNotThrow()
    {
        var exception = await Record.ExceptionAsync(() =>
            _sut.ReorderAsync(Array.Empty<(Guid, int)>(), Ct));

        Assert.Null(exception);
    }

    // --- Helpers ---

    private static Account MakeAccount(Guid id, bool isSystem) => new()
    {
        Id = id,
        Name = isSystem ? "Cash" : "Test Account",
        Type = AccountType.Income,
        AccountNumber = isSystem ? "1100" : "4000",
        IsSystem = isSystem,
        SortOrder = 0,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}
