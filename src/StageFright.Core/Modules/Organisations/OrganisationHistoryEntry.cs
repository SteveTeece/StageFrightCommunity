using StageFright.Core.Enums;

namespace StageFright.Core.Modules.Organisations;

/// <summary>
/// One linked expense or income entry in an organisation's financial history (spec 032 FR-011).
/// Read model only, never persisted. <c>Amount</c> is the sum of the entry's debit lines, which
/// counts the bank-side movement once (FR-012). <c>Type</c> is <see cref="JournalEntryType.ExpensePayment"/>
/// (money paid to the organisation) or <see cref="JournalEntryType.Income"/> (money received from it).
/// </summary>
public sealed record OrganisationHistoryEntry(
    Guid JournalEntryId,
    DateTime Date,
    string? Description,
    string AccountName,
    JournalEntryType Type,
    decimal Amount);
