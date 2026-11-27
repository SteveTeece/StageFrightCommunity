using StageFright.Core.Exceptions;
using StageFright.Core.Localization;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Shared;

/// <summary>
/// The {Detail} text of an error alert. A StageFright exception's message is written for the user at a
/// layer boundary, so it is shown as is. Any other exception would show framework or database text, so
/// the caller logs it and shows its generic text instead.
/// </summary>
public static class ErrorDetail
{
    public static string For(Exception ex, string unexpectedText) =>
        ex.GetType().Namespace == typeof(ValidationException).Namespace ? ex.Message : unexpectedText;

    /// <summary>As <see cref="For(Exception, string)"/>, with the shared "unexpected error" text.</summary>
    public static string For(Exception ex, ILocalizer loc) =>
        For(ex, loc.Get<SharedResource>("Shared_Error_UnexpectedDetail"));
}
