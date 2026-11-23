namespace StageFright.Core.Contracts;

/// <summary>
/// Supplies the directory where <see cref="IBackupService"/> writes the pre-restore recovery copy
/// before a restore overwrites the local database (FR-015). Platform-backed (MAUI); never throws —
/// it returns an existing, writable directory, matching the never-throw shape of
/// <see cref="ILanguagePreferenceStore"/> / <see cref="IDeviceThemePreferenceProvider"/>.
/// </summary>
public interface IRecoveryCopyStore
{
    /// <summary>An existing, writable directory for recovery-copy <c>.sfbak</c> files. Created on demand.</summary>
    string GetRecoveryDirectory();
}
