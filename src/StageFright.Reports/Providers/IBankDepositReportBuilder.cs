using StageFright.Core.Modules.Finance;
using StageFright.Reports.Models;

namespace StageFright.Reports.Providers;

/// <summary>
/// Builds the printable report for one bank deposit (issue #399). It is not an
/// <see cref="Registry.IReportProvider"/>: a single deposit is opened from the Bank Deposits
/// report, never from the Reports menu.
/// </summary>
public interface IBankDepositReportBuilder
{
    /// <summary>Returns the deposit's items, cash and total as report data for the PDF renderer.</summary>
    ReportData Build(DepositDetail deposit);
}
