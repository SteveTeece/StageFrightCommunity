using StageFright.Core.Modules.AuditTrail;

namespace StageFright.Core.Tests.Modules.AuditTrail;

/// <summary>
/// Unit tests for AuditTrailSuppressionScope: the ambient AsyncLocal scope the debug data
/// seeder uses to suppress AuditTrailService.LogAsync writes for its whole run (#296).
/// Every test disposes its own scope so state never leaks to a sibling test.
/// </summary>
public class AuditTrailSuppressionScopeTests
{
    [Fact]
    public void IsSuppressed_IsFalse_WhenNoScopeActive()
    {
        Assert.False(AuditTrailSuppressionScope.IsSuppressed);
    }

    [Fact]
    public void Begin_SetsIsSuppressedTrue_UntilDisposed()
    {
        using var scope = AuditTrailSuppressionScope.Begin();

        Assert.True(AuditTrailSuppressionScope.IsSuppressed);
    }

    [Fact]
    public void Dispose_RestoresIsSuppressedFalse()
    {
        var scope = AuditTrailSuppressionScope.Begin();

        scope.Dispose();

        Assert.False(AuditTrailSuppressionScope.IsSuppressed);
    }

    [Fact]
    public void Dispose_RestoresIsSuppressedFalse_WhenExceptionThrownInsideScope()
    {
        // Explicitly typed as Action — a throw-only lambda body is otherwise ambiguous
        // between Action and the obsolete-for-sync-use Record.Exception(Func<Task>) overload.
        Action act = () =>
        {
            using var scope = AuditTrailSuppressionScope.Begin();
            throw new InvalidOperationException("simulated seeding failure");
        };

        var thrown = Record.Exception(act);

        Assert.IsType<InvalidOperationException>(thrown);
        Assert.False(AuditTrailSuppressionScope.IsSuppressed);
    }

    [Fact]
    public async Task IsSuppressed_FlowsAcrossAwait()
    {
        using var scope = AuditTrailSuppressionScope.Begin();

        await Task.Yield();

        Assert.True(AuditTrailSuppressionScope.IsSuppressed);
    }
}
