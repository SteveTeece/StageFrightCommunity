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

			// Register Blazor Web View
			builder.Services.AddBlazorWebView();

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

			// Register database context
			// Ensure TestData directory exists and use absolute path
			var dbDir = Path.Combine(AppContext.BaseDirectory, "TestData");
			if (!Directory.Exists(dbDir))
				Directory.CreateDirectory(dbDir);
			
			var dbPath = Path.Combine(dbDir, "stagefright.db");
			var connectionString = $"Data Source={dbPath}";
			
			builder.Services.AddDbContext<StageFrightContext>(options =>
				options.UseSqlite(connectionString, sqlOptions => 
					sqlOptions.MigrationsAssembly("StageFright.Data")));

			// Register database initialization and seeding services
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

			// Register UI and platform services (C# only, no JS interop)
			builder.Services.AddScoped<IFileService, MauiFileService>();
			builder.Services.AddSingleton<IThemeService, ThemeService>();

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
