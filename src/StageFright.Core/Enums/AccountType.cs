namespace StageFright.Core.Enums;

/// <summary>
/// Classifies an account in the chart of accounts and determines its number range
/// (Australian small-NFP convention):
/// Assets 1000–1999, Liabilities 2000–2999, Equity 3000–3999,
/// Income 4000–4999, Expenses 6000–6999.
/// Persisted as a string; the legacy values "Income"/"Expense" survive unchanged.
/// </summary>
public enum AccountType
{
    /// <summary>Income account; numbers assigned in the 4000–4999 range.</summary>
    Income,

    /// <summary>Expense account; numbers assigned in the 6000–6999 range.</summary>
    Expense,

    /// <summary>Asset account; numbers assigned in the 1000–1999 range. Bank/cash accounts live here.</summary>
    Asset,

    /// <summary>Liability account; numbers assigned in the 2000–2999 range.</summary>
    Liability,

    /// <summary>Equity account; numbers assigned in the 3000–3999 range.</summary>
    Equity
}
