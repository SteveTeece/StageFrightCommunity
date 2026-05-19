using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using StageFright.Maui;

namespace StageFright.Maui;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();

		// Load configuration from appsettings.json
		var configBuilder = new ConfigurationBuilder()
			.SetBasePath(AppContext.BaseDirectory)
			.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

		var config = configBuilder.Build();

		// Configure Serilog
		Log.Logger = new LoggerConfiguration()
			.MinimumLevel.Information()
			.WriteTo.Console()
			.WriteTo.File(
				Path.Combine(AppContext.BaseDirectory, "logs", "stagefright-.txt"),
				rollingInterval: RollingInterval.Day,
				retainedFileCountLimit: 30,
				outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
			.CreateLogger();

		try
		{
			builder
				.UseMauiApp<App>()
				.ConfigureFonts(fonts =>
				{
					fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
					fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
				});

			// Register configuration
			builder.Services.AddSingleton<IConfiguration>(config);

			// Register logging
			builder.Services.AddLogging(loggingBuilder =>
			{
				loggingBuilder.ClearProviders();
				loggingBuilder.AddSerilog();
#if DEBUG
				loggingBuilder.AddDebug();
#endif
			});

			// Register other services here as they are created
			// (will be populated in future tasks)

#if DEBUG
			builder.Logging.AddDebug();
#endif

			return builder.Build();
		}
		catch (Exception ex)
		{
			Log.Fatal(ex, "Application start-up failed");
			throw;
		}
	}
}
