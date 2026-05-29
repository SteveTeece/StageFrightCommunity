namespace StageFright.Maui;

/// <summary>
/// AppShell serves as the MAUI application root/container.
/// Shell routing is NOT used - all routing and navigation is handled by Blazor.
/// This shell contains only MainPage which hosts BlazorWebViewHost.
/// </summary>
public partial class AppShell : Shell
{
	public AppShell()
	{
#if DEBUG
		System.Diagnostics.Debug.WriteLine("[STARTUP-XAML] AppShell.ctor() called");
#endif
		try
		{
			InitializeComponent();
#if DEBUG
			System.Diagnostics.Debug.WriteLine("[STARTUP-XAML] AppShell.InitializeComponent() completed");
#endif
		}
		catch (Exception ex)
		{
#if DEBUG
			System.Diagnostics.Debug.WriteLine($"[STARTUP-XAML] AppShell.ctor() EXCEPTION: {ex.GetType().Name}: {ex.Message}");
			if (ex.InnerException != null)
				System.Diagnostics.Debug.WriteLine($"[STARTUP-XAML] InnerException: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
#endif
			throw;
		}
	}
}
