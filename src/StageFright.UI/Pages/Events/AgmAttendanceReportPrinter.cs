using Microsoft.Extensions.Logging;
using StageFright.Core.Contracts;
using StageFright.Reports.Rendering;

namespace StageFright.UI.Pages.Events;

/// <summary>
/// Shared generate → render → write-temp-file → launch flow for the AGM attendance report PDF,
/// used by both AgmList (the dedicated AGMs screen) and EventList (the combined All Events
/// screen, spec 023) so the two entry points to the same report stay in sync.
/// </summary>
public static class AgmAttendanceReportPrinter
{
    /// <summary>
    /// Runs the print flow for the given AGM. Returns a user-facing message to show (empty-members
    /// notice or error), or null on success.
    /// </summary>
    public static async Task<string?> PrintAsync(
        Guid agmId,
        IAgmAttendanceSheetService sheetService,
        IAgmAttendanceSheetPdfRenderer pdfRenderer,
        ISettingsService settingsService,
        ILogger logger)
    {
        try
        {
            var sheetData = await sheetService.GenerateAsync(agmId);

            if (sheetData.Members.Count == 0)
            {
                return "No attendance records found — nothing to print.";
            }

            var settings = await settingsService.GetAsync();
            var orgName = settings?.OrganizationName ?? string.Empty;
            var bytes = pdfRenderer.Render(sheetData, orgName);
            var tempPath = Path.Combine(Path.GetTempPath(), $"agm-attendance-report_{Guid.NewGuid():N}.pdf");
            File.WriteAllBytes(tempPath, bytes);
#pragma warning disable CA1416
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(tempPath) { UseShellExecute = true });
#pragma warning restore CA1416
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to print attendance report for AGM {AgmId}", agmId);
            return "Unable to print attendance report. Please try again.";
        }
    }
}
