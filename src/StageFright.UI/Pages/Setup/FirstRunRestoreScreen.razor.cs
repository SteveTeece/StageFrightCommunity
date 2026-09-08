using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using StageFright.Core.Contracts;
using StageFright.Core.Exceptions;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Settings.Backup;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Pages.Setup;

/// <summary>
/// First-run restore choice (spec 030, US1). Pre-wizard screen shown after <c>/language-select</c>
/// and before <c>/setup</c>: a plain <c>&lt;input type="checkbox"&gt;</c> — the deliberate Verbatim
/// Constraint exception to the RadzenSwitch rule — offers restoring from a <c>.sfbak</c> file
/// instead of manual setup. Ticking it reveals an <c>&lt;InputFile&gt;</c>; a valid pick reads the
/// file's <see cref="BackupManifest"/> for a contents summary; <c>#confirm-restore</c> runs
/// <see cref="IBackupService.ImportAsync"/> off the UI thread behind a blocking overlay and, on
/// success, navigates to the terminal <c>/restart-required</c> screen. <c>#cancel-restore</c> or
/// any validation / corrupt-file / newer-version failure leaves the database untouched and keeps
/// the manual <c>#continue-setup</c> path available (FR-001..FR-003, FR-005, FR-007, FR-008).
/// </summary>
public partial class FirstRunRestoreScreen : ComponentBase
{
    [Inject] private IBackupService BackupService { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private IStringLocalizer<SetupResource> L { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;

    private bool _restoreFromBackup;
    private bool _restoring;
    private string? _errorMessage;

    private BackupManifest? _manifest;
    private string? _tempRestorePath;

    private void HandleRestoreToggled(ChangeEventArgs e)
    {
        _restoreFromBackup = e.Value is true;
        _errorMessage = null;
        if (!_restoreFromBackup)
            ResetSelection();
    }

    private async Task OnFileSelectedAsync(InputFileChangeEventArgs e)
    {
        _errorMessage = null;
        _manifest = null;
        CleanupTempFile();

        var file = e.File;
        if (file is null)
            return;

        try
        {
            _tempRestorePath = Path.Combine(Path.GetTempPath(), $"sf_firstrun_restore_{Guid.NewGuid()}.sfbak");
            await using (var dest = File.Create(_tempRestorePath))
            await using (var src = file.OpenReadStream(maxAllowedSize: 100 * 1024 * 1024))
            {
                await src.CopyToAsync(dest);
            }

            _manifest = await BackupService.GetManifestAsync(_tempRestorePath);
        }
        catch (ImportException ex)
        {
            // Corrupt / not a .sfbak / newer-version — message is already localized in Core.
            _errorMessage = ex.Message;
            _manifest = null;
            CleanupTempFile();
        }
        catch (Exception ex)
        {
            _errorMessage = Loc.Get<SetupResource>("Setup_FirstRunRestore_ReadFileError", ex.Message);
            _manifest = null;
            CleanupTempFile();
        }
    }

    private void HandleCancelRestore()
    {
        _restoreFromBackup = false;
        _errorMessage = null;
        ResetSelection();
    }

    private void HandleContinueSetup() => Nav.NavigateTo("/setup");

    private async Task HandleConfirmRestoreAsync()
    {
        if (_tempRestorePath is null)
            return;

        _restoring = true;
        _errorMessage = null;

        try
        {
            // ImportAsync writes the pre-restore recovery copy, runs the atomic PK-upsert and the
            // audit entry. It exposes no progress callback, so the blocking overlay itself is the
            // "advancing progress" indicator (mirrors the setup-seeding overlay). Run it off the UI
            // thread so the render stays responsive (spec Edge Case "Large dataset").
            var path = _tempRestorePath;
            await Task.Run(() => BackupService.ImportAsync(path));

            Nav.NavigateTo("/restart-required");
        }
        catch (ImportException ex)
        {
            _errorMessage = ex.Message;
        }
        catch (DataAccessException ex)
        {
            _errorMessage = Loc.Get<SetupResource>("Setup_FirstRunRestore_ReadFileError", ex.Message);
        }
        catch (Exception ex)
        {
            _errorMessage = Loc.Get<SetupResource>("Setup_FirstRunRestore_ReadFileError", ex.Message);
        }
        finally
        {
            _restoring = false;
            CleanupTempFile();
        }
    }

    private void ResetSelection()
    {
        _manifest = null;
        CleanupTempFile();
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
