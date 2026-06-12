namespace StageFright.Core.Enums;

/// <summary>Classification of a fee record.</summary>
public enum FeeType
{
    /// <summary>Annual membership fee applied once per year per eligible active member.</summary>
    Annual,

    /// <summary>Per-rehearsal attendance fee created automatically when attendance is recorded.</summary>
    Attendance,

    /// <summary>Miscellaneous fee not fitting the above categories.</summary>
    Other
}
