using StageFright.Core.Services;
using StageFright.Data.Repositories;

namespace StageFright.Maui.Services;

/// <summary>
/// Implementation of IThemeService for managing application themes.
/// Persists theme preference and notifies subscribers of changes.
/// </summary>
public class ThemeService : IThemeService
{
	private readonly ISettingsRepository _settingsRepository;
	private string _currentTheme = "Dark";

	/// <summary>
	/// Gets the current theme.
	/// </summary>
	public string CurrentTheme => _currentTheme;

	/// <summary>
	/// Event raised when theme changes.
	/// </summary>
	public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

	/// <summary>
	/// Initializes a new instance of the ThemeService.
	/// </summary>
	/// <param name="settingsRepository">Repository for persisting theme settings</param>
	public ThemeService(ISettingsRepository settingsRepository)
	{
		_settingsRepository = settingsRepository;
		InitializeThemeAsync().Wait();
	}

	/// <summary>
	/// Sets the application theme.
	/// </summary>
	/// <param name="theme">"Dark" or "Light"</param>
	public async Task SetThemeAsync(string theme)
	{
		if (_currentTheme == theme)
			return;

		try
		{
			_currentTheme = theme;
			var settings = await _settingsRepository.GetSettingsAsync();
			if (settings != null)
			{
				settings.Theme = theme;
				await _settingsRepository.UpdateSettingsAsync(settings);
			}

			OnThemeChanged(new ThemeChangedEventArgs { NewTheme = theme });
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error setting theme: {ex}");
		}
	}

	/// <summary>
	/// Toggles between dark and light themes.
	/// </summary>
	public async Task ToggleThemeAsync()
	{
		var newTheme = _currentTheme == "Dark" ? "Light" : "Dark";
		await SetThemeAsync(newTheme);
	}

	/// <summary>
	/// Gets CSS class name for the current theme.
	/// </summary>
	public string GetThemeCssClass()
	{
		return _currentTheme == "Dark" ? "theme-dark" : "theme-light";
	}

	/// <summary>
	/// Initializes the theme from persisted settings.
	/// </summary>
	private async Task InitializeThemeAsync()
	{
		try
		{
			var settings = await _settingsRepository.GetSettingsAsync();
			_currentTheme = settings?.Theme ?? "Dark";
		}
		catch
		{
			_currentTheme = "Dark"; // Default to dark
		}
	}

	/// <summary>
	/// Raises the ThemeChanged event.
	/// </summary>
	protected virtual void OnThemeChanged(ThemeChangedEventArgs args)
	{
		ThemeChanged?.Invoke(this, args);
	}
}
