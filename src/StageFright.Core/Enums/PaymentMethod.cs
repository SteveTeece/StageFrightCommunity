namespace StageFright.Core.Enums;

/// <summary>
/// Represents the method by which a payment was made.
/// </summary>
public enum PaymentMethod
{
	/// <summary>Cash payment.</summary>
	Cash,

	/// <summary>Check payment.</summary>
	Check,

	/// <summary>Card payment.</summary>
	Card,

	/// <summary>Electronic bank transfer.</summary>
	ElectronicTransfer,

	/// <summary>Other payment methods.</summary>
	Other
}
