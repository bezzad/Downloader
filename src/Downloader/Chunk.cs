using System;

namespace Downloader
{
    /// <summary>
    /// Chunk data structure
    /// </summary>
    public abstract class Chunk
    {
        protected Chunk(long start, long end)
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
        public int FailoverCount { get; protected set; }
        public int MaxTryAgainOnFailover { get; set; }
        public int Timeout { get; set; }

        public bool CanTryAgainOnFailover() => FailoverCount++ <= MaxTryAgainOnFailover;
        public virtual void Clear()
        {
            Position = 0;
            FailoverCount = 0;
            Timeout = 0;
        }
    }
}
