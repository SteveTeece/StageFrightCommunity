using StageFright.Core.Entities;
using StageFright.Core.Modules.Finance;

namespace StageFright.Core.Contracts;

/// <summary>
/// Application service for recording expense payments out of a bank/cash account.
/// </summary>
public interface IExpensePaymentService
{
    /// <summary>
    /// Records an expense payment with a matching GL pair under an ExpensePayment journal
    /// entry: Debit the Expense account / Credit the chosen bank account.
    /// Throws <see cref="Core.Exceptions.ValidationException"/> on bad input.
    /// </summary>
    Task RecordExpenseAsync(RecordExpenseRequest request, CancellationToken ct = default);

    /// <summary>Returns all active Expense accounts excluding system accounts.</summary>
    Task<IReadOnlyList<Account>> GetExpenseAccountsAsync(CancellationToken ct = default);
}
