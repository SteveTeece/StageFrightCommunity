namespace StageFright.Core.Modules.Members;

/// <summary>
/// Splits a legacy combined name into first/last name components, following the same
/// trim -> collapse-whitespace -> split-on-first-space -> truncate-to-100 rule as the
/// SplitMemberNameIntoFirstLastName migration's SQL implementation.
/// </summary>
public static class MemberNameSplitter
{
    private const int MaxLength = 100;

    public static (string FirstName, string LastName) Split(string combinedName)
    {
        var normalized = (combinedName ?? string.Empty).Trim();
        while (normalized.Contains("  "))
            normalized = normalized.Replace("  ", " ");

        var spaceIndex = normalized.IndexOf(' ');
        if (spaceIndex < 0)
            return (Truncate(normalized), string.Empty);

        var firstName = normalized[..spaceIndex];
        var lastName = normalized[(spaceIndex + 1)..];
        return (Truncate(firstName), Truncate(lastName));
    }

    private static string Truncate(string value) =>
        value.Length > MaxLength ? value[..MaxLength] : value;
}
