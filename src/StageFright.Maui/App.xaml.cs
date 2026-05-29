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
#if DEBUG
		System.Diagnostics.Debug.WriteLine("[STARTUP] App.CreateWindow() called");
#endif
		try
		{
			// Store the service provider for later use
			_serviceProvider = Handler?.MauiContext?.Services;
#if DEBUG
			System.Diagnostics.Debug.WriteLine($"[STARTUP] Service provider stored: {(_serviceProvider != null ? "Success" : "Failed")}");
#endif
			
#if DEBUG
			System.Diagnostics.Debug.WriteLine("[STARTUP] Creating AppShell instance");
#endif
			var window = new Window(new AppShell());
#if DEBUG
			System.Diagnostics.Debug.WriteLine("[STARTUP] AppShell instance created, returning Window");
#endif
			return window;
		}
		catch (Exception ex)
		{
#if DEBUG
			System.Diagnostics.Debug.WriteLine($"[STARTUP] EXCEPTION in CreateWindow: {ex.GetType().Name}: {ex.Message}");
			if (ex.InnerException != null)
				System.Diagnostics.Debug.WriteLine($"[STARTUP] InnerException: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
			System.Diagnostics.Debug.WriteLine($"[STARTUP] Stack Trace: {ex.StackTrace}");
#endif
			throw;
		}
	}

	protected override void OnStart()
	{
#if DEBUG
		System.Diagnostics.Debug.WriteLine("[STARTUP] App.OnStart() called");
#endif
		base.OnStart();
		// Initialize database after the app has fully started and services are available
		InitializeDatabaseAsync().Wait();
#if DEBUG
		System.Diagnostics.Debug.WriteLine("[STARTUP] App.OnStart() completed");
#endif
	}

	private async Task InitializeDatabaseAsync()
	{
#if DEBUG
		System.Diagnostics.Debug.WriteLine("[STARTUP] InitializeDatabaseAsync() started");
#endif
		if (_serviceProvider == null)
		{
			// Get service provider from the current application
#if DEBUG
			System.Diagnostics.Debug.WriteLine("[STARTUP] Service provider is null, attempting to retrieve from Application.Current");
#endif
			_serviceProvider = Application.Current?.Handler?.MauiContext?.Services;
		}

		if (_serviceProvider == null)
		{
#if DEBUG
			System.Diagnostics.Debug.WriteLine("[STARTUP] CRITICAL: Service provider still null after retrieval attempt");
#endif
			return; // Services not available yet
		}

#if DEBUG
		System.Diagnostics.Debug.WriteLine("[STARTUP] Service provider available, proceeding with initialization");
#endif

		try
		{
#if DEBUG
			System.Diagnostics.Debug.WriteLine("[STARTUP] Creating service scope");
#endif
			using (var scope = _serviceProvider.CreateScope())
			{
				var initializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
				var logger = scope.ServiceProvider.GetRequiredService<ILogger<App>>();
				var initService = _serviceProvider.GetRequiredService<IAppInitializationService>();

#if DEBUG
				System.Diagnostics.Debug.WriteLine("[STARTUP] Dependencies resolved: IDatabaseInitializer, ILogger, IAppInitializationService");
#endif

				logger.LogInformation("[STARTUP] Starting application initialization...");

				// Run database initialization synchronously to block UI until complete
#if DEBUG
				System.Diagnostics.Debug.WriteLine("[STARTUP] Calling IDatabaseInitializer.InitializeDatabaseAsync()");
#endif
				await initializer.InitializeDatabaseAsync();

#if DEBUG
				System.Diagnostics.Debug.WriteLine("[STARTUP] IDatabaseInitializer.InitializeDatabaseAsync() completed successfully");
#endif

				logger.LogInformation("[STARTUP] Database initialization completed successfully");
				initService.MarkInitializationComplete();

#if DEBUG
				System.Diagnostics.Debug.WriteLine("[STARTUP] IAppInitializationService.MarkInitializationComplete() called");
#endif
			}
		}
		catch (Exception ex)
		{
#if DEBUG
			System.Diagnostics.Debug.WriteLine($"[STARTUP] EXCEPTION in InitializeDatabaseAsync: {ex.GetType().Name}: {ex.Message}");
#endif
			try
			{
				var logger = _serviceProvider.GetRequiredService<ILogger<App>>();
				var initService = _serviceProvider.GetRequiredService<IAppInitializationService>();
				logger.LogError(ex, "[STARTUP] Application initialization failed");
				initService.MarkInitializationFailed(ex.Message);

#if DEBUG
				System.Diagnostics.Debug.WriteLine("[STARTUP] Error state marked in IAppInitializationService");
#endif
			}
			catch
			{
				// If logging fails, we can't do much more
#if DEBUG
				System.Diagnostics.Debug.WriteLine("[STARTUP] CRITICAL: Failed to log error or mark initialization as failed");
#endif
			}
		}

#if DEBUG
		System.Diagnostics.Debug.WriteLine("[STARTUP] InitializeDatabaseAsync() exited");
#endif
	}
}