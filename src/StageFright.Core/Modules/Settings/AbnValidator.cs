namespace StageFright.Core.Modules.Settings;

/// <summary>
/// Validates Australian Business Numbers using the ATO's published weighted-modulus-89 checksum.
/// Accepts only a plain 11-digit string (no spaces or other separators).
/// </summary>
public static class AbnValidator
{
    private static readonly int[] Weights = { 10, 1, 3, 5, 7, 9, 11, 13, 15, 17, 19 };

    public static bool IsValid(string? abn)
    {
        if (string.IsNullOrEmpty(abn) || abn.Length != 11)
            return false;

        var sum = 0;
        for (var i = 0; i < 11; i++)
        {
            if (!char.IsDigit(abn[i]))
                return false;

            var digit = abn[i] - '0';
            if (i == 0)
                digit -= 1;

            sum += digit * Weights[i];
        }

        return sum != 0 && sum % 89 == 0;
    }
}
