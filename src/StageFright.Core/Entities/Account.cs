using StageFright.Core.Enums;

namespace StageFright.Core.Entities;

/// <summary>
/// An account in the chart of accounts with an auto-assigned account number.
/// System accounts (IsSystem=true) are seeded and cannot be edited or archived.
/// User accounts are archivable only when no Transaction references them.
/// Archive is permanently blocked once any Transaction references the account.
/// </summary>
public class Account
{
    /// <summary>Primary key (GUID).</summary>
    public Guid Id { get; set; }

    /// <summary>Display name of the account. Required.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Account classification. Set at creation; immutable (drives the account number range).
    /// </summary>
    public AccountType Type { get; set; }

    /// <summary>
    /// Account number. Auto-assigned by AccountNumberAssignmentService at creation
    /// (max-in-range + 1): Assets 1000s (user bank accounts from 1110), Liabilities 2000s,
    /// Equity 3000s, Income 4000s, Expenses 6000s.
    /// Fixed system accounts: Cash on Hand=1100, Member Receivable=1200, Bad Debt=6999,
    /// Tax Collected=2310 (Liability), Tax Receivable=2320 (Asset — recoverable input tax,
    /// kept in the 2000s as a documented exception), Opening Balance Equity=3100,
    /// Accumulated Surplus=3200.
    /// Immutable once assigned.
    /// </summary>
    public string AccountNumber { get; set; } = string.Empty;

    /// <summary>User-defined sort order. Supports drag-reorder in the UI.</summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// True for seeded system accounts. System accounts cannot be edited, archived, or reordered.
    /// </summary>
    public bool IsSystem { get; set; }

    /// <summary>
    /// True when this Asset account represents a bank or cash account.
    /// Only settable on Asset accounts; drives the "pay from / deposit to / transfer /
    /// reconcile" account pickers.
    /// </summary>
    public bool IsBankAccount { get; set; }

    // --- Soft-delete fields ---

    /// <summary>True when the account has been archived. Permanently blocked if referenced by any Transaction.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>UTC timestamp when the account was archived.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>Identity that archived the account.</summary>
    public string? DeletedBy { get; set; }

    // --- Audit fields ---

    /// <summary>UTC timestamp when the record was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp of the most recent update.</summary>
    public DateTime UpdatedAt { get; set; }

    // --- Navigation ---

    /// <summary>All GL transactions posted to this account.</summary>
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
