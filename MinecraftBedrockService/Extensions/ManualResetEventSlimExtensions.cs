namespace System.Threading;

public static class ManualResetEventSlimExtensions
{
    /// <summary>
    /// Blocks the current thread until the current <see cref="ManualResetEventSlim" /> is set, and then immediately sets the state of the event to nonsignalled, causing future threads to block.
    /// </summary>
    /// <param name="target"></param>
    public static void WaitOne(this ManualResetEventSlim target)
    {
        lock (target)
        {
            target.Wait();
            target.Reset();
        }
    }
}
