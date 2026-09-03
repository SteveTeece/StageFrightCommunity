using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using StageFright.Core.Contracts;
using StageFright.Core.Exceptions;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Settings.Backup;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Pages.Settings;

public partial class BackupRestoreTab : ComponentBase
{
    [Inject] private IBackupService BackupService { get; set; } = null!;
    [Inject] private IStringLocalizer<SettingsResource> L { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResource> Shared { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;

    private bool _busy;
    private string _operation = string.Empty;
    private string? _errorMessage;
    private string? _successMessage;

    private IBrowserFile? _selectedFile;
    private BackupManifest? _manifest;
    private string? _tempRestorePath;
    private bool _confirmed;

    private async Task HandleBackupAsync()
    {
        _busy = true;
        _operation = "backup";
        _errorMessage = null;
        _successMessage = null;

        try
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var fileName = $"StageFright-Backup-{timestamp}.sfbak";
            var filePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                fileName);

            await BackupService.ExportAsync(filePath);
            _successMessage = Loc.Get<SettingsResource>("Settings_Backup_Created", filePath);
        }
        catch (DataAccessException ex)
        {
            _errorMessage = Loc.Get<SettingsResource>("Settings_Backup_Error", ex.Message);
        }
        catch (Exception ex)
        {
            _errorMessage = Loc.Get<SettingsResource>("Settings_Backup_Error", ex.Message);
        }
        finally
        {
            _busy = false;
            _operation = string.Empty;
        }
    }

    private async Task OnFileSelected(InputFileChangeEventArgs e)
    {
        _errorMessage = null;
        _successMessage = null;
        _manifest = null;
        _confirmed = false;
        _selectedFile = e.File;

        if (_selectedFile is null) return;

        try
        {
            _tempRestorePath = Path.Combine(Path.GetTempPath(), $"sf_restore_{Guid.NewGuid()}.sfbak");
            await using var dest = File.Create(_tempRestorePath);
            await using var src = _selectedFile.OpenReadStream(maxAllowedSize: 100 * 1024 * 1024);
            await src.CopyToAsync(dest);

            _manifest = await BackupService.GetManifestAsync(_tempRestorePath);
        }
        catch (ImportException ex)
        {
            _errorMessage = ex.Message;
            _manifest = null;
            CleanupTempFile();
        }
        catch (Exception ex)
        {
            _errorMessage = Loc.Get<SettingsResource>("Settings_Restore_ReadFileError", ex.Message);
            _manifest = null;
            CleanupTempFile();
        }
    }

    private void ConfirmRestore()
    {
        _confirmed = true;
    }

    private void CancelRestore()
    {
        _confirmed = false;
        _manifest = null;
        _selectedFile = null;
        CleanupTempFile();
    }

    private async Task HandleRestoreAsync()
    {
        if (_tempRestorePath is null) return;

        _busy = true;
        _operation = "restore";
        _errorMessage = null;
        _successMessage = null;

        try
        {
            await BackupService.ImportAsync(_tempRestorePath);
            _successMessage = L["Settings_Restore_Success"];
            _confirmed = false;
            _manifest = null;
            _selectedFile = null;
        }
        catch (ImportException ex)
        {
            _errorMessage = ex.Message;
        }
        catch (DataAccessException ex)
        {
            _errorMessage = Loc.Get<SettingsResource>("Settings_Restore_FailedNoChanges", ex.Message);
        }
        catch (Exception ex)
        {
            _errorMessage = Loc.Get<SettingsResource>("Settings_Restore_Error", ex.Message);
        }
        finally
        {
            _busy = false;
            _operation = string.Empty;
            CleanupTempFile();
        }
    }

    private void CleanupTempFile()
    {
        if (_tempRestorePath is not null && File.Exists(_tempRestorePath))
        {
            try { File.Delete(_tempRestorePath); } catch { /* best-effort */ }
        }
        _tempRestorePath = null;
    }
}
