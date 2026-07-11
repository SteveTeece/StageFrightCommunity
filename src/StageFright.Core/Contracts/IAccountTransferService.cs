using StageFright.Core.Modules.Finance;

namespace StageFright.Core.Contracts;

/// <summary>
/// Application service for transferring funds between two bank/cash accounts.
/// </summary>
public interface IAccountTransferService
{
    /// <summary>
    /// Records a transfer with a matching GL pair under a Transfer journal entry:
    /// Debit the destination account / Credit the source account. Both accounts must
    /// be bank accounts and must differ.
    /// Throws <see cref="Core.Exceptions.ValidationException"/> on bad input.
    /// </summary>
    Task RecordTransferAsync(RecordTransferRequest request, CancellationToken ct = default);
}
