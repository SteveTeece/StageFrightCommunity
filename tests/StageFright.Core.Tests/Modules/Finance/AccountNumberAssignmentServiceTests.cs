using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Tests.Fixtures;

namespace StageFright.Core.Tests.Modules.Finance;

/// <summary>
/// Unit tests for AccountNumberAssignmentService — sequential GL number assignment for Income and Expense accounts.
/// </summary>
public class AccountNumberAssignmentServiceTests : TestBase
{
    private readonly IAccountRepository _repo = Substitute.For<IAccountRepository>();
    private readonly AccountNumberAssignmentService _sut;

    public AccountNumberAssignmentServiceTests()
    {
        _sut = new AccountNumberAssignmentService(_repo);
    }

    [Fact]
    public async Task AssignNextAsync_FirstIncome_Returns1000()
    {
        _repo.GetNextAccountNumberAsync(AccountType.Income, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns("4000");

        var result = await _sut.AssignNextAsync(AccountType.Income, ct: Ct);

        Assert.Equal("4000", result);
    }

    [Fact]
    public async Task AssignNextAsync_SecondIncome_Returns1001()
    {
        _repo.GetNextAccountNumberAsync(AccountType.Income, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns("4001");

        var result = await _sut.AssignNextAsync(AccountType.Income, ct: Ct);

        Assert.Equal("4001", result);
    }

    [Fact]
    public async Task AssignNextAsync_FirstExpense_Returns2000()
    {
        _repo.GetNextAccountNumberAsync(AccountType.Expense, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns("6000");

        var result = await _sut.AssignNextAsync(AccountType.Expense, ct: Ct);

        Assert.Equal("6000", result);
    }

    [Fact]
    public async Task AssignNextAsync_SecondExpense_Returns2001()
    {
        _repo.GetNextAccountNumberAsync(AccountType.Expense, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns("6001");

        var result = await _sut.AssignNextAsync(AccountType.Expense, ct: Ct);

        Assert.Equal("6001", result);
    }

    [Fact]
    public async Task AssignNextAsync_Income_DelegatesTo_Repository()
    {
        _repo.GetNextAccountNumberAsync(AccountType.Income, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns("4000");

        await _sut.AssignNextAsync(AccountType.Income, ct: Ct);

        await _repo.Received(1).GetNextAccountNumberAsync(AccountType.Income, Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssignNextAsync_Expense_DelegatesTo_Repository()
    {
        _repo.GetNextAccountNumberAsync(AccountType.Expense, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns("6000");

        await _sut.AssignNextAsync(AccountType.Expense, ct: Ct);

        await _repo.Received(1).GetNextAccountNumberAsync(AccountType.Expense, Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssignNextAsync_IncomeSeries_CreationOrderDeterministic()
    {
        // Simulate three sequential calls returning 1000, 1001, 1002
        _repo.GetNextAccountNumberAsync(AccountType.Income, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns("4000", "4001", "4002");

        var first = await _sut.AssignNextAsync(AccountType.Income, ct: Ct);
        var second = await _sut.AssignNextAsync(AccountType.Income, ct: Ct);
        var third = await _sut.AssignNextAsync(AccountType.Income, ct: Ct);

        Assert.Equal("4000", first);
        Assert.Equal("4001", second);
        Assert.Equal("4002", third);
    }

    [Fact]
    public async Task AssignNextAsync_ExpenseSeries_CreationOrderDeterministic()
    {
        _repo.GetNextAccountNumberAsync(AccountType.Expense, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns("6000", "6001", "6002");

        var first = await _sut.AssignNextAsync(AccountType.Expense, ct: Ct);
        var second = await _sut.AssignNextAsync(AccountType.Expense, ct: Ct);
        var third = await _sut.AssignNextAsync(AccountType.Expense, ct: Ct);

        Assert.Equal("6000", first);
        Assert.Equal("6001", second);
        Assert.Equal("6002", third);
    }

    [Fact]
    public async Task AssignNextAsync_Income_DoesNotAffect_ExpenseSequence()
    {
        // Income and expense series are independent
        _repo.GetNextAccountNumberAsync(AccountType.Income, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns("4002");
        _repo.GetNextAccountNumberAsync(AccountType.Expense, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns("6000");

        var income = await _sut.AssignNextAsync(AccountType.Income, ct: Ct);
        var expense = await _sut.AssignNextAsync(AccountType.Expense, ct: Ct);

        Assert.Equal("4002", income);
        Assert.Equal("6000", expense);
    }
}
