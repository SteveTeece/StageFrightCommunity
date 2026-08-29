namespace StageFright.Core.Contracts;

/// <summary>
/// Stores any critical errors that occur during the startup sequence before the Blazor UI is available.
/// The UI checks this on load and navigates to the startup error page if an error is present.
/// </summary>
public interface IStartupDiagnosticService
{
    /// <summary>True if a critical startup error was recorded.</summary>
    bool HasStartupError { get; }

    /// <summary>The exception recorded during startup, or null if startup succeeded.</summary>
    Exception? StartupException { get; }

    /// <summary>Path to the database file where the error occurred, used for recovery options.</summary>
    string? DatabasePath { get; }

    /// <summary>Records a critical startup error.</summary>
    void RecordError(Exception ex, string? databasePath = null);

    /// <summary>
    /// True if a non-fatal startup warning was recorded (spec 028, US8 / FR-025). A warning does
    /// not block startup or trigger the recovery page — it is surfaced to the user as a dismissible
    /// notice so a swallowed failure (e.g. a failed audit-trail purge) is never silently discarded.
    /// </summary>
    bool HasStartupWarning { get; }

    /// <summary>The message of the non-fatal startup warning, or null if none was recorded.</summary>
    string? StartupWarning { get; }

    /// <summary>Records a non-fatal startup warning. Startup continues regardless.</summary>
    void RecordWarning(string message);

    /// <summary>Clears any recorded startup error and warning (called after database recreation).</summary>
    void ClearError();
}
