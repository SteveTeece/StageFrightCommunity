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

    /// <summary>Clears any recorded startup error (called after database recreation).</summary>
    void ClearError();
}
