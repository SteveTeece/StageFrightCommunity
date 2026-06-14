using StageFright.Core.Contracts;

namespace StageFright.Core.Modules.Settings;

/// <summary>
/// Singleton that holds startup error state so the Blazor UI can surface it to the user.
/// </summary>
public class StartupDiagnosticService : IStartupDiagnosticService
{
    private Exception? _startupException;
    private string? _databasePath;

    public bool HasStartupError => _startupException is not null;

    public Exception? StartupException => _startupException;

    public string? DatabasePath => _databasePath;

    public void RecordError(Exception ex, string? databasePath = null)
    {
        _startupException = ex;
        _databasePath = databasePath;
    }

    public void ClearError()
    {
        _startupException = null;
        _databasePath = null;
    }
}
