namespace StageFright.Core.Enums;

/// <summary>Display format applied to cell values in a report column.</summary>
public enum ReportColumnFormat
{
    /// <summary>Plain text; no formatting applied.</summary>
    Text,

    /// <summary>Currency with two decimal places (e.g., $1,234.56).</summary>
    Currency,

    /// <summary>Short date (e.g., 2026-06-12).</summary>
    Date,

    /// <summary>Integer with thousands separator where applicable.</summary>
    Integer,

    /// <summary>Percentage value (e.g., 75.0%).</summary>
    Percentage
}
