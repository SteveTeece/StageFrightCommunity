namespace StageFright.Maui.Services;

using Entities;
using Exceptions;
using StageFright.Data.Repositories;
using System;
using System.Linq;
using System.Threading.Tasks;

/// <summary>Service for application settings with singleton pattern (only one Settings record exists).</summary>
public class SettingsService : ISettingsService
{
	private readonly ISettingsRepository _settingsRepository;

	public SettingsService(ISettingsRepository settingsRepository)
	{
		_settingsRepository = settingsRepository;
	}

	public async Task<Settings> GetSettingsAsync()
	{
		var settings = await _settingsRepository.GetAllAsync();
		var settingsList = settings.ToList();

		if (settingsList.Count == 0)
			throw new EntityNotFoundException("Settings not initialized. Please run setup wizard first.");

		// Return the first (and should be only) settings record
		return settingsList[0];
	}

	public async Task UpdateSettingsAsync(Settings settings)
	{
		if (settings.Id == Guid.Empty)
			throw new ValidationException("Settings ID is required.");

		var existing = await _settingsRepository.GetByIdAsync(settings.Id);
		if (existing == null)
			throw new EntityNotFoundException($"Settings with ID {settings.Id} not found.");

		// Preserve immutable fields
		settings.CreatedAt = existing.CreatedAt;
		settings.IsDeleted = existing.IsDeleted;
		settings.DeletedAt = existing.DeletedAt;
		settings.DeletedBy = existing.DeletedBy;

		await _settingsRepository.UpdateAsync(settings);
	}

	public async Task InitializeDefaultSettingsAsync(
		string organizationName,
		decimal annualFee,
		decimal attendanceFee)
	{
		if (string.IsNullOrWhiteSpace(organizationName))
			throw new ValidationException("Organization name is required.");

		if (annualFee < 0)
			throw new ValidationException("Annual fee cannot be negative.");

		if (attendanceFee < 0)
			throw new ValidationException("Attendance fee cannot be negative.");

		int renewalMonth = 7; // Default to July

		// Check if settings already exist
		var existing = await _settingsRepository.GetAllAsync();
		var existingList = existing.ToList();

		if (existingList.Count > 0)
			throw new ValidationException("Settings already initialized. Cannot reinitialize.");

		var settings = new Settings
		{
			Id = Guid.NewGuid(),
			OrganizationName = organizationName,
			AnnualFee = annualFee,
			AttendanceFee = attendanceFee,
			RenewalMonth = renewalMonth,
			CreatedAt = DateTime.UtcNow,
			IsDeleted = false
		};

		await _settingsRepository.CreateAsync(settings);
	}
}
