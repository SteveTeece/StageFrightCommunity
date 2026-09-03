using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.Core.Contracts;
using StageFright.Core.Localization;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Pages;

public partial class StartupError : ComponentBase
{
    [Inject] private IStartupDiagnosticService Diagnostics { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResource> L { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;

    private string? _errorDetail;
    private string? _dbPath;
    private bool _recreating;
    private string? _actionError;

    protected override void OnInitialized()
    {
        _errorDetail = Diagnostics.StartupException?.Message;
        _dbPath = Diagnostics.DatabasePath;
    }

    private Task OpenFolderAsync()
    {
        if (string.IsNullOrEmpty(_dbPath))
            return Task.CompletedTask;

        try
        {
            var folder = Path.GetDirectoryName(_dbPath);
            if (!string.IsNullOrEmpty(folder) && OperatingSystem.IsWindows())
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });
        }
        catch (Exception ex)
        {
            _actionError = Loc.Get<SharedResource>("Shared_StartupError_OpenFolderError", ex.Message);
        }

        return Task.CompletedTask;
    }

    private async Task RecreateAsync()
    {
        if (string.IsNullOrEmpty(_dbPath))
            return;

        _recreating = true;
        _actionError = null;

        try
        {
            if (File.Exists(_dbPath))
                File.Delete(_dbPath);

            Diagnostics.ClearError();
            Nav.NavigateTo("/", forceLoad: true);
        }
        catch (Exception ex)
        {
            _actionError = Loc.Get<SharedResource>("Shared_StartupError_DeleteError", ex.Message);
        }
        finally
        {
            _recreating = false;
            await Task.CompletedTask;
        }
    }
}
