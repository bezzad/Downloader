using System.Threading;
using System.Threading.Tasks;

namespace Downloader
{
    internal class PauseTokenSource
    {
        private volatile TaskCompletionSource<bool> tcsPaused;
        internal static readonly Task CompletedTask = Task.FromResult(true);

        public PauseToken Token => new PauseToken(this);
        public bool IsPaused => tcsPaused != null;

        public void Pause()
        {
            // if (tcsPause == new TaskCompletionSource<bool>()) tcsPause = null;
            Interlocked.CompareExchange(ref tcsPaused, new TaskCompletionSource<bool>(), null);
        }

        public void Resume()
        {
            // we need to do this in a standard compare-exchange loop:
            // grab the current value, do the compare exchange assuming that value,
            // and if the value actually changed between the time we grabbed it
            // and the time we did the compare-exchange, repeat.
            while (true)
            {
                var tcs = tcsPaused;

                if (tcs == null)
                    return;

                if (Interlocked.CompareExchange(ref tcsPaused, null, tcs) == tcs)
                {
                    tcs.SetResult(true);
                    break;
                }
            }
        }

        internal Task WaitWhilePausedAsync()
        {
            return tcsPaused?.Task ?? CompletedTask;
        }
    }
}
