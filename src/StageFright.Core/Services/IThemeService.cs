namespace StageFright.Core.Services;

/// <summary>
/// Service for managing application theme switching in C# without JavaScript.
/// Provides theme state management and persistence.
/// </summary>
public interface IThemeService
{
	/// <summary>
	/// Gets the current theme.
	/// </summary>
	string CurrentTheme { get; }

	/// <summary>
	/// Sets the application theme.
	/// </summary>
	/// <param name="theme">"Dark" or "Light"</param>
	Task SetThemeAsync(string theme);

	/// <summary>
	/// Toggles between dark and light themes.
	/// </summary>
	Task ToggleThemeAsync();

	/// <summary>
	/// Gets CSS class name for the current theme.
	/// </summary>
	string GetThemeCssClass();

	/// <summary>
	/// Event raised when theme changes.
	/// </summary>
	event EventHandler<ThemeChangedEventArgs>? ThemeChanged;
}

/// <summary>
/// Event arguments for theme change events.
/// </summary>
public class ThemeChangedEventArgs : EventArgs
{
	/// <summary>
	/// The new theme ("Dark" or "Light").
	/// </summary>
	public string NewTheme { get; set; } = string.Empty;
}
