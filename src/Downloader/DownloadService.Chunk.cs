namespace Downloader
{
    public partial class DownloadService
    {
        protected class Chunk
        {
            public Chunk(long start, long end)
            {
                Start = start;
                End = end;
                Position = 0;
                Data = new byte[Length];
            }

            public long Id => Start.PairingFunction(End);
            public long Length => End - Start + 1;

            public long Start { get; set; }
            public long End { get; set; }
            public int Position { get; set; }
            public int FailoverCount { get; set; }
            public byte[] Data { get; set; }
            public string FileName { get; set; }
            public int PositionCheckpoint { get; set; } // keep last download position on failover

            public bool CanContinue() => PositionCheckpoint < Position;
            public void Checkpoint() => PositionCheckpoint = Position;
        }
    }
}
