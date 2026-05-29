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
		InitializeComponent();
	}
}
