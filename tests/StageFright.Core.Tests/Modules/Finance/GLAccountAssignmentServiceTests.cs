using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Tests.Fixtures;

namespace StageFright.Core.Tests.Modules.Finance;

/// <summary>
/// Unit tests for GLAccountAssignmentService — sequential GL number assignment for Income and Expense categories.
/// </summary>
public class GLAccountAssignmentServiceTests : TestBase
{
    private readonly ICategoryRepository _repo = Substitute.For<ICategoryRepository>();
    private readonly GLAccountAssignmentService _sut;

    public GLAccountAssignmentServiceTests()
    {
        _sut = new GLAccountAssignmentService(_repo);
    }

    [Fact]
    public async Task AssignNextAsync_FirstIncome_Returns1000()
    {
        _repo.GetNextGLAccountAsync(CategoryType.Income, Arg.Any<CancellationToken>())
            .Returns("1000");

        var result = await _sut.AssignNextAsync(CategoryType.Income, Ct);

        Assert.Equal("1000", result);
    }

    [Fact]
    public async Task AssignNextAsync_SecondIncome_Returns1001()
    {
        _repo.GetNextGLAccountAsync(CategoryType.Income, Arg.Any<CancellationToken>())
            .Returns("1001");

        var result = await _sut.AssignNextAsync(CategoryType.Income, Ct);

        Assert.Equal("1001", result);
    }

    [Fact]
    public async Task AssignNextAsync_FirstExpense_Returns2000()
    {
        _repo.GetNextGLAccountAsync(CategoryType.Expense, Arg.Any<CancellationToken>())
            .Returns("2000");

        var result = await _sut.AssignNextAsync(CategoryType.Expense, Ct);

        Assert.Equal("2000", result);
    }

    [Fact]
    public async Task AssignNextAsync_SecondExpense_Returns2001()
    {
        _repo.GetNextGLAccountAsync(CategoryType.Expense, Arg.Any<CancellationToken>())
            .Returns("2001");

        var result = await _sut.AssignNextAsync(CategoryType.Expense, Ct);

        Assert.Equal("2001", result);
    }

    [Fact]
    public async Task AssignNextAsync_Income_DelegatesTo_Repository()
    {
        _repo.GetNextGLAccountAsync(CategoryType.Income, Arg.Any<CancellationToken>())
            .Returns("1000");

        await _sut.AssignNextAsync(CategoryType.Income, Ct);

        await _repo.Received(1).GetNextGLAccountAsync(CategoryType.Income, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssignNextAsync_Expense_DelegatesTo_Repository()
    {
        _repo.GetNextGLAccountAsync(CategoryType.Expense, Arg.Any<CancellationToken>())
            .Returns("2000");

        await _sut.AssignNextAsync(CategoryType.Expense, Ct);

        await _repo.Received(1).GetNextGLAccountAsync(CategoryType.Expense, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssignNextAsync_IncomeSeries_CreationOrderDeterministic()
    {
        // Simulate three sequential calls returning 1000, 1001, 1002
        _repo.GetNextGLAccountAsync(CategoryType.Income, Arg.Any<CancellationToken>())
            .Returns("1000", "1001", "1002");

        var first = await _sut.AssignNextAsync(CategoryType.Income, Ct);
        var second = await _sut.AssignNextAsync(CategoryType.Income, Ct);
        var third = await _sut.AssignNextAsync(CategoryType.Income, Ct);

        Assert.Equal("1000", first);
        Assert.Equal("1001", second);
        Assert.Equal("1002", third);
    }

    [Fact]
    public async Task AssignNextAsync_ExpenseSeries_CreationOrderDeterministic()
    {
        _repo.GetNextGLAccountAsync(CategoryType.Expense, Arg.Any<CancellationToken>())
            .Returns("2000", "2001", "2002");

        var first = await _sut.AssignNextAsync(CategoryType.Expense, Ct);
        var second = await _sut.AssignNextAsync(CategoryType.Expense, Ct);
        var third = await _sut.AssignNextAsync(CategoryType.Expense, Ct);

        Assert.Equal("2000", first);
        Assert.Equal("2001", second);
        Assert.Equal("2002", third);
    }

    [Fact]
    public async Task AssignNextAsync_Income_DoesNotAffect_ExpenseSequence()
    {
        // Income and expense series are independent
        _repo.GetNextGLAccountAsync(CategoryType.Income, Arg.Any<CancellationToken>())
            .Returns("1002");
        _repo.GetNextGLAccountAsync(CategoryType.Expense, Arg.Any<CancellationToken>())
            .Returns("2000");

        var income = await _sut.AssignNextAsync(CategoryType.Income, Ct);
        var expense = await _sut.AssignNextAsync(CategoryType.Expense, Ct);

        Assert.Equal("1002", income);
        Assert.Equal("2000", expense);
    }
}
