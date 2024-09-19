using System.Threading.Tasks;

namespace Downloader;

/// <summary>
/// Represents a pause token that can be used to pause and resume operations.
/// </summary>
public record PauseToken
{
    private readonly PauseTokenSource _tokenSource;

    /// <summary>
    /// Gets a value indicating whether the operation is paused.
    /// </summary>
    public bool IsPaused => _tokenSource?.IsPaused == true;

    /// <summary>
    /// Initializes a new instance of the <see cref="PauseToken"/> class.
    /// </summary>
    /// <param name="source">The pause token source.</param>
    internal PauseToken(PauseTokenSource source)
    {
        _tokenSource = source;
    }

    /// <summary>
    /// Waits asynchronously while the operation is paused.
    /// </summary>
    /// <returns>A task that represents the asynchronous wait operation.</returns>
    public Task WaitWhilePausedAsync()
    {
        return IsPaused
            ? _tokenSource.WaitWhilePausedAsync()
            : Task.FromResult(true);
    }
}