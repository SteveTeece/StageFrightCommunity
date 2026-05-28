using StageFright.Data.Repositories;
using StageFright.Plugins.Contracts;

namespace StageFright.Plugins.Providers;

/// <summary>
/// Dashboard tile provider for Members module.
/// Displays active and inactive member counts.
/// </summary>
public class MembersDashboardTileProvider : IDashboardTileProvider
{
    private readonly IMemberRepository _memberRepository;

    public string TileId => "members-tile";
    public string DisplayName => "Members";
    public string ModuleName => "Members";
    public int DisplayOrder => 1;

    public MembersDashboardTileProvider(IMemberRepository memberRepository)
    {
        _memberRepository = memberRepository ?? throw new ArgumentNullException(nameof(memberRepository));
    }

    public async Task<TileData> GenerateAsync()
    {
        try
        {
            var members = await _memberRepository.GetAllAsync();
            var activeCount = members.Count(m => m.Status == "Active");
            var inactiveCount = members.Count(m => m.Status == "Inactive");

            return new TileData
            {
                Title = "Members",
                Content = $"Active: {activeCount} | Inactive: {inactiveCount}",
                Metrics = new Dictionary<string, string>
                {
                    { "Active", activeCount.ToString() },
                    { "Inactive", inactiveCount.ToString() }
                },
                IsError = false
            };
        }
        catch (Exception ex)
        {
            return new TileData
            {
                Title = "Members",
                IsError = true,
                ErrorMessage = $"Error loading members data: {ex.Message}"
            };
        }
    }
}
