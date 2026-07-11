namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Input model for recording a manual multi-line general journal.
/// </summary>
public class RecordJournalRequest
{
    /// <summary>UTC date the journal is posted at. Required.</summary>
    public DateTime Date { get; set; }

    /// <summary>Description of why the journal is being posted. Required for manual journals.</summary>
    public string? Description { get; set; }

    /// <summary>The journal lines. At least two; Σdebits must equal Σcredits.</summary>
    public List<JournalLine> Lines { get; set; } = new();
}
