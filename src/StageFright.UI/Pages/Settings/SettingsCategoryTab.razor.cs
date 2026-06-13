using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using CoreValidationException = StageFright.Core.Exceptions.ValidationException;

namespace StageFright.UI.Pages.Settings;

public partial class SettingsCategoryTab : ComponentBase
{
    [Inject] private ICategoryService CategoryService { get; set; } = null!;

    private bool _loading = true;
    private bool _creating;
    private string? _errorMessage;
    private string? _successMessage;

    private List<Category> _incomeCategories = new();
    private List<Category> _expenseCategories = new();
    private List<Category> _archivedCategories = new();

    private NewCategoryModel _newCategoryModel = new();

    protected override async Task OnInitializedAsync()
    {
        await LoadCategoriesAsync();
    }

    private async Task LoadCategoriesAsync()
    {
        _loading = true;
        _errorMessage = null;
        try
        {
            var all = await CategoryService.GetAllAsync();
            _incomeCategories = all.Where(c => c.Type == CategoryType.Income)
                                   .OrderBy(c => c.SortOrder).ThenBy(c => c.CreatedAt)
                                   .ToList();
            _expenseCategories = all.Where(c => c.Type == CategoryType.Expense)
                                    .OrderBy(c => c.SortOrder).ThenBy(c => c.CreatedAt)
                                    .ToList();
            _archivedCategories = (await CategoryService.GetArchivedAsync())
                                   .OrderBy(c => c.Type).ThenBy(c => c.Name)
                                   .ToList();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to load categories: {ex.Message}";
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task HandleCreateAsync()
    {
        _creating = true;
        _errorMessage = null;
        _successMessage = null;

        try
        {
            var created = await CategoryService.CreateAsync(_newCategoryModel.Name!, _newCategoryModel.Type);
            _successMessage = $"Category '{created.Name}' created with GL account {created.GLAccount}.";
            _newCategoryModel = new NewCategoryModel();
            await LoadCategoriesAsync();
        }
        catch (CoreValidationException ex)
        {
            _errorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to create category: {ex.Message}";
        }
        finally
        {
            _creating = false;
        }
    }

    private async Task ArchiveAsync(Guid id)
    {
        _errorMessage = null;
        _successMessage = null;

        try
        {
            await CategoryService.ArchiveAsync(id);
            _successMessage = "Category archived.";
            await LoadCategoriesAsync();
        }
        catch (CoreValidationException ex)
        {
            _errorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to archive category: {ex.Message}";
        }
    }

    private async Task RestoreAsync(Guid id)
    {
        _errorMessage = null;
        _successMessage = null;

        try
        {
            await CategoryService.RestoreAsync(id);
            _successMessage = "Category restored.";
            await LoadCategoriesAsync();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to restore category: {ex.Message}";
        }
    }

    private async Task MoveUpAsync(Category cat, List<Category> list)
    {
        var index = list.IndexOf(cat);
        if (index <= 0) return;
        (list[index - 1], list[index]) = (list[index], list[index - 1]);
        await PersistOrderAsync(list);
    }

    private async Task MoveDownAsync(Category cat, List<Category> list)
    {
        var index = list.IndexOf(cat);
        if (index < 0 || index >= list.Count - 1) return;
        (list[index], list[index + 1]) = (list[index + 1], list[index]);
        await PersistOrderAsync(list);
    }

    private async Task PersistOrderAsync(List<Category> list)
    {
        _errorMessage = null;
        try
        {
            var order = list.Select((c, i) => (c.Id, i)).ToList();
            await CategoryService.ReorderAsync(order);
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to save order: {ex.Message}";
        }
    }

    private sealed class NewCategoryModel
    {
        [Required(ErrorMessage = "Category name is required.")]
        [MaxLength(100, ErrorMessage = "Category name must be 100 characters or fewer.")]
        public string? Name { get; set; }

        [Required]
        public CategoryType Type { get; set; } = CategoryType.Income;
    }
}
