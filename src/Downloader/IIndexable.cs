namespace Downloader
{
    public interface IIndexable
    {
        public long Position { get; set; }
        public long NextPosition { get; }
    }
}
