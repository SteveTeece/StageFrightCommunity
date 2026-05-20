using Microsoft.Extensions.DependencyInjection;
using StageFright.Maui.Services;

namespace StageFright.Maui;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		// Initialize database and run migrations if needed
		try
		{
			var services = Application.Current?.Handler?.MauiContext?.Services;
			if (services != null)
			{
				// Create a scope for the database initializer
				using (var scope = services.CreateScope())
				{
					var initializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
					initializer.InitializeDatabaseAsync().Wait();
				}
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Database initialization error: {ex}");
		}

		return new Window(new AppShell());
	}
}