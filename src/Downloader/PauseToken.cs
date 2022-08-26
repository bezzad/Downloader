using System.Threading.Tasks;

namespace Downloader
{
    internal struct PauseToken
    {
        private readonly PauseTokenSource tokenSource;
        public bool IsPaused => tokenSource?.IsPaused == true;

        internal PauseToken(PauseTokenSource source)
        {
            tokenSource = source;
        }

        public Task WaitWhilePausedAsync()
        {
            return IsPaused
                ? tokenSource.WaitWhilePausedAsync()
                : PauseTokenSource.CompletedTask;
        }
    }
}
