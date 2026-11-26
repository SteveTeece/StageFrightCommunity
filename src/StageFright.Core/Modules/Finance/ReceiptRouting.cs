using StageFright.Core.Enums;

namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Pure rule for where money received by a given <see cref="PaymentMethod"/> is debited
/// (spec 033, research §6). Stateless so it needs no interface or mock to test.
/// </summary>
public static class ReceiptRouting
{
    /// <summary>
    /// Returns the account to debit: Petty Cash for cash, Undeposited Funds for cheque and other,
    /// and the chosen bank account for card and electronic transfer.
    /// Callers validate that the bank account exists and is a bank account other than Petty Cash.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="method"/> is card or electronic transfer and <paramref name="bankAccountId"/> is null.
    /// </exception>
    public static Guid Destination(PaymentMethod method, Guid? bankAccountId)
    {
        switch (method)
        {
            case PaymentMethod.Cash:
                return SystemAccounts.CashId;
            case PaymentMethod.Check:
            case PaymentMethod.Other:
                return SystemAccounts.UndepositedFundsId;
            default:
                return bankAccountId
                    ?? throw new ArgumentException("A bank account is required for this payment method.", nameof(bankAccountId));
        }
    }

    /// <summary>True when the receipt waits in Undeposited Funds as a received item (cheque and other).</summary>
    public static bool CreatesReceivedItem(PaymentMethod method) =>
        method == PaymentMethod.Check || method == PaymentMethod.Other;
}
