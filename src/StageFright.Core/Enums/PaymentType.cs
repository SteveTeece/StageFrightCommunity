namespace StageFright.Core.Enums;

/// <summary>Reporting classification of what a payment is for (distinct from GL category).</summary>
public enum PaymentType
{
    /// <summary>Payment against an annual membership fee.</summary>
    Annual,

    /// <summary>Payment against one or more attendance fees.</summary>
    Attendance,

    /// <summary>Payment not specifically for annual or attendance fees.</summary>
    Other
}
