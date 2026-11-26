using StageFright.Core.Exceptions;

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
}
