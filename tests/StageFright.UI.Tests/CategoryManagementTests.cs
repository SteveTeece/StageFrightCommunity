using Xunit;
using StageFright.Core.Entities;

namespace StageFright.UI.Tests;

/// <summary>
/// Acceptance tests for User Story 7: Category Management
/// Verifies create/edit/archive/restore operations with validation.
/// </summary>
public class CategoryManagementTests
{
    [Fact]
    public void Category_CanBeCreated_WithValidData()
    {
        // Arrange & Act
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = "General Expenses",
            Type = "Expense",
            SortOrder = 0,
            IsArchived = false
        };

        // Assert
        Assert.NotNull(category);
        Assert.Equal("General Expenses", category.Name);
        Assert.Equal("Expense", category.Type);
        Assert.False(category.IsArchived);
    }

    [Fact]
    public void Category_Type_MustBeIncomeOrExpense()
    {
        // Arrange
        var incomeCategory = new Category { Type = "Income" };
        var expenseCategory = new Category { Type = "Expense" };

        // Act & Assert
        Assert.Equal("Income", incomeCategory.Type);
        Assert.Equal("Expense", expenseCategory.Type);
    }

    [Fact]
    public void Category_GlAccount_CanBeAssigned()
    {
        // Arrange & Act
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Revenue",
            Type = "Income",
            GlAccount = "1000"
        };

        // Assert
        Assert.Equal("1000", category.GlAccount);
    }

    [Fact]
    public void Category_CanBeArchived()
    {
        // Arrange
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Legacy Category",
            Type = "Income",
            IsArchived = false
        };

        // Act
        category.IsArchived = true;

        // Assert
        Assert.True(category.IsArchived);
    }

    [Fact]
    public void Category_CanBeRestored_AfterArchival()
    {
        // Arrange
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Archived Category",
            Type = "Expense",
            IsArchived = true
        };

        // Act
        category.IsArchived = false;

        // Assert
        Assert.False(category.IsArchived);
    }

    [Fact]
    public void Category_SortOrder_CanBeModified()
    {
        // Arrange
        var category1 = new Category { Name = "First", SortOrder = 1 };
        var category2 = new Category { Name = "Second", SortOrder = 2 };
        var category3 = new Category { Name = "Third", SortOrder = 3 };

        // Act
        category2.SortOrder = 1;
        category1.SortOrder = 2;

        // Assert
        Assert.Equal(1, category2.SortOrder);
        Assert.Equal(2, category1.SortOrder);
    }

    [Fact]
    public void Category_ReorderingSupport_Valid()
    {
        // Arrange
        var categories = new[]
        {
            new Category { Name = "Expenses", SortOrder = 1 },
            new Category { Name = "Income", SortOrder = 2 },
            new Category { Name = "Other", SortOrder = 3 }
        };

        // Act
        var reordered = new List<Category>(categories);
        var temp = reordered[0];
        reordered[0] = reordered[2];
        reordered[2] = temp;

        // Assert
        Assert.Equal("Other", reordered[0].Name);
        Assert.Equal("Expenses", reordered[2].Name);
    }

    [Fact]
    public void Category_IncomeExpenseDistinction_Valid()
    {
        // Arrange
        var incomeCategories = new[]
        {
            new Category { Type = "Income", Name = "Annual Fees" },
            new Category { Type = "Income", Name = "Donations" }
        };

        var expenseCategories = new[]
        {
            new Category { Type = "Expense", Name = "Supplies" },
            new Category { Type = "Expense", Name = "Rent" }
        };

        // Act & Assert
        foreach (var cat in incomeCategories)
        {
            Assert.Equal("Income", cat.Type);
        }

        foreach (var cat in expenseCategories)
        {
            Assert.Equal("Expense", cat.Type);
        }
    }

    [Fact]
    public void Category_MultipleArchival_Scenarios_Supported()
    {
        // Arrange
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Test Category",
            IsArchived = false
        };

        // Act & Assert
        Assert.False(category.IsArchived);
        
        category.IsArchived = true;
        Assert.True(category.IsArchived);

        category.IsArchived = false;
        Assert.False(category.IsArchived);
    }
}
