namespace StageFright.Core.Enums;

/// <summary>Determines the GL account range for a category (Income → 10xx, Expense → 20xx).</summary>
public enum CategoryType
{
    /// <summary>Income category; GL accounts assigned in the 1000–1999 range.</summary>
    Income,

    /// <summary>Expense category; GL accounts assigned in the 2000–2999 range.</summary>
    Expense
}
