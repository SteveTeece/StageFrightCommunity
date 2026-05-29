using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StageFright.Core.Services;
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
		// Initialize database and run migrations before showing UI
		var services = Application.Current?.Handler?.MauiContext?.Services;
		if (services != null)
		{
			try
			{
				var initService = services.GetRequiredService<IAppInitializationService>();
				using (var scope = services.CreateScope())
				{
					var initializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
					var logger = scope.ServiceProvider.GetRequiredService<ILogger<App>>();

					logger.LogInformation("Starting application initialization...");

					// Run database initialization synchronously to block UI until complete
					initializer.InitializeDatabaseAsync().Wait();

					logger.LogInformation("Database initialization completed successfully");
					initService.MarkInitializationComplete();
				}
			}
			catch (Exception ex)
			{
				var logger = services.GetRequiredService<ILogger<App>>();
				logger.LogError(ex, "Application initialization failed");

				var initService = services.GetRequiredService<IAppInitializationService>();
				initService.MarkInitializationFailed(ex.Message);

				// Critical initialization failure - we need to propagate this so the app knows
				// The Blazor app will catch this and display an error
			}
		}

		return new Window(new AppShell());
	}
}