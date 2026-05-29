using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Serilog;
using StageFright.Maui;
using StageFright.Maui.Services;
using StageFright.Data.Context;
using StageFright.Data.Repositories;
using StageFright.Data.Services;
using StageFright.Core.Services;
using StageFright.Plugins.Discovery;
using StageFright.Reports.Services;
using StageFright.Reports.Exporters;

namespace StageFright.Maui;

public static class MauiProgram
{
	private static IServiceProvider? _serviceProvider;

	public static MauiApp CreateMauiApp()
	{
#if DEBUG
		Debug.WriteLine("[STARTUP] MauiProgram.CreateMauiApp() started");
#endif
		var builder = MauiApp.CreateBuilder();

		// Load configuration from appsettings.json
#if DEBUG
		Debug.WriteLine("[STARTUP] Loading configuration from appsettings.json");
#endif
		var configBuilder = new ConfigurationBuilder()
			.SetBasePath(AppContext.BaseDirectory)
			.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

		var config = configBuilder.Build();
#if DEBUG
		Debug.WriteLine("[STARTUP] Configuration loaded successfully");
#endif

		// Ensure TestData directory exists in repo root
#if DEBUG
		Debug.WriteLine("[STARTUP] Finding repository root");
#endif
		var repoRoot = FindRepositoryRoot();
#if DEBUG
		Debug.WriteLine($"[STARTUP] Repository root: {repoRoot}");
#endif
		var testDataDir = Path.Combine(repoRoot, "TestData");
		if (!Directory.Exists(testDataDir))
			Directory.CreateDirectory(testDataDir);
#if DEBUG
		Debug.WriteLine($"[STARTUP] TestData directory: {testDataDir}");
#endif

		// Configure Serilog to write to TestData folder, one entry per line
		var minimumLevel =
#if DEBUG
			Serilog.Events.LogEventLevel.Debug;
#else
			Serilog.Events.LogEventLevel.Information;
#endif

		Log.Logger = new LoggerConfiguration()
			.MinimumLevel.Is(minimumLevel)
			.WriteTo.Console()
			.WriteTo.File(
				Path.Combine(testDataDir, "stagefright.log"),
				rollingInterval: RollingInterval.Day,
				retainedFileCountLimit: 30,
				outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
			.CreateLogger();

#if DEBUG
		Log.Information("[STARTUP] Debug logging enabled for startup sequence");
#endif

		try
		{
#if DEBUG
			Debug.WriteLine("[STARTUP] Configuring MAUI app");
#endif
			builder
				.UseMauiApp<App>()
				.ConfigureFonts(fonts =>
				{
					fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
					fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
				});
#if DEBUG
			Debug.WriteLine("[STARTUP] Fonts configured");
#endif

			// Register Blazor Web View
#if DEBUG
			Debug.WriteLine("[STARTUP] Registering Blazor Web View with developer tools");
#endif
			builder
				.Services
				.AddBlazorWebView()
#if DEBUG
				.AddBlazorWebViewDeveloperTools()
#endif
			;

			// Register configuration
#if DEBUG
			Debug.WriteLine("[STARTUP] Registering configuration service");
#endif
			builder.Services.AddSingleton<IConfiguration>(config);

			// Register logging
#if DEBUG
			Debug.WriteLine("[STARTUP] Configuring logging providers");
#endif
			builder.Services.AddLogging(loggingBuilder =>
			{
				loggingBuilder.ClearProviders();
				loggingBuilder.AddSerilog();
#if DEBUG
				loggingBuilder.AddDebug();
#endif
			});

			// Register database context
			// Use the TestData directory that was already created for logging
#if DEBUG
			Debug.WriteLine("[STARTUP] Configuring database context");
#endif
			var dbPath = Path.Combine(testDataDir, "stagefright.db");
			var connectionString = $"Data Source={dbPath}";
#if DEBUG
			Debug.WriteLine($"[STARTUP] Database path: {dbPath}");
#endif
			
			builder.Services.AddDbContext<StageFrightContext>(options =>
				options.UseSqlite(connectionString, sqlOptions => 
					sqlOptions.MigrationsAssembly("StageFright.Data")));
#if DEBUG
			Debug.WriteLine("[STARTUP] Database context configured");
#endif

			// Register app initialization service
#if DEBUG
			Debug.WriteLine("[STARTUP] Registering app initialization service");
#endif
			builder.Services.AddSingleton<IAppInitializationService, AppInitializationService>();

			// Register database initialization and seeding services
#if DEBUG
			Debug.WriteLine("[STARTUP] Registering database services");
#endif
			builder.Services.AddTransient<IDatabaseSeeder, DatabaseSeeder>();
			builder.Services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();

			// Register repositories (scoped lifetime for data access)
			builder.Services.AddScoped<IMemberRepository, MemberRepository>();
			builder.Services.AddScoped<IRehearsalRepository, RehearsalRepository>();
			builder.Services.AddScoped<IEventRepository, EventRepository>();
			builder.Services.AddScoped<IAttendanceRepository, AttendanceRepository>();
			builder.Services.AddScoped<IParticipationRepository, ParticipationRepository>();
			builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
			builder.Services.AddScoped<IFeeRepository, FeeRepository>();
			builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
			builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
			builder.Services.AddScoped<ICommitteeMembershipRepository, CommitteeMembershipRepository>();
			builder.Services.AddScoped<ISettingsRepository, SettingsRepository>();
			builder.Services.AddScoped<IAuditTrailRepository, AuditTrailRepository>();

			// Register business logic services
			builder.Services.AddScoped<AgeCalculationService>();
			builder.Services.AddScoped<MemberValidationService>();
			builder.Services.AddScoped<GLAccountAssignmentService>();

			// Register Phase 2a financial services
			builder.Services.AddScoped<GlTransactionService>();
			builder.Services.AddScoped<GlBalanceValidationService>();
			builder.Services.AddScoped<PaymentAllocationService>();
			builder.Services.AddScoped<MemberBalanceService>();

			// Register application services
			builder.Services.AddScoped<IMemberService, MemberService>();
			builder.Services.AddScoped<IRehearsalService, RehearsalService>();
			builder.Services.AddScoped<IEventService, EventService>();
			builder.Services.AddScoped<ICategoryService, CategoryService>();
			builder.Services.AddScoped<ICommitteeMembershipService, CommitteeMembershipService>();
			builder.Services.AddScoped<ISettingsService, SettingsService>();
			builder.Services.AddScoped<ISetupService, SetupService>();
			builder.Services.AddScoped<IFinanceService, FinanceService>();
			builder.Services.AddScoped<IAnnualFeeApplicationService, AnnualFeeApplicationService>();

			// Register navigation and directory services
			builder.Services.AddSingleton<INavigationService, NavigationService>();
			builder.Services.AddSingleton<IDirectoryService, DirectoryService>();

			// Register plugin discovery and tile providers
			builder.Services.AddPlugins();
			builder.Services.AddScoped<StageFright.Plugins.Providers.MembersDashboardTileProvider>();
			builder.Services.AddScoped<StageFright.Plugins.Providers.RehearsalsDashboardTileProvider>();
			builder.Services.AddScoped<StageFright.Plugins.Providers.EventsDashboardTileProvider>();
			builder.Services.AddScoped<StageFright.Plugins.Providers.FinanceDashboardTileProvider>();

			// Register report infrastructure services
			builder.Services.AddScoped<ReportAggregationService>();
			builder.Services.AddScoped<ReportMenuService>();
			builder.Services.AddScoped<PdfExporter>();
			builder.Services.AddScoped<CsvExporter>();

			// Register financial report providers
			builder.Services.AddScoped<StageFright.Reports.Providers.IncomeStatementReportProvider>();
			builder.Services.AddScoped<StageFright.Reports.Providers.TrialBalanceReportProvider>();
			builder.Services.AddScoped<StageFright.Reports.Providers.AccountRegisterReportProvider>();
			builder.Services.AddScoped<StageFright.Reports.Providers.MemberAccountSummaryReportProvider>();
			builder.Services.AddScoped<StageFright.Reports.Providers.MemberListReportProvider>();
			builder.Services.AddScoped<StageFright.Reports.Providers.CommitteeReportProvider>();

			// Register UI and platform services (C# only, no JS interop)
			builder.Services.AddScoped<IFileService, MauiFileService>();
			builder.Services.AddSingleton<IThemeService, ThemeService>();

#if DEBUG
			builder.Logging.AddDebug();
#endif

			var app = builder.Build();
			_serviceProvider = app.Services;
			return app;
		}
		catch (Exception ex)
		{
			Log.Fatal(ex, "Application start-up failed");
			throw;
		}
	}

	/// <summary>
	/// Gets the service provider for the MAUI application.
	/// This allows Blazor components and other parts of the app to access services.
	/// </summary>
	public static IServiceProvider? GetServiceProvider()
	{
		return _serviceProvider;
	}

	/// <summary>
	/// Locates the repository root by searching for the solution file or .git directory.
	/// </summary>
	private static string FindRepositoryRoot()
	{
		var currentDirectory = AppContext.BaseDirectory;

		while (currentDirectory != null)
		{
			// Look for .git directory or .sln file to identify repo root
			if (Directory.Exists(Path.Combine(currentDirectory, ".git")) ||
				Directory.GetFiles(currentDirectory, "*.sln").Length > 0)
			{
				return currentDirectory;
			}

			currentDirectory = Directory.GetParent(currentDirectory)?.FullName;
		}

		// Fallback to AppContext.BaseDirectory if repo root not found
		return AppContext.BaseDirectory;
	}
}
