using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using StageFright.Core.Contracts;
using StageFright.Core.Exceptions;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Settings.Backup;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Pages.Settings;

/// <summary>
/// Settings → Backup &amp; Restore. Create runs <see cref="IBackupService.CreateBackupAsync"/> — the
/// OS-native Save dialog pre-filled with the FR-010 default name, then a read-back self-check — and
/// shows "verified" or "failed — do not rely on this file" (FR-018, FR-020, FR-021–FR-024). A
/// user-dismissed Save dialog is a silent no-op. A successful restore navigates to the terminal
/// <c>/restart-required</c> screen, the same as the first-run path (FR-007).
/// </summary>
public partial class BackupRestoreTab : ComponentBase
{
    [Inject] private IBackupService BackupService { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private IStringLocalizer<SettingsResource> L { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResource> Shared { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;

    private bool _busy;
    private string _operation = string.Empty;
    private string? _errorMessage;

    private BackupVerificationResult? _verifyResult;
    private IReadOnlyList<string>? _verifyDiscrepancies;

    private IBrowserFile? _selectedFile;
    private BackupManifest? _manifest;
    private string? _tempRestorePath;
    private bool _confirmed;

    private async Task HandleBackupAsync()
    {
        _busy = true;
        _operation = "backup";
        _errorMessage = null;
        _verifyResult = null;
        _verifyDiscrepancies = null;

        try
        {
            _verifyResult = await BackupService.CreateBackupAsync();
        }
        catch (OperationCanceledException)
        {
            // The user dismissed the native Save dialog — a silent no-op, not an error.
        }
        catch (BackupVerificationException ex)
        {
            // The file was written but failed its post-write self-check; it is left on disk.
            _verifyDiscrepancies = ex.Discrepancies;
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
        _verifyResult = null;
        _verifyDiscrepancies = null;
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

        try
        {
            await BackupService.ImportAsync(_tempRestorePath);
            _confirmed = false;
            _manifest = null;
            _selectedFile = null;
            // In-memory config, culture and the first-run flag from before the restore must not be
            // trusted — send the user to the terminal restart screen, the same as the first-run
            // path (FR-007 / FR-018).
            Nav.NavigateTo("/restart-required");
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
