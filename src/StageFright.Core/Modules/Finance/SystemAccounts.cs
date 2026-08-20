namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Single source of truth for the well-known GUIDs and account numbers of seeded
/// system accounts. All posting services reference these instead of duplicating
/// their own constants.
/// </summary>
public static class SystemAccounts
{
    /// <summary>Cash on Hand — Asset 1100, IsBankAccount. The default deposit/payment account.</summary>
    public static readonly Guid CashId = new("00000000-0000-0000-0000-000000000001");

    /// <summary>Member Receivable — Asset 1200. The authoritative per-member balance account.</summary>
    public static readonly Guid MemberReceivableId = new("00000000-0000-0000-0000-000000000002");

    /// <summary>Bad Debt Expense — Expense 6999. Target of reactivation forgiveness write-offs.</summary>
    public static readonly Guid BadDebtId = new("00000000-0000-0000-0000-000000000003");

    /// <summary>Tax Collected — Liability 2310. Tax clearing account for sales.</summary>
    public static readonly Guid TaxCollectedId = new("00000000-0000-0000-0000-000000000004");

    /// <summary>Tax Paid — Liability 2320. Tax clearing account for purchases.</summary>
    public static readonly Guid TaxPaidId = new("00000000-0000-0000-0000-000000000005");

    /// <summary>Opening Balance Equity — Equity 3100. Plug account for the opening balances wizard.</summary>
    public static readonly Guid OpeningBalanceEquityId = new("00000000-0000-0000-0000-000000000006");

    /// <summary>Accumulated Surplus — Equity 3200. Presentation account for retained surplus.</summary>
    public static readonly Guid AccumulatedSurplusId = new("00000000-0000-0000-0000-000000000007");

    /// <summary>Account number of Cash on Hand.</summary>
    public const string CashNumber = "1100";

    /// <summary>Account number of Member Receivable.</summary>
    public const string MemberReceivableNumber = "1200";

    /// <summary>Account number of Bad Debt Expense.</summary>
    public const string BadDebtNumber = "6999";

    /// <summary>Account number of Tax Collected.</summary>
    public const string TaxCollectedNumber = "2310";

    /// <summary>Account number of Tax Paid.</summary>
    public const string TaxPaidNumber = "2320";

    /// <summary>Account number of Opening Balance Equity.</summary>
    public const string OpeningBalanceEquityNumber = "3100";

    /// <summary>Account number of Accumulated Surplus.</summary>
    public const string AccumulatedSurplusNumber = "3200";
}
