using StageFright.Core;
using StageFright.Core.Exceptions;

namespace StageFright.Core.Tests;

/// <summary>
/// BlockingRunner waits for async startup work from a thread whose synchronization context never runs queued
/// work, as a UI thread blocked in the wait would be, and still finishes.
/// </summary>
public class BlockingRunnerTests
{
    // The UI thread is blocked in the wait, so anything posted back to it never runs.
    private sealed class BlockedUiContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state) { }
    }

    private static async Task<int> WorkAsync()
    {
        await Task.Delay(20);
        return 42;
    }

    private static bool FinishesOnBlockedUiThread(Action wait, TimeSpan timeout)
    {
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new BlockedUiContext());
            wait();
        }) { IsBackground = true };
        thread.Start();
        return thread.Join(timeout);
    }

    [Fact]
    public void Should_NeverFinish_When_AwaitedWorkIsWaitedOnDirectlyFromBlockedUiThread()
    {
        // The control case: the pattern startup used before, which deadlocks here.
        Assert.False(FinishesOnBlockedUiThread(() => WorkAsync().GetAwaiter().GetResult(), TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Should_ReturnResult_When_CalledFromBlockedUiThread()
    {
        var result = 0;

        Assert.True(FinishesOnBlockedUiThread(() => result = BlockingRunner.Run(WorkAsync), TimeSpan.FromSeconds(10)));
        Assert.Equal(42, result);
    }

    [Fact]
    public void Should_Finish_When_WorkWithoutResultCalledFromBlockedUiThread()
    {
        var done = false;

        Assert.True(FinishesOnBlockedUiThread(() => BlockingRunner.Run(async () =>
        {
            await Task.Delay(20);
            done = true;
        }), TimeSpan.FromSeconds(10)));
        Assert.True(done);
    }

    [Fact]
    public void Should_RethrowOriginalException_When_WorkFails()
    {
        var ex = Assert.Throws<DataAccessException>(() => BlockingRunner.Run(async () =>
        {
            await Task.Yield();
            throw new DataAccessException("disk unavailable", "Settings", "GetAsync");
        }));

        Assert.Equal("disk unavailable", ex.Message);
    }
}
