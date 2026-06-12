namespace StageFright.Core.Enums;

/// <summary>Method used to tender a payment.</summary>
public enum PaymentMethod
{
    /// <summary>Cash payment received in person.</summary>
    Cash,

    /// <summary>Cheque / check payment.</summary>
    Check,

    /// <summary>Credit or debit card payment.</summary>
    Card,

    /// <summary>Electronic bank transfer (EFT, direct deposit, etc.).</summary>
    ElectronicTransfer,

    /// <summary>Any other payment method not listed above.</summary>
    Other
}
