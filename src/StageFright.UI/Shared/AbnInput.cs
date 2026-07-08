using System.Text;
using Microsoft.AspNetCore.Components.Forms;

namespace StageFright.UI.Shared;

/// <summary>
/// Text input for an Australian Business Number. Displays the standard "XX XXX XXX XXX"
/// digit grouping while typing; the bound/persisted value is always the plain 11-digit
/// string (no spaces). Validity is enforced separately by StageFright.Core.Modules.Settings.AbnAttribute.
/// </summary>
public class AbnInput : InputText
{
    protected override string? FormatValueAsString(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        var digits = new StringBuilder(11);
        foreach (var c in value)
        {
            if (char.IsDigit(c) && digits.Length < 11)
                digits.Append(c);
        }

        var sb = new StringBuilder(digits.Length + 3);
        for (var i = 0; i < digits.Length; i++)
        {
            if (i is 2 or 5 or 8)
                sb.Append(' ');
            sb.Append(digits[i]);
        }

        return sb.ToString();
    }

    protected override bool TryParseValueFromString(string? value, out string? result, out string? validationErrorMessage)
    {
        var digits = new StringBuilder(11);
        if (value is not null)
        {
            foreach (var c in value)
            {
                if (char.IsDigit(c) && digits.Length < 11)
                    digits.Append(c);
            }
        }

        result = digits.Length == 0 ? null : digits.ToString();
        validationErrorMessage = null;
        return true;
    }
}
