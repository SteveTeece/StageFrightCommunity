using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Settings.Backup;
using StageFright.UI.Pages.Setup;

namespace StageFright.UI.Tests.Pages.Setup;

/// <summary>
/// bUnit tests for <see cref="FirstRunRestoreScreen"/> (spec 030, US1) — the pre-wizard
/// <c>/first-run-restore</c> screen: a plain checkbox reveals an <c>&lt;InputFile&gt;</c>, a valid
/// pick shows a contents summary, <c>#confirm-restore</c> routes to <c>/restart-required</c> on
/// success, and cancel / a corrupt or newer-version file leaves the database untouched with the
/// manual <c>#continue-setup</c> path still available (FR-001..FR-003, FR-005, FR-007, FR-008).
/// </summary>
public class FirstRunRestoreScreenTests : LocalizedTestContext
{
    private readonly IBackupService _backup = Substitute.For<IBackupService>();

    public FirstRunRestoreScreenTests()
    {
        Services.AddSingleton(_backup);
    }

    private static BackupManifest SampleManifest() => new()
    {
        SchemaVersion = "1.2.0",
        GeneratedAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
        ApplicationVersion = "9.9.9",
        EntityCounts = new Dictionary<string, int> { ["Members"] = 5, ["Fees"] = 0, ["Accounts"] = 3 },
    };

    private static InputFileContent SfbakFile() =>
        InputFileContent.CreateFromText("not a real backup", "handover.sfbak");

    [Fact]
    public void RestoreCheckbox_IsAPlainCheckbox_UncheckedByDefault()
    {
        var cut = Render<FirstRunRestoreScreen>();

        var checkbox = cut.Find("#restore-from-backup");
        Assert.Equal("checkbox", checkbox.GetAttribute("type"));
        Assert.False(checkbox.HasAttribute("checked"));
        Assert.Empty(cut.FindAll("#restore-file"));
    }

    [Fact]
    public void FileInput_IsShown_OnlyWhileTheCheckboxIsChecked()
    {
        var cut = Render<FirstRunRestoreScreen>();
        Assert.Empty(cut.FindAll("#restore-file"));

        cut.Find("#restore-from-backup").Change(true);
        Assert.Single(cut.FindAll("#restore-file"));

        cut.Find("#restore-from-backup").Change(false);
        Assert.Empty(cut.FindAll("#restore-file"));
    }

    [Fact]
    public void ValidFile_RendersTheContentsSummary()
    {
        _backup.GetManifestAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(SampleManifest());
        var cut = Render<FirstRunRestoreScreen>();
        cut.Find("#restore-from-backup").Change(true);

        cut.FindComponent<InputFile>().UploadFiles(SfbakFile());

        cut.WaitForAssertion(() =>
        {
            var summary = cut.Find(".first-run-restore-summary");
            Assert.Contains("9.9.9", summary.TextContent);            // originating ApplicationVersion
            Assert.Contains("Members", summary.TextContent);          // a record type with a non-zero count
            Assert.Contains("2026-01-02", summary.TextContent);       // GeneratedAt, local calendar day
        });
        cut.Find("#confirm-restore");
        cut.Find("#cancel-restore");
    }

    [Fact]
    public async Task ConfirmRestore_OnSuccess_RoutesToRestartRequired()
    {
        _backup.GetManifestAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(SampleManifest());
        _backup.ImportAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var cut = Render<FirstRunRestoreScreen>();
        cut.Find("#restore-from-backup").Change(true);
        cut.FindComponent<InputFile>().UploadFiles(SfbakFile());
        cut.WaitForAssertion(() => cut.Find("#confirm-restore"));

        // #confirm-restore runs ImportAsync on a real Task.Run hop — needs ClickAsync, not Click.
        await cut.Find("#confirm-restore").ClickAsync(new MouseEventArgs());

        await _backup.Received(1).ImportAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/restart-required", nav.Uri);
    }

    [Fact]
    public void CancelRestore_ClearsTheSelection_AndChangesNothing()
    {
        _backup.GetManifestAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(SampleManifest());
        var cut = Render<FirstRunRestoreScreen>();
        cut.Find("#restore-from-backup").Change(true);
        cut.FindComponent<InputFile>().UploadFiles(SfbakFile());
        cut.WaitForAssertion(() => cut.Find("#cancel-restore"));

        cut.Find("#cancel-restore").Click();

        Assert.Empty(cut.FindAll(".first-run-restore-summary"));
        Assert.Empty(cut.FindAll("#restore-file"));
        cut.Find("#continue-setup");
        _backup.DidNotReceive().ImportAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ContinueSetup_NavigatesToTheWizard()
    {
        var cut = Render<FirstRunRestoreScreen>();

        cut.Find("#continue-setup").Click();

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/setup", nav.Uri);
    }

    [Theory]
    [InlineData("The backup file is corrupt or not a StageFright backup.")]
    [InlineData("This backup was created by a newer version. Update the application and retry.")]
    public void InvalidFile_ShowsAnError_LeavesTheDatabaseUntouched_AndKeepsContinueAvailable(string coreMessage)
    {
        _backup.GetManifestAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task<BackupManifest>>(_ => throw new ImportException(coreMessage));
        var cut = Render<FirstRunRestoreScreen>();
        cut.Find("#restore-from-backup").Change(true);

        cut.FindComponent<InputFile>().UploadFiles(SfbakFile());

        cut.WaitForAssertion(() =>
        {
            var error = cut.Find(".first-run-restore-error");
            Assert.Contains(coreMessage, error.TextContent);
        });
        Assert.Empty(cut.FindAll(".first-run-restore-summary"));
        cut.Find("#continue-setup");
        _backup.DidNotReceive().ImportAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
