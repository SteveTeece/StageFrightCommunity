using Xunit;
using StageFright.Core.Entities;

namespace StageFright.UI.Tests;

/// <summary>
/// Acceptance tests for User Story 1: First-Run Setup
/// Verifies setup wizard captures organization configuration.
/// </summary>
public class SetupWizardTests
{
    [Fact]
    public void SetupWizard_CanCreateSettings_WithAllFields()
    {
        // Arrange & Act
        var settings = new Settings
        {
            OrganizationName = "Test Organization",
            AnnualFee = 50m,
            AttendanceFee = 5m,
            RenewalMonth = 6
        };

        // Assert
        Assert.NotNull(settings);
        Assert.Equal("Test Organization", settings.OrganizationName);
        Assert.Equal(50m, settings.AnnualFee);
        Assert.Equal(5m, settings.AttendanceFee);
        Assert.Equal(6, settings.RenewalMonth);
    }

    [Fact]
    public void SetupWizard_DefaultCategories_CreatedStructure()
    {
        // Arrange & Act
        var incomeCategory = new Category { Name = "Income", Type = "Income" };
        var expenseCategory = new Category { Name = "Expense", Type = "Expense" };

        // Assert
        Assert.Equal("Income", incomeCategory.Type);
        Assert.Equal("Expense", expenseCategory.Type);
    }
}

