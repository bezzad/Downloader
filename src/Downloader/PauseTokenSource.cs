using System.Threading;
using System.Threading.Tasks;

namespace Downloader;

public class PauseTokenSource
{
    private volatile TaskCompletionSource<bool> _tcsPaused;

    public PauseToken Token => new PauseToken(this);
    public bool IsPaused => _tcsPaused != null;

    public void Pause()
    {
        // if (tcsPause == null) tcsPause = new TaskCompletionSource<bool>();
        Interlocked.CompareExchange(ref _tcsPaused, new TaskCompletionSource<bool>(), null);
    }

    public void Resume()
    {
        // we need to do this in a standard compare-exchange loop:
        // grab the current value, do the compare exchange assuming that value,
        // and if the value actually changed between the time we grabbed it
        // and the time we did the compare-exchange, repeat.
        while (true)
        {
            var tcs = _tcsPaused;

            if (tcs == null)
                return;

            // if(tcsPaused == tcs) tcsPaused = null;
            if (Interlocked.CompareExchange(ref _tcsPaused, null, tcs) == tcs)
            {
                tcs.SetResult(true);
                return;
            }
        }
    }

    internal Task WaitWhilePausedAsync()
    {
        return _tcsPaused?.Task ?? Task.FromResult(true);
    }
}
