using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using StageFright.App.Seeding;
using StageFright.Core.Contracts;
using StageFright.Core.Modules.Agm;
using StageFright.Core.Modules.AuditTrail;
using StageFright.Core.Modules.Dashboard;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Modules.Events;
using StageFright.Core.Modules.Members;
using StageFright.Core.Modules.Rehearsals;
using StageFright.Core.Modules.Settings;
using StageFright.Data;
using StageFright.UI.Modules.Events;
using StageFright.UI.Modules.Finance;
using StageFright.UI.Modules.Members;
using StageFright.UI.Modules.Rehearsals;
using StageFright.UI.Pages.Settings;
using StageFright.Data.PluginData;
using StageFright.Data.Repositories;
using StageFright.Plugins.Contracts;
using StageFright.Reports.Providers;
using StageFright.Reports.Registry;
using StageFright.Reports.Rendering;
using StageFright.UI.Modules.Reports;

namespace StageFright.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        var testDataDir = Path.Combine(repoRoot, "TestData");
        Directory.CreateDirectory(testDataDir);

        var appDataDir = FileSystem.AppDataDirectory;
        var dbPath = Path.Combine(testDataDir, "stagefright.db");
        var logPath = Path.Combine(appDataDir, "logs", "stagefright-.log");
        var pluginsPath = Path.Combine(appDataDir, "Plugins");

        ConfigureSerilog(logPath);

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddBlazorBootstrap();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
        builder.Services.AddScoped<IDebugDataSeeder, DebugDataSeeder>();
#endif

        builder.Logging.AddSerilog(dispose: true);

        ConfigureOpenTelemetry(builder.Services);

        var connectionString = $"Data Source={dbPath}";
        builder.Services.AddDbContext<StageFrightDbContext>(opts =>
            opts.UseSqlite(connectionString), ServiceLifetime.Scoped);

        // Singleton diagnostic service must be registered before RunStartupSequence reads it
        var diagnosticService = new StartupDiagnosticService();
        builder.Services.AddSingleton<IStartupDiagnosticService>(diagnosticService);

        RegisterRepositories(builder.Services);
        RegisterCoreServices(builder.Services);

        builder.Services.AddScoped<PluginMigrationRunner>(sp =>
            new PluginMigrationRunner(
                connectionString,
                sp.GetServices<IDataAccessProvider>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PluginMigrationRunner>>()));

        // Plugin discovery must register into the IServiceCollection before the container
        // is built — the built ServiceProvider does not implement IServiceCollection, so
        // registrations attempted afterward are never resolvable (issue #273).
        DiscoverAndRegisterPlugins(builder.Services, pluginsPath);

        // Run startup sequence after the app is built
        var app = builder.Build();

        RunStartupSequence(app.Services, dbPath, connectionString, diagnosticService);

        return app;
    }

    private static void DiscoverAndRegisterPlugins(IServiceCollection services, string pluginsPath)
    {
        // Auto-create Plugins directory (FR-021)
        try
        {
            Directory.CreateDirectory(pluginsPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Error(ex, "Failed to create Plugins directory at {Path}; plugin discovery skipped", pluginsPath);
            return;
        }

        using var loggerFactory = LoggerFactory.Create(b => b.AddSerilog());
        var logger = loggerFactory.CreateLogger("PluginLoader");
        PluginLoader.DiscoverAndRegister(services, pluginsPath, logger);
    }

    private static string FindRepoRoot(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir != null)
        {
            if (dir.GetFiles("*.slnx").Length > 0 || dir.GetDirectories(".git").Length > 0)
                return dir.FullName;
            dir = dir.Parent;
        }
        // Fallback: use the executable's directory itself
        return startDir;
    }

    private static void ConfigureSerilog(string logPath)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
            .CreateLogger();
    }

    private static void ConfigureOpenTelemetry(IServiceCollection services)
    {
        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .AddSource("StageFright.*")
                .AddConsoleExporter())
            .WithMetrics(metrics => metrics
                .AddRuntimeInstrumentation()
                .AddConsoleExporter());
    }

    private static void RegisterRepositories(IServiceCollection services)
    {
        services.AddScoped<IMemberRepository, MemberRepository>();
        services.AddScoped<ICommitteePositionRecordRepository, CommitteePositionRecordRepository>();
        services.AddScoped<IAgmRepository, AgmRepository>();
        services.AddScoped<IAgmAttendanceRepository, AgmAttendanceRepository>();
        services.AddScoped<ICommitteeOfficeHolderTypeRepository, CommitteeOfficeHolderTypeRepository>();
        services.AddScoped<ICommitteeTermRepository, CommitteeTermRepository>();
        services.AddScoped<IRehearsalRepository, RehearsalRepository>();
        services.AddScoped<IAttendanceRepository, AttendanceRepository>();
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IEventTypeRepository, EventTypeRepository>();
        services.AddScoped<IParticipationRepository, ParticipationRepository>();
        services.AddScoped<IFeeRepository, FeeRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IGLRepository, GLRepository>();
        services.AddScoped<IJournalEntryRepository, JournalEntryRepository>();
        services.AddScoped<IBankReconciliationRepository, BankReconciliationRepository>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<ISettingsRepository, SettingsRepository>();
        services.AddScoped<IAuditTrailRepository, AuditTrailRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
    }

    private static void RegisterCoreServices(IServiceCollection services)
    {
        services.AddScoped<IAuditTrailService, AuditTrailService>();
        services.AddScoped<ISetupService, SetupService>();
        services.AddSingleton<IDeviceThemePreferenceProvider, MauiDeviceThemePreferenceProvider>();
        services.AddScoped<AccountNumberAssignmentService>();

        // Settings service
        services.AddScoped<ISettingsService, SettingsService>();

        // Members module (Phase 4 + 15)
        services.AddScoped<AgeCalculationService>();
        services.AddScoped<MemberValidationService>();
        services.AddScoped<IMemberService, MemberService>();
        services.AddScoped<ICommitteeService, CommitteeService>();
        services.AddScoped<ICommitteeOfficeHolderTypeService, CommitteeOfficeHolderTypeService>();
        services.AddScoped<IAgmService, AgmService>();
        services.AddScoped<IAgmAttendanceSheetService, AgmAttendanceSheetService>();
        services.AddScoped<IAgmAttendanceSheetPdfRenderer, AgmAttendanceSheetPdfRenderer>();
        services.AddScoped<IAgmResultsPdfRenderer, AgmResultsPdfRenderer>();

        // Rehearsals module (Phase 5)
        services.AddScoped<IRehearsalService, RehearsalService>();
        services.AddScoped<IAttendanceService, AttendanceService>();
        services.AddScoped<IAttendanceRollService, AttendanceRollService>();
        services.AddScoped<IAttendanceRollPdfRenderer, AttendanceRollPdfRenderer>();

        // Events module (Phase 12)
        services.AddScoped<IEventTypeService, EventTypeService>();
        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IEventAttendanceSheetService, EventAttendanceSheetService>();
        services.AddScoped<IEventAttendanceSheetPdfRenderer, EventAttendanceSheetPdfRenderer>();
        services.AddScoped<ICombinedEventListService, CombinedEventListService>();

        // Finance module (Phase 6 + 9)
        services.AddScoped<IFeeService, FeeService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IReactivationForgivenessService, ReactivationForgivenessService>();
        services.AddScoped<IMemberBalanceService, MemberBalanceService>();
        services.AddScoped<IFinanceSummaryService, FinanceSummaryService>();
        services.AddScoped<IIncomeEntryService, IncomeEntryService>();

        // Finance expansion — posting engine + money-out workflows (spec 002 US2)
        services.AddScoped<IExpensePaymentService, ExpensePaymentService>();
        services.AddScoped<IBankDepositService, BankDepositService>();
        services.AddScoped<IGeneralJournalService, GeneralJournalService>();
        services.AddScoped<IOpeningBalanceService, OpeningBalanceService>();

        // Finance expansion — bank reconciliation (spec 002 US3)
        services.AddScoped<IBankReconciliationService, BankReconciliationService>();

        // Account management module (Phase 7)
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IAccountBalanceService, AccountBalanceService>();

        // Dashboard service (Phase 8)
        services.AddScoped<IDashboardService, DashboardService>();

        // Menu and dashboard providers
        services.AddSingleton<IMenuItemProvider, DashboardMenuItemProvider>();
        services.AddSingleton<IMenuItemProvider, MemberMenuItemProvider>();
        services.AddSingleton<IMenuItemProvider, RehearsalMenuItemProvider>();
        services.AddSingleton<IMenuItemProvider, EventsMenuItemProvider>();
        services.AddSingleton<IMenuItemProvider, FinanceMenuItemProvider>();
        services.AddScoped<IDashboardTileProvider, MembersDashboardTileProvider>();
        services.AddScoped<IDashboardTileProvider, RehearsalsDashboardTileProvider>();
        services.AddScoped<IDashboardTileProvider, EventsDashboardTileProvider>();
        services.AddScoped<IDashboardTileProvider, FinanceDashboardTileProvider>();
        services.AddScoped<IDashboardTileProvider, CashFlowDashboardTileProvider>();
        services.AddScoped<IDashboardTileProvider, OutstandingBalancesDashboardTileProvider>();
        services.AddScoped<IDashboardTileProvider, AttendanceTrendDashboardTileProvider>();

        // Backup service (Phase 13)
        services.AddScoped<IBackupRepository, BackupRepository>();
        services.AddScoped<IBackupService, BackupService>();

        // Reports module (Phase 10 + 11)
        services.AddScoped<IReportProvider, IncomeStatementReportProvider>();
        services.AddScoped<IReportProvider, ChartOfAccountsReportProvider>();
        services.AddScoped<IReportProvider, TrialBalanceReportProvider>();
        services.AddScoped<IReportProvider, AccountRegisterReportProvider>();
        services.AddScoped<IReportProvider, MemberAccountSummaryReportProvider>();
        services.AddScoped<IReportProvider, MemberListReportProvider>();
        services.AddScoped<IReportProvider, CommitteeReportProvider>();
        services.AddScoped<IReportProvider, BankReconciliationReportProvider>();
        services.AddScoped<IReportProvider, TaxSummaryReportProvider>();
        services.AddScoped<IReportProvider, BalanceSheetReportProvider>();
        services.AddScoped<IReportProvider, GeneralLedgerReportProvider>();
        services.AddScoped<IReportProviderRegistry, ReportProviderRegistry>();
        services.AddScoped<IPdfReportRenderer, PdfReportRenderer>();
        services.AddScoped<ICsvReportExporter, CsvReportExporter>();
        services.AddScoped<IMenuItemProvider, ReportMenuItemProvider>();
        services.AddSingleton<IMenuItemProvider, SettingsMenuItemProvider>();
    }

    private static void RunStartupSequence(IServiceProvider services, string dbPath, string connectionString, StartupDiagnosticService diagnosticService)
    {
        // Run core EF Core migration + startup tasks
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StageFrightDbContext>();

        try
        {
            db.Database.Migrate();
            Log.Information("Database migration completed. DB path: {DbPath}", dbPath);
        }
        catch (Exception ex) when (ex is DbException or DbUpdateException)
        {
            // Database file is corrupted or inaccessible — record the error so the UI can
            // present recovery options (open file location / create new database) via the
            // StartupError page instead of crashing (FR-T172).
            Log.Error(ex, "Database migration failed due to database error; showing recovery UI. DB path: {DbPath}", dbPath);
            diagnosticService.RecordError(ex, dbPath);
            return;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Database migration failed; application cannot start");
            throw;
        }

        // Run plugin migrations
        var migrationRunner = scope.ServiceProvider.GetRequiredService<PluginMigrationRunner>();
        migrationRunner.RunAsync().GetAwaiter().GetResult();

        // Audit trail startup purge (FR-022): failure is tolerated, startup continues.
        // Resolved via the registered interface — resolving the concrete AuditTrailService
        // type here previously returned null (it was never registered by concrete type),
        // silently skipping the purge while still logging a false "complete" message.
        try
        {
            var auditService = scope.ServiceProvider.GetRequiredService<IAuditTrailService>();
            var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
            var settings = settingsService.GetAsync().GetAwaiter().GetResult();
            var retentionYears = settings?.AuditRetentionYears ?? 1;

            auditService.PurgeOlderThanAsync(DateTime.UtcNow.AddYears(-retentionYears)).GetAwaiter().GetResult();
            Log.Information("Audit trail startup purge complete (retention: {RetentionYears} year(s))", retentionYears);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Audit trail purge failed during startup; startup continues");
        }
    }
}
