namespace StageFright.Core.Tests.Fixtures;

/// <summary>
/// Base class for StageFright.Core unit tests.
/// Provides a shared CancellationTokenSource with a 30-second timeout so tests
/// never hang silently.
/// </summary>
public abstract class TestBase : IDisposable
{
    protected readonly CancellationTokenSource Cts = new(TimeSpan.FromSeconds(30));
    protected CancellationToken Ct => Cts.Token;

    public virtual void Dispose()
    {
        Cts.Dispose();
    }
}
