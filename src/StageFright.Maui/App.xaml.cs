using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StageFright.Core.Services;
using StageFright.Maui.Services;

namespace StageFright.Maui;

public partial class App : Application
{
	private IServiceProvider? _serviceProvider;

	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		// Store the service provider for later use
		_serviceProvider = Handler?.MauiContext?.Services;
		
		return new Window(new AppShell());
	}

	protected override void OnStart()
	{
		base.OnStart();
		// Initialize database after the app has fully started and services are available
		InitializeDatabaseAsync().Wait();
	}

	private async Task InitializeDatabaseAsync()
	{
		if (_serviceProvider == null)
		{
			// Get service provider from the current application
			_serviceProvider = Application.Current?.Handler?.MauiContext?.Services;
		}

		if (_serviceProvider == null)
		{
			return; // Services not available yet
		}

		try
		{
			using (var scope = _serviceProvider.CreateScope())
			{
				var initializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
				var logger = scope.ServiceProvider.GetRequiredService<ILogger<App>>();
				var initService = _serviceProvider.GetRequiredService<IAppInitializationService>();

				logger.LogInformation("Starting application initialization...");

				// Run database initialization synchronously to block UI until complete
				await initializer.InitializeDatabaseAsync();

				logger.LogInformation("Database initialization completed successfully");
				initService.MarkInitializationComplete();
			}
		}
		catch (Exception ex)
		{
			try
			{
				var logger = _serviceProvider.GetRequiredService<ILogger<App>>();
				var initService = _serviceProvider.GetRequiredService<IAppInitializationService>();
				logger.LogError(ex, "Application initialization failed");
				initService.MarkInitializationFailed(ex.Message);
			}
			catch
			{
				// If logging fails, we can't do much more
			}
		}
	}
}