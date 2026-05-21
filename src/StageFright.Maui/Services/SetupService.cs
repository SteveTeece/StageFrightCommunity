namespace StageFright.Maui.Services;

using System;
using System.Threading.Tasks;
using Core.Services;
using Entities;
using Exceptions;

/// <summary>Service for first-run setup wizard initialization.</summary>
public class SetupService : ISetupService
{
	private readonly ISettingsService _settingsService;
	private readonly ICategoryService _categoryService;

	public SetupService(
		ISettingsService settingsService,
		ICategoryService categoryService)
	{
		_settingsService = settingsService;
		_categoryService = categoryService;
	}

	/// <summary>Initializes application with organization details and default categories.</summary>
	public async Task InitializeApplicationAsync(
		string organizationName,
		decimal annualFee,
		decimal attendanceFee,
		int renewalMonth = 7)
	{
		if (string.IsNullOrWhiteSpace(organizationName))
			throw new ValidationException("Organization name is required.");

		try
		{
			// Initialize settings
			await _settingsService.InitializeDefaultSettingsAsync(
				organizationName,
				annualFee,
				attendanceFee);

			// Create default categories for Income
			await _categoryService.CreateCategoryAsync(new Category
			{
				Name = "Performance",
				Type = "Income",
				Description = "Income from performances"
			});

			await _categoryService.CreateCategoryAsync(new Category
			{
				Name = "Fundraiser",
				Type = "Income",
				Description = "Income from fundraiser events"
			});

			await _categoryService.CreateCategoryAsync(new Category
			{
				Name = "Donation",
				Type = "Income",
				Description = "Member donations and other income"
			});

			// Create default categories for Expense
			await _categoryService.CreateCategoryAsync(new Category
			{
				Name = "Equipment",
				Type = "Expense",
				Description = "Equipment purchases and maintenance"
			});

			await _categoryService.CreateCategoryAsync(new Category
			{
				Name = "Facilities",
				Type = "Expense",
				Description = "Venue rental and facility costs"
			});

			await _categoryService.CreateCategoryAsync(new Category
			{
				Name = "Personnel",
				Type = "Expense",
				Description = "Staff and instructor payments"
			});

			// Create default event types - stored as categories
			await _categoryService.CreateCategoryAsync(new Category
			{
				Name = "Performance",
				Type = "EventType",
				Description = "Regular performance events"
			});

			await _categoryService.CreateCategoryAsync(new Category
			{
				Name = "Eisteddfod",
				Type = "EventType",
				Description = "Eisteddfod competition events"
			});

			await _categoryService.CreateCategoryAsync(new Category
			{
				Name = "Fundraiser",
				Type = "EventType",
				Description = "Fundraiser events"
			});

			await _categoryService.CreateCategoryAsync(new Category
			{
				Name = "Promotional",
				Type = "EventType",
				Description = "Promotional events"
			});

			await _categoryService.CreateCategoryAsync(new Category
			{
				Name = "AGM",
				Type = "EventType",
				Description = "Annual General Meeting"
			});
		}
		catch (ValidationException)
		{
			throw;
		}
		catch (Exception ex)
		{
			throw new DataAccessException("Failed to initialize application settings and categories.", ex);
		}
	}
}
