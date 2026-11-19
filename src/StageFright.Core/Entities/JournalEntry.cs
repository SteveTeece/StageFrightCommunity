using StageFright.Core.Enums;

namespace StageFright.Core.Entities;

/// <summary>
/// Header row grouping the GL transaction lines posted by a single financial workflow
/// (income, expense payment, transfer, general journal, opening balances).
/// Immutable — NO soft-delete fields per Constitution §3.4; corrections use reversing journals.
/// </summary>
public class JournalEntry
{
    /// <summary>Primary key (GUID).</summary>
    public Guid Id { get; set; }

    /// <summary>The workflow that produced this entry.</summary>
    public JournalEntryType Type { get; set; }

    /// <summary>UTC date the entry is posted at (matches the date on its GL lines).</summary>
    public DateTime Date { get; set; }

    /// <summary>Optional description shared by the entry's GL lines.</summary>
    public string? Description { get; set; }

    /// <summary>UTC timestamp when the record was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Optional FK to the external organisation this entry was paid to or received from.
    /// Only expense payment and income entries carry a link. Set once when the entry is
    /// created and never added, changed or cleared afterwards (spec 032 FR-010).
    /// </summary>
    public Guid? ExternalOrganisationId { get; set; }

    // --- Navigation ---

    /// <summary>The linked external organisation, when <see cref="ExternalOrganisationId"/> is set.</summary>
    public ExternalOrganisation? ExternalOrganisation { get; set; }

    /// <summary>The balanced GL lines posted under this entry.</summary>
    public List<Transaction> Transactions { get; set; } = new();
}
