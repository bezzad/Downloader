using System.Threading.Tasks;

namespace Downloader
{
    public struct PauseToken
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
                : Task.FromResult(true);
        }
    }
}
