using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Settings.Backup;
using StageFright.UI.Pages.Settings;

namespace StageFright.UI.Tests.Pages.Settings;

/// <summary>
/// bUnit tests for <see cref="BackupRestoreTab"/> (spec 030, US2): the unencrypted-file notice is
/// shown (FR-020), Create runs <see cref="IBackupService.CreateBackupAsync"/> with a user cancel as
/// a silent no-op, <c>.backup-verify-result</c> reflects the <see cref="BackupVerificationResult"/>
/// ("verified" / "failed — do not rely on this file" + discrepancies), and a successful restore
/// navigates to <c>/restart-required</c> (FR-007, FR-018).
/// </summary>
public class BackupRestoreTabTests : LocalizedTestContext
{
    private readonly IBackupService _backup = Substitute.For<IBackupService>();

    public BackupRestoreTabTests()
    {
        Services.AddSingleton(_backup);
    }

    private static BackupManifest SampleManifest() => new()
    {
        SchemaVersion = "1.2.0",
        GeneratedAt = new DateTime(2026, 2, 3, 4, 5, 6, DateTimeKind.Utc),
        ApplicationVersion = "1.0.0",
        EntityCounts = new Dictionary<string, int> { ["Members"] = 4, ["Accounts"] = 2 },
    };

    private static InputFileContent SfbakFile() =>
        InputFileContent.CreateFromText("not a real backup", "handover.sfbak");

    [Fact]
    public void UnencryptedNotice_IsRendered()
    {
        var cut = Render<BackupRestoreTab>();

        var notice = cut.Find(".backup-unencrypted-notice");
        Assert.False(string.IsNullOrWhiteSpace(notice.TextContent));
    }

    [Fact]
    public async Task CreateBackup_InvokesCreateBackupAsync()
    {
        _backup.CreateBackupAsync(Arg.Any<CancellationToken>())
            .Returns(new BackupVerificationResult(true, [], @"C:\backups\Choir backup 2026-02-03.sfbak"));
        var cut = Render<BackupRestoreTab>();

        await cut.Find("#create-backup").ClickAsync(new MouseEventArgs());

        await _backup.Received(1).CreateBackupAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateBackup_UserCancellingTheSaveDialog_IsASilentNoOp()
    {
        _backup.CreateBackupAsync(Arg.Any<CancellationToken>())
            .Returns<Task<BackupVerificationResult>>(_ => throw new OperationCanceledException());
        var cut = Render<BackupRestoreTab>();

        await cut.Find("#create-backup").ClickAsync(new MouseEventArgs());

        Assert.Empty(cut.FindAll(".backup-verify-result"));
        Assert.Empty(cut.FindAll(".alert-danger"));
    }

    [Fact]
    public async Task CreateBackup_WhenVerified_ShowsTheVerifiedResult()
    {
        _backup.CreateBackupAsync(Arg.Any<CancellationToken>())
            .Returns(new BackupVerificationResult(true, [], @"C:\backups\Choir backup 2026-02-03.sfbak"));
        var cut = Render<BackupRestoreTab>();

        await cut.Find("#create-backup").ClickAsync(new MouseEventArgs());

        var result = cut.Find(".backup-verify-result");
        Assert.Contains("verified", result.TextContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Choir backup 2026-02-03.sfbak", result.TextContent);
    }

    [Fact]
    public async Task CreateBackup_WhenVerificationFails_ShowsTheFailureAndTheDiscrepancies()
    {
        var discrepancy = "Members: the file records 3 but the backup captured 4.";
        _backup.CreateBackupAsync(Arg.Any<CancellationToken>())
            .Returns<Task<BackupVerificationResult>>(_ => throw new BackupVerificationException(
                "verification failed", @"C:\backups\bad.sfbak", [discrepancy]));
        var cut = Render<BackupRestoreTab>();

        await cut.Find("#create-backup").ClickAsync(new MouseEventArgs());

        var result = cut.Find(".backup-verify-result");
        Assert.Contains("do not rely on this file", result.TextContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(discrepancy, result.TextContent);
    }

    [Fact]
    public async Task SuccessfulRestore_NavigatesToRestartRequired()
    {
        _backup.GetManifestAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(SampleManifest());
        _backup.ImportAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var cut = Render<BackupRestoreTab>();

        cut.FindComponent<InputFile>().UploadFiles(SfbakFile());
        cut.WaitForAssertion(() => cut.Find("#understand-restore"));

        cut.Find("#understand-restore").Click();
        await cut.Find("#confirm-restore").ClickAsync(new MouseEventArgs());

        await _backup.Received(1).ImportAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/restart-required", nav.Uri);
    }
}
