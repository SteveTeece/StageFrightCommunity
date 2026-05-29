using Microsoft.AspNetCore.Components;
using StageFright.Core.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StageFright.UI.Pages.Finance;

public partial class CategoryManagement
{
    [Inject]
    private IFinanceService FinanceService { get; set; } = null!;

    private string? ErrorMessage;
    private string? SuccessMessage;
    private bool ShowingAddForm = false;
    private string NewCategoryName = string.Empty;
    private string NewCategoryType = string.Empty;
    private Guid EditingCategoryId = Guid.Empty;
    private string EditingCategoryName = string.Empty;

    private List<dynamic> Categories = new();

    protected override async Task OnInitializedAsync()
    {
        await LoadCategories();
    }

    private async Task LoadCategories()
    {
        try
        {
            ErrorMessage = null;
            Categories = (await FinanceService.GetCategoriesAsync()).ToList();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading categories: {ex.Message}";
        }
    }

    private void ShowAddForm()
    {
        ShowingAddForm = true;
        NewCategoryName = string.Empty;
        NewCategoryType = string.Empty;
    }

    private void HideAddForm()
    {
        ShowingAddForm = false;
        NewCategoryName = string.Empty;
        NewCategoryType = string.Empty;
    }

    private async Task AddCategory()
    {
        try
        {
            ErrorMessage = null;

            if (string.IsNullOrWhiteSpace(NewCategoryName))
                throw new Exception("Category name is required.");

            if (string.IsNullOrWhiteSpace(NewCategoryType))
                throw new Exception("Category type is required.");

            await FinanceService.CreateCategoryAsync(NewCategoryName, NewCategoryType);
            SuccessMessage = "Category added successfully.";
            HideAddForm();
            await LoadCategories();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private void EditCategory(dynamic category)
    {
        EditingCategoryId = category.Id;
        EditingCategoryName = category.Name;
    }

    private async Task SaveEditCategory(Guid categoryId)
    {
        try
        {
            ErrorMessage = null;

            if (string.IsNullOrWhiteSpace(EditingCategoryName))
                throw new Exception("Category name is required.");

            await FinanceService.UpdateCategoryAsync(categoryId, EditingCategoryName);
            SuccessMessage = "Category updated successfully.";
            CancelEdit();
            await LoadCategories();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private void CancelEdit()
    {
        EditingCategoryId = Guid.Empty;
        EditingCategoryName = string.Empty;
    }

    private async Task ArchiveCategory(Guid categoryId)
    {
        try
        {
            ErrorMessage = null;

            await FinanceService.ArchiveCategoryAsync(categoryId);
            SuccessMessage = "Category archived successfully.";
            await LoadCategories();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private async Task RestoreCategory(Guid categoryId)
    {
        try
        {
            ErrorMessage = null;

            await FinanceService.RestoreCategoryAsync(categoryId);
            SuccessMessage = "Category restored successfully.";
            await LoadCategories();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
