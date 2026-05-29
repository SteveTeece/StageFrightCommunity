namespace StageFright.Maui;

/// <summary>
/// MainPage is a MAUI container page that hosts the BlazorWebViewHost.
/// No navigation or UI logic is implemented here.
/// All UI elements and navigation are handled by Blazor.
/// </summary>
public partial class MainPage : ContentPage
{
	public MainPage()
	{
#if DEBUG
		System.Diagnostics.Debug.WriteLine("[STARTUP-XAML] MainPage.ctor() called");
#endif
		try
		{
			InitializeComponent();
#if DEBUG
			System.Diagnostics.Debug.WriteLine("[STARTUP-XAML] MainPage.InitializeComponent() completed");
#endif
		}
		catch (Exception ex)
		{
#if DEBUG
			System.Diagnostics.Debug.WriteLine($"[STARTUP-XAML] MainPage.ctor() EXCEPTION: {ex.GetType().Name}: {ex.Message}");
			if (ex.InnerException != null)
				System.Diagnostics.Debug.WriteLine($"[STARTUP-XAML] InnerException: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
#endif
			throw;
		}
	}
}
