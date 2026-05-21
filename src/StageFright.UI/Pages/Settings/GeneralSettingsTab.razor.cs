using Microsoft.AspNetCore.Components;
using StageFright.Data.Repositories;
using StageFright.Core.Entities;
using SettingsEntity = StageFright.Core.Entities.Settings;

namespace StageFright.UI.Pages.Settings;

public partial class GeneralSettingsTab
{
    [Inject]
    public ISettingsRepository SettingsRepository { get; set; } = default!;

    private SettingsEntity? Settings { get; set; }
    private bool IsLoading = true;
    private string? ErrorMessage = null;
    private string? SuccessMessage = null;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            Settings = await SettingsRepository.GetSettingsAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading settings: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"Error: {ex}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SaveSettings()
    {
        if (Settings == null) return;

        try
        {
            SuccessMessage = null;
            ErrorMessage = null;
            await SettingsRepository.UpdateSettingsAsync(Settings);
            SuccessMessage = "Settings saved successfully.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error saving settings: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"Error: {ex}");
        }
    }
}
