namespace StageFright.Core.Enums;

/// <summary>
/// The financial workflow that produced a journal entry. Persisted as a string.
/// </summary>
public enum JournalEntryType
{
    /// <summary>Non-member income deposited to a bank account (raffles, donations, fundraising).</summary>
    Income,

    /// <summary>Money paid out of a bank account against an expense account.</summary>
    ExpensePayment,

    /// <summary>Movement of funds between two bank accounts.</summary>
    Transfer,

    /// <summary>Undeposited funds (cash and cheques received) deposited into a bank account.</summary>
    BankDeposit,

    /// <summary>Manually entered multi-line general journal.</summary>
    GeneralJournal,

    /// <summary>One-off opening balances posted by the opening balances wizard.</summary>
    OpeningBalance,

    /// <summary>Reversal of a previously recorded bank deposit, returning its items to awaiting deposit.</summary>
    BankDepositReversal,

    /// <summary>Cash withdrawn from a bank account into Petty Cash.</summary>
    CashWithdrawal
}
