using System;

namespace Downloader
{
    public class Chunk
    {
        public Chunk(long start, long end)
        {
            Id = Guid.NewGuid().ToString("N");
            Start = start;
            End = end;
            Position = 0;
        }

        public string Id { get; }
        public long Start { get; }
        public long End { get; }
        public int Position { get; set; }
        public long Length => End - Start + 1;
        public int FailoverCount { get; set; }
        public int MaxTryAgainOnFailover { get; set; }
        public byte[] Data { get; set; }
        public string FileName { get; set; }
        public int PositionCheckpoint { get; set; } // keep last download position on failover

        public bool CanContinue() => PositionCheckpoint < Position;
        public void Checkpoint() => PositionCheckpoint = Position;
        public bool CanTryAgainOnFailover() => FailoverCount++ <= MaxTryAgainOnFailover;
    }
}
