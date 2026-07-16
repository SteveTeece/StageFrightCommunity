using StageFright.Core.Enums;

namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Aggregates a Chart of Accounts account's display fields with its GL-derived current
/// balance, for grid binding. Balance is null and HasError is true when the underlying
/// GL calculation fails for this account; every other account row is unaffected.
/// </summary>
public class AccountBalance
{
    /// <summary>Account primary key.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Account number, e.g. "1100".</summary>
    public string AccountNumber { get; set; } = string.Empty;

    /// <summary>Account display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Account classification; drives the credit-normal sign convention applied to Balance.</summary>
    public AccountType Type { get; set; }

    /// <summary>True for seeded system accounts (read-only in the UI).</summary>
    public bool IsSystem { get; set; }

    /// <summary>True when this Asset account represents a bank or cash account.</summary>
    public bool IsBankAccount { get; set; }

    /// <summary>
    /// Current balance as of now: Σdebits − Σcredits, sign-flipped for credit-normal types
    /// (Liability, Equity, Income). Null when HasError is true.
    /// </summary>
    public decimal? Balance { get; set; }

    /// <summary>True when this account's balance could not be computed; Balance is null in that case.</summary>
    public bool HasError { get; set; }
}
