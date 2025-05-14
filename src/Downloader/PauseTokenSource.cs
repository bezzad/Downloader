using System.Threading;
using System.Threading.Tasks;

namespace Downloader;

/// <summary>
/// Represents a source for creating and managing pause tokens.
/// </summary>
public class PauseTokenSource
{
    private volatile TaskCompletionSource<bool> _tcsPaused;

    /// <summary>
    /// Gets the pause token associated with this source.
    /// </summary>
    public PauseToken Token => new(this);

    /// <summary>
    /// Gets a value indicating whether the operation is paused.
    /// </summary>
    public bool IsPaused => _tcsPaused != null;

    /// <summary>
    /// Pauses the operation by creating a new task completion source.
    /// </summary>
    public void Pause()
    {
        // if (tcsPause == null) tcsPause = new TaskCompletionSource<bool>();
        Interlocked.CompareExchange(ref _tcsPaused, new TaskCompletionSource<bool>(), null);
    }

    /// <summary>
    /// Resumes the operation by setting the result of the task completion source and resetting it.
    /// </summary>
    public void Resume()
    {
        // we need to do this in a standard compare-exchange loop:
        // grab the current value, do the compare exchange assuming that value,
        // and if the value actually changed between the time we grabbed it
        // and the time we did the compare-exchange, repeat.
        while (true)
        {
            TaskCompletionSource<bool> tcs = _tcsPaused;

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

    /// <summary>
    /// Waits asynchronously while the operation is paused.
    /// </summary>
    /// <returns>A task that represents the asynchronous wait operation.</returns>
    internal Task WaitWhilePausedAsync()
    {
        return _tcsPaused?.Task ?? Task.FromResult(true);
    }
}