namespace StageFright.Core.Modules.AuditTrail;

/// <summary>
/// Ambient scope that suppresses audit trail writes for the duration of a bulk operation
/// (currently only the debug data seeder — see DebugDataSeeder.SeedAsync). While a scope
/// is active on the current async flow, AuditTrailService.LogAsync no-ops instead of
/// writing to the database. Flows across await via AsyncLocal, so callers several layers
/// deep don't need a suppression parameter threaded through their signatures.
/// </summary>
public static class AuditTrailSuppressionScope
{
    private static readonly AsyncLocal<bool> _suppressed = new();

    /// <summary>True while a suppression scope is active on the current async flow.</summary>
    public static bool IsSuppressed => _suppressed.Value;

    /// <summary>
    /// Begins suppressing audit trail writes until the returned handle is disposed.
    /// Always dispose via <c>using</c> so suppression reliably lifts even if the wrapped
    /// work throws.
    /// </summary>
    public static IDisposable Begin()
    {
        _suppressed.Value = true;
        return new SuppressionHandle();
    }

    private sealed class SuppressionHandle : IDisposable
    {
        public void Dispose() => _suppressed.Value = false;
    }
}
