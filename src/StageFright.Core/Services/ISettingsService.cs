namespace StageFright.Core.Services;

using Entities;
using System.Threading.Tasks;

/// <summary>Service for settings management.</summary>
public interface ISettingsService
{
	Task<Settings> GetSettingsAsync();
	Task UpdateSettingsAsync(Settings settings);
	Task InitializeDefaultSettingsAsync(string organizationName, decimal annualFee, decimal attendanceFee);
}
