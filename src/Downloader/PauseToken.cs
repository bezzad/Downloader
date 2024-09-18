using System.Threading.Tasks;

namespace Downloader;

public struct PauseToken
{
    private readonly PauseTokenSource _tokenSource;
    public bool IsPaused => _tokenSource?.IsPaused == true;

    internal PauseToken(PauseTokenSource source)
    {
        _tokenSource = source;
    }

    public Task WaitWhilePausedAsync()
    {
        return IsPaused
            ? _tokenSource.WaitWhilePausedAsync()
            : Task.FromResult(true);
    }
}
