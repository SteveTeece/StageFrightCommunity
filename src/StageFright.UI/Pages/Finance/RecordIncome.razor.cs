using Microsoft.AspNetCore.Components;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Modules.Finance;

namespace StageFright.UI.Pages.Finance;

public partial class RecordIncome : ComponentBase
{
    [Inject] private IIncomeEntryService IncomeEntryService { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;

    private readonly RecordIncomeModel _form = new();
    private readonly Dictionary<string, string> _errors = new();
    private IReadOnlyList<Category> _categories = [];
    private bool _loading = true;
    private bool _saving;
    private string? _successMessage;
    private string? _errorMessage;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _categories = await IncomeEntryService.GetIncomeCategoriesAsync();
            if (_categories.Count == 1)
                _form.CategoryId = _categories[0].Id;
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

    private async Task SaveAsync()
    {
        _errors.Clear();
        _errorMessage = null;

        if (_form.Amount <= 0m)
        {
            _errors["Amount"] = "Amount must be greater than zero.";
            return;
        }

        if (_form.CategoryId == Guid.Empty)
        {
            _errors["CategoryId"] = "Please select a category.";
            return;
        }

        _saving = true;
        try
        {
            var request = new RecordIncomeRequest
            {
                Date = _form.Date,
                Amount = _form.Amount,
                CategoryId = _form.CategoryId,
                Description = string.IsNullOrWhiteSpace(_form.Description) ? null : _form.Description.Trim()
            };

            await IncomeEntryService.RecordIncomeAsync(request);
            _successMessage = $"Income of {request.Amount:C} recorded successfully.";
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to record income: {ex.Message}";
        }
        finally
        {
            _saving = false;
        }
    }

    private void RecordAnother()
    {
        _successMessage = null;
        _errorMessage = null;
        _form.Amount = 0m;
        _form.Description = null;
        _form.Date = DateTime.Today;
    }
}
