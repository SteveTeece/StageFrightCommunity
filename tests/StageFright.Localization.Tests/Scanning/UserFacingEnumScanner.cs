namespace StageFright.Localization.Tests.Scanning;

/// <summary>
/// Enumerates the members of a user-facing enum type by reflection and builds its expected
/// resource key. Used by the enum-coverage guard (Phase 3+) to assert EnumsResource has an
/// Enum_&lt;Type&gt;_&lt;Member&gt; entry for every member of each allow-listed enum
/// (MemberStatus, FeeType, Theme, etc. — FR-024).
/// </summary>
public static class UserFacingEnumScanner
{
    public static IReadOnlyList<string> GetMemberNames(Type enumType)
    {
        if (!enumType.IsEnum)
            throw new ArgumentException($"{enumType.Name} is not an enum type.", nameof(enumType));

        return System.Enum.GetNames(enumType);
    }

    public static string BuildResourceKey(Type enumType, string memberName) => $"Enum_{enumType.Name}_{memberName}";
}
