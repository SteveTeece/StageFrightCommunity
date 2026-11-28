namespace StageFright.Core.Contracts;

/// <summary>
/// Keeps the names of the built-in system accounts (Petty Cash, Undeposited Funds, Member
/// Receivable, Tax Collected, Tax Receivable, Opening Balance Equity, Accumulated Surplus, Bad
/// Debt Expense) in the language the app is using (issue #394).
/// </summary>
public interface ISystemAccountNameService
{
    /// <summary>
    /// Renames each system account to its name in the current UI culture. Run at startup and
    /// whenever the display language changes. Never throws: a failure is logged and the names stay
    /// as they were until the next time the language is applied.
    /// </summary>
    Task ApplyAsync(CancellationToken ct = default);
}
