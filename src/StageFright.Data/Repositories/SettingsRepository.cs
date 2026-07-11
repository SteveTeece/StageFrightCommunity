using Microsoft.EntityFrameworkCore;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Exceptions;

namespace StageFright.Data.Repositories;

public class SettingsRepository : ISettingsRepository
{
    private readonly StageFrightDbContext _db;

    public SettingsRepository(StageFrightDbContext db)
    {
        _db = db;
    }

    public async Task<Settings?> GetAsync(CancellationToken ct = default)
    {
        return await _db.Settings.FirstOrDefaultAsync(ct);
    }

    public async Task SaveAsync(Settings settings, CancellationToken ct = default)
    {
        try
        {
            var existing = await _db.Settings.FirstOrDefaultAsync(ct);
            if (existing is null)
            {
                await _db.Settings.AddAsync(settings, ct);
            }
            else
            {
                existing.OrganizationName = settings.OrganizationName;
                existing.AnnualFee = settings.AnnualFee;
                existing.AttendanceFee = settings.AttendanceFee;
                existing.MembershipRenewalMonth = settings.MembershipRenewalMonth;
                existing.CommitteeRenewalMonth = settings.CommitteeRenewalMonth;
                existing.MaxAgeRangeYears = settings.MaxAgeRangeYears;
                existing.MinimumMemberAge = settings.MinimumMemberAge;
                existing.Theme = settings.Theme;
                existing.ShowParticipationGraphs = settings.ShowParticipationGraphs;
                existing.FinancialYearStartMonth = settings.FinancialYearStartMonth;
                existing.IsGstRegistered = settings.IsGstRegistered;
                existing.AnnualFeeGstCode = settings.AnnualFeeGstCode;
                existing.AttendanceFeeGstCode = settings.AttendanceFeeGstCode;
                existing.LastCommitteeResetYear = settings.LastCommitteeResetYear;
                existing.SchemaVersion = settings.SchemaVersion;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not DataAccessException)
        {
            throw new DataAccessException(ex.Message, nameof(Settings), nameof(SaveAsync), null, ex);
        }
    }
}
