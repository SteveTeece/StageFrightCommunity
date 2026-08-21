using StageFright.Core.Enums;

namespace StageFright.Core.Modules.Settings;

/// <summary>
/// One Chart of Accounts entry queued during first-run setup (spec 017 FR-012/FR-013).
/// <see cref="ClientId"/> is a wizard-side local reference, not a real <c>Account.Id</c> —
/// the setup wizard uses it as the placeholder <c>OpeningBalanceEntry.AccountId</c> for a
/// balance entered against this not-yet-created account, and <see cref="SetupService"/>
/// resolves it to the real id it assigns via <see cref="Contracts.IAccountService.CreateAsync"/>
/// before posting any queued opening balances (data-model.md's account-reference resolution).
/// </summary>
public record QueuedAccountRequest(
    Guid ClientId,
    string Name,
    AccountType Type,
    bool IsBankAccount);
