using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using StageFright.Core.Contracts;
using StageFright.Core.Modules.AuditTrail;
using StageFright.Core.Modules.Finance;
using StageFright.Data;
using StageFright.Data.PluginData;
using StageFright.Data.Repositories;
using StageFright.Plugins.Contracts;

namespace StageFright.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var appDataDir = FileSystem.AppDataDirectory;
        var dbPath = Path.Combine(appDataDir, "stagefright.db");
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

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        builder.Logging.AddSerilog(dispose: true);

        ConfigureOpenTelemetry(builder.Services);

        var connectionString = $"Data Source={dbPath}";
        builder.Services.AddDbContext<StageFrightDbContext>(opts =>
            opts.UseSqlite(connectionString), ServiceLifetime.Scoped);

        RegisterRepositories(builder.Services);
        RegisterCoreServices(builder.Services);

        builder.Services.AddScoped<PluginMigrationRunner>(sp =>
            new PluginMigrationRunner(
                connectionString,
                sp.GetServices<IDataAccessProvider>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PluginMigrationRunner>>()));

        // Run startup sequence after the app is built
        var app = builder.Build();

        RunStartupSequence(app.Services, dbPath, pluginsPath, connectionString);

        return app;
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
        services.AddScoped<ICommitteeMembershipRepository, CommitteeMembershipRepository>();
        services.AddScoped<IRehearsalRepository, RehearsalRepository>();
        services.AddScoped<IAttendanceRepository, AttendanceRepository>();
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IEventTypeRepository, EventTypeRepository>();
        services.AddScoped<IParticipationRepository, ParticipationRepository>();
        services.AddScoped<IFeeRepository, FeeRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IGLRepository, GLRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<ISettingsRepository, SettingsRepository>();
        services.AddScoped<IAuditTrailRepository, AuditTrailRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
    }

    private static void RegisterCoreServices(IServiceCollection services)
    {
        services.AddScoped<IAuditTrailService, AuditTrailService>();
        services.AddScoped<GLAccountAssignmentService>();
    }

    private static void RunStartupSequence(IServiceProvider services, string dbPath, string pluginsPath, string connectionString)
    {
        // Auto-create Plugins directory (FR-021)
        try
        {
            Directory.CreateDirectory(pluginsPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Error(ex, "Failed to create Plugins directory at {Path}; plugin discovery skipped", pluginsPath);
        }

        // Discover and register plugins
        using var loggerFactory = LoggerFactory.Create(b => b.AddSerilog());
        var logger = loggerFactory.CreateLogger("PluginLoader");
        PluginLoader.DiscoverAndRegister(services as IServiceCollection ?? new ServiceCollection(), pluginsPath, logger);

        // Run core EF Core migration + startup tasks
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StageFrightDbContext>();

        try
        {
            db.Database.Migrate();
            Log.Information("Database migration completed. DB path: {DbPath}", dbPath);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Database migration failed; application cannot start");
            throw;
        }

        // Run plugin migrations
        var migrationRunner = scope.ServiceProvider.GetRequiredService<PluginMigrationRunner>();
        migrationRunner.RunAsync().GetAwaiter().GetResult();

        // Audit trail startup purge (FR-022)
        try
        {
            var auditService = scope.ServiceProvider.GetService<AuditTrailService>();
            auditService?.PurgeOlderThanAsync(DateTime.UtcNow.AddMonths(-12)).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Audit trail purge failed during startup; startup continues");
        }
    }
}
