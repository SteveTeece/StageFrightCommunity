namespace StageFright.Core;

/// <summary>
/// Waits for async work from synchronous startup code (<c>MauiProgram</c>), which must finish before the first
/// render. The work runs on a thread-pool thread, so an <c>await</c> inside it resumes on the pool instead of
/// queueing back to the caller's synchronization context, which is the UI thread blocked in this wait and would
/// never run it (a deadlock).
/// </summary>
public static class BlockingRunner
{
    public static T Run<T>(Func<Task<T>> work) => Task.Run(work).GetAwaiter().GetResult();

    public static void Run(Func<Task> work) => Task.Run(work).GetAwaiter().GetResult();
}
