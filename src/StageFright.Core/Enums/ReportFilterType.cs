namespace StageFright.Core.Enums;

/// <summary>Input control type used for a report filter parameter.</summary>
public enum ReportFilterType
{
    /// <summary>Free-text input.</summary>
    Text,

    /// <summary>Date picker (returns ISO date string).</summary>
    Date,

    /// <summary>Date-range picker (start + end dates).</summary>
    DateRange,

    /// <summary>Drop-down list from a fixed set of options.</summary>
    Select,

    /// <summary>Boolean checkbox (true / false).</summary>
    Boolean
}
