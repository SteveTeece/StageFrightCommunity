using System.ComponentModel.DataAnnotations;
using StageFright.Core.Enums;

namespace StageFright.UI.Shared;

/// <summary>
/// Input model for the shared <see cref="AddAccountForm"/> — used both for immediate
/// creation (the standalone Chart of Accounts page) and deferred queuing (the setup
/// wizard's Chart of Accounts tab). Extracted from <c>ChartOfAccountsPage</c>'s former
/// private nested model so both callers share one validated shape.
/// </summary>
public sealed class NewAccountModel
{
    [Required(ErrorMessageResourceType = typeof(FormValidationMessages), ErrorMessageResourceName = nameof(FormValidationMessages.Validation_Account_NameRequired))]
    [MaxLength(100, ErrorMessageResourceType = typeof(FormValidationMessages), ErrorMessageResourceName = nameof(FormValidationMessages.Shared_Validation_AccountNameTooLong))]
    public string? Name { get; set; }

    [Required]
    public AccountType Type { get; set; } = AccountType.Income;

    public bool IsBankAccount { get; set; }
}
