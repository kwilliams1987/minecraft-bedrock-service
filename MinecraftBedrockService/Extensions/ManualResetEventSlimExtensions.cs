namespace System.Threading
{
    public static class ManualResetEventSlimExtensions
    {
        public static void WaitOne(this ManualResetEventSlim target)
        {
            lock (target)
            {
                target.Wait();
                target.Reset();
            }
        }
    }
}
