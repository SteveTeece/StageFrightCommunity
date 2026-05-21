using Microsoft.AspNetCore.Components;
using StageFright.Data.Repositories;
using StageFright.Core.Entities;

namespace StageFright.UI.Pages.Settings;

public partial class CategoriesTab
{
    [Inject]
    public ICategoryRepository CategoryRepository { get; set; } = default!;

    private List<Category> IncomeCategories = new();
    private List<Category> ExpenseCategories = new();
    private bool IsLoading = true;
    private bool ShowForm = false;
    private string? ErrorMessage = null;

    private Category? EditingCategory = null;
    private string FormName = "";
    private string FormType = "Income";

    protected override async Task OnInitializedAsync()
    {
        await LoadCategories();
    }

    private async Task LoadCategories()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            var incomeCategories = await CategoryRepository.GetIncomeCategoriesAsync();
            var expenseCategories = await CategoryRepository.GetExpenseCategoriesAsync();

            IncomeCategories = incomeCategories.ToList();
            ExpenseCategories = expenseCategories.ToList();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading categories: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ShowCreateForm()
    {
        EditingCategory = null;
        FormName = "";
        FormType = "Income";
        ShowForm = true;
    }

    private void EditCategory(Category category)
    {
        EditingCategory = category;
        FormName = category.Name;
        FormType = category.Type;
        ShowForm = true;
    }

    private async Task SaveCategory()
    {
        try
        {
            ErrorMessage = null;

            if (EditingCategory == null)
            {
                var newCategory = new Category
                {
                    Name = FormName,
                    Type = FormType,
                    SortOrder = 0,
                    IsArchived = false
                };
                await CategoryRepository.CreateAsync(newCategory);
            }
            else
            {
                EditingCategory.Name = FormName;
                await CategoryRepository.UpdateAsync(EditingCategory);
            }

            CancelForm();
            await LoadCategories();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error saving category: {ex.Message}";
        }
    }

    private void CancelForm()
    {
        ShowForm = false;
        EditingCategory = null;
        FormName = "";
    }

    private async Task ArchiveCategory(Guid id)
    {
        try
        {
            await CategoryRepository.ArchiveAsync(id);
            await LoadCategories();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error archiving category: {ex.Message}";
        }
    }

    private async Task RestoreCategory(Guid id)
    {
        try
        {
            await CategoryRepository.RestoreAsync(id);
            await LoadCategories();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error restoring category: {ex.Message}";
        }
    }
}
