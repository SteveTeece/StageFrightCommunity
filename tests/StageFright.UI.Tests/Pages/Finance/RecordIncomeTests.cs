using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;
using StageFright.UI.Pages.Finance;

namespace StageFright.UI.Tests.Pages.Finance;

/// <summary>
/// bUnit tests for RecordIncome page:
/// - Renders category dropdown with available income categories
/// - Shows warning when no categories configured
/// - Amount > 0 validation enforced client-side
/// - Category must be selected
/// - Submit calls IIncomeEntryService.RecordIncomeAsync
/// - Success message displayed after save with Record Another option
/// - Category pre-selected when only one category exists
/// </summary>
public class RecordIncomeTests : BunitContext
{
    private readonly IIncomeEntryService _incomeService = Substitute.For<IIncomeEntryService>();
    private static readonly Guid CategoryId = Guid.NewGuid();

    public RecordIncomeTests()
    {
        Services.AddSingleton(_incomeService);

        _incomeService.GetIncomeCategoriesAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Category>
            {
                MakeCategory(CategoryId, "Raffle Income")
            });

        _incomeService.RecordIncomeAsync(Arg.Any<RecordIncomeRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
    }

    // --- Rendering ---

    [Fact]
    public void Renders_PageTitle_RecordIncome()
    {
        var cut = Render<RecordIncome>();

        Assert.Contains("Record Income", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Renders_CategoryDropdown_WithAvailableCategories()
    {
        var cut = Render<RecordIncome>();

        var select = cut.Find("#category");
        Assert.Contains("Raffle Income", select.InnerHtml);
    }

    [Fact]
    public void Renders_AmountInput()
    {
        var cut = Render<RecordIncome>();

        cut.Find("#amount");
    }

    [Fact]
    public void Renders_DateInput()
    {
        var cut = Render<RecordIncome>();

        cut.Find("#incomeDate");
    }

    [Fact]
    public void Renders_DescriptionTextArea()
    {
        var cut = Render<RecordIncome>();

        cut.Find("#description");
    }

    [Fact]
    public void Renders_ClearButton()
    {
        var cut = Render<RecordIncome>();

        // RecordIncome is embedded as a tab; the old Cancel nav link was replaced
        // with a Clear button that resets the form in-place.
        var clearBtn = cut.Find("button.btn-outline-secondary");
        Assert.Contains("Clear", clearBtn.TextContent, StringComparison.OrdinalIgnoreCase);
    }

    // --- No-categories state ---

    [Fact]
    public void WhenNoCategories_ShowsWarning_AndNoForm()
    {
        _incomeService.GetIncomeCategoriesAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Category>());

        var cut = Render<RecordIncome>();

        Assert.Empty(cut.FindAll("#category"));
        Assert.Contains("No income categories", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    // --- Pre-selection when single category ---

    [Fact]
    public void WhenSingleCategory_PreSelectsItInDropdown()
    {
        var cut = Render<RecordIncome>();

        // Only one category; code-behind sets _form.CategoryId to its Id
        var select = cut.Find("#category");
        Assert.Equal(CategoryId.ToString(), select.GetAttribute("value"));
    }

    // --- Validation ---

    [Fact]
    public async Task Submit_WithZeroAmount_ShowsValidationError_DoesNotCallService()
    {
        var cut = Render<RecordIncome>();

        // Leave amount at default (0) and submit
        await cut.Find("button.btn-primary").ClickAsync(
            new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.Contains("greater than zero", cut.Markup, StringComparison.OrdinalIgnoreCase);
        await _incomeService.DidNotReceive().RecordIncomeAsync(
            Arg.Any<RecordIncomeRequest>(), Arg.Any<CancellationToken>());
    }

    // --- Successful submission ---

    [Fact]
    public async Task Submit_WithValidData_CallsRecordIncomeAsync()
    {
        var cut = Render<RecordIncome>();

        await cut.Find("#amount").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs
        {
            Value = "150.00"
        });

        await cut.Find("button.btn-primary").ClickAsync(
            new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await _incomeService.Received(1).RecordIncomeAsync(
            Arg.Any<RecordIncomeRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_Success_ShowsSuccessMessage()
    {
        var cut = Render<RecordIncome>();

        await cut.Find("#amount").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs
        {
            Value = "75.50"
        });

        await cut.Find("button.btn-primary").ClickAsync(
            new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.Contains("recorded successfully", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Submit_Success_ShowsRecordAnotherButton()
    {
        var cut = Render<RecordIncome>();

        await cut.Find("#amount").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs
        {
            Value = "50"
        });

        await cut.Find("button.btn-primary").ClickAsync(
            new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.Contains("Record Another", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    // --- Helpers ---

    private static Category MakeCategory(Guid id, string name) => new()
    {
        Id = id, Name = name, Type = CategoryType.Income,
        GLAccount = "1000", IsSystem = false, SortOrder = 0,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };
}
