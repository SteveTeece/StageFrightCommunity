using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.UI.Pages.Settings;

namespace StageFright.UI.Tests.Pages.SettingsPage;

/// <summary>
/// bUnit tests for SettingsCategoryTab — renders categories by type, create form validation,
/// archive blocking with error message, restore, system category read-only enforcement.
/// </summary>
public class SettingsCategoryTabTests : BunitContext
{
    private readonly ICategoryService _categoryService = Substitute.For<ICategoryService>();

    private static readonly Guid IncomeCategoryId = Guid.NewGuid();
    private static readonly Guid ExpenseCategoryId = Guid.NewGuid();
    private static readonly Guid SystemCategoryId = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid ArchivedCategoryId = Guid.NewGuid();

    public SettingsCategoryTabTests()
    {
        Services.AddSingleton(_categoryService);

        _categoryService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(DefaultActiveCategories());

        _categoryService.GetArchivedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Category>());

        _categoryService.CreateAsync(Arg.Any<string>(), Arg.Any<CategoryType>(), Arg.Any<CancellationToken>())
            .Returns(ci => new Category
            {
                Id = Guid.NewGuid(),
                Name = ci.ArgAt<string>(0),
                Type = ci.ArgAt<CategoryType>(1),
                GLAccount = ci.ArgAt<CategoryType>(1) == CategoryType.Income ? "1001" : "2001",
                IsSystem = false,
                SortOrder = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
    }

    // --- Rendering ---

    [Fact]
    public void Renders_IncomeCategoriesSection()
    {
        var cut = Render<SettingsCategoryTab>();

        Assert.Contains("Income", cut.Markup);
        Assert.Contains("Membership Fees", cut.Markup);
    }

    [Fact]
    public void Renders_ExpenseCategoriesSection()
    {
        var cut = Render<SettingsCategoryTab>();

        Assert.Contains("Expense", cut.Markup);
        Assert.Contains("Hall Rental", cut.Markup);
    }

    [Fact]
    public void Renders_GLAccountColumn()
    {
        var cut = Render<SettingsCategoryTab>();

        Assert.Contains("1000", cut.Markup);
        Assert.Contains("2000", cut.Markup);
    }

    [Fact]
    public void SystemCategory_ShowsReadOnlyLabel_NotArchiveButton()
    {
        _categoryService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Category>
            {
                MakeSystemCategory()
            });

        var cut = Render<SettingsCategoryTab>();

        Assert.Contains("Read-only", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("System", cut.Markup);
    }

    [Fact]
    public void SystemCategory_ArchiveButton_IsAbsent()
    {
        _categoryService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Category> { MakeSystemCategory() });

        var cut = Render<SettingsCategoryTab>();

        // The Archive button should not appear for system categories
        var archiveButtons = cut.FindAll("button.btn-outline-danger");
        Assert.Empty(archiveButtons);
    }

    // --- Create form ---

    [Fact]
    public void Renders_AddCategoryForm()
    {
        var cut = Render<SettingsCategoryTab>();

        cut.Find("form");
    }

    [Fact]
    public async Task SubmitCreate_WithValidName_CallsService()
    {
        var cut = Render<SettingsCategoryTab>();

        var nameInput = cut.Find("input#cat-name");
        await nameInput.ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "New Income Source" });

        await cut.Find("button[type='submit']").ClickAsync(
            new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await _categoryService.Received(1).CreateAsync(
            Arg.Is<string>(s => s.Contains("New Income Source")),
            Arg.Any<CategoryType>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SubmitCreate_ShowsSuccessMessage()
    {
        var cut = Render<SettingsCategoryTab>();

        var nameInput = cut.Find("input#cat-name");
        await nameInput.ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "Raffles" });

        await cut.Find("button[type='submit']").ClickAsync(
            new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.Contains("created", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    // --- Archive ---

    [Fact]
    public async Task ClickArchive_CallsService_ArchiveAsync()
    {
        _categoryService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Category> { MakeUserCategory(IncomeCategoryId, "To Archive", CategoryType.Income) });

        var cut = Render<SettingsCategoryTab>();

        await cut.Find("button.btn-outline-danger").ClickAsync(
            new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await _categoryService.Received(1).ArchiveAsync(IncomeCategoryId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ArchiveBlocked_ShowsValidationError()
    {
        _categoryService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Category> { MakeUserCategory(IncomeCategoryId, "Referenced", CategoryType.Income) });

        _categoryService.ArchiveAsync(IncomeCategoryId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new ValidationException(
                "This category cannot be archived because it is referenced by one or more transactions.",
                "Category", "ArchiveAsync", IncomeCategoryId));

        var cut = Render<SettingsCategoryTab>();

        await cut.Find("button.btn-outline-danger").ClickAsync(
            new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.Contains("referenced by one or more transactions", cut.Markup);
    }

    // --- Restore ---

    [Fact]
    public void Renders_ArchivedSection_WhenArchivedCategoriesExist()
    {
        _categoryService.GetArchivedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Category> { MakeArchivedCategory() });

        var cut = Render<SettingsCategoryTab>();

        Assert.Contains("Archived", cut.Markup);
        Assert.Contains("Restore", cut.Markup);
    }

    [Fact]
    public async Task ClickRestore_CallsService_RestoreAsync()
    {
        _categoryService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Category>());
        _categoryService.GetArchivedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Category> { MakeArchivedCategory() });

        var cut = Render<SettingsCategoryTab>();

        var restoreButton = cut.FindAll("button")
            .First(b => b.TextContent.Trim() == "Restore");
        await restoreButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await _categoryService.Received(1).RestoreAsync(ArchivedCategoryId, Arg.Any<CancellationToken>());
    }

    // --- Helpers ---

    private static List<Category> DefaultActiveCategories() =>
    [
        MakeUserCategory(Guid.NewGuid(), "Membership Fees", CategoryType.Income, "1000"),
        MakeUserCategory(Guid.NewGuid(), "Hall Rental", CategoryType.Expense, "2000")
    ];

    private static Category MakeUserCategory(Guid id, string name, CategoryType type, string gl = "1000") => new()
    {
        Id = id,
        Name = name,
        Type = type,
        GLAccount = gl,
        IsSystem = false,
        SortOrder = 0,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static Category MakeSystemCategory() => new()
    {
        Id = SystemCategoryId,
        Name = "Cash",
        Type = CategoryType.Income,
        GLAccount = "0100",
        IsSystem = true,
        SortOrder = 0,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static Category MakeArchivedCategory() => new()
    {
        Id = ArchivedCategoryId,
        Name = "Old Donations",
        Type = CategoryType.Income,
        GLAccount = "1005",
        IsSystem = false,
        SortOrder = 0,
        IsDeleted = true,
        DeletedAt = DateTime.UtcNow,
        DeletedBy = "system",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}
