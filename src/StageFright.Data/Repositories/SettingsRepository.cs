namespace StageFright.Data.Repositories;

using Microsoft.EntityFrameworkCore;
using StageFright.Core.Entities;
using StageFright.Data.Context;
using System;
using System.Threading.Tasks;

/// <summary>Repository implementation for Settings entity (singleton).</summary>
public class SettingsRepository : BaseRepository<Settings>, ISettingsRepository
{
	public SettingsRepository(StageFrightContext context) : base(context) { }

	public async Task<Settings?> GetSettingsAsync()
	{
		return await _dbSet.FirstOrDefaultAsync();
	}

	public async Task UpdateSettingsAsync(Settings settings)
	{
		var existing = await GetSettingsAsync();
		if (existing != null)
		{
			existing.OrganizationName = settings.OrganizationName;
			existing.AnnualFee = settings.AnnualFee;
			existing.AttendanceFee = settings.AttendanceFee;
			existing.RenewalMonth = settings.RenewalMonth;
			existing.CommitteeRenewalMonth = settings.CommitteeRenewalMonth;
			existing.MaxAgeRange = settings.MaxAgeRange;
			existing.MinimumMemberAge = settings.MinimumMemberAge;
			existing.Theme = settings.Theme;
			existing.ModifiedAt = DateTime.UtcNow;
			await UpdateAsync(existing);
		}
		else
		{
			settings.CreatedAt = DateTime.UtcNow;
			settings.ModifiedAt = DateTime.UtcNow;
			await CreateAsync(settings);
		}
	}
}
