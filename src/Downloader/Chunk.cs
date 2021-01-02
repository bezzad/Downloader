using System;

namespace Downloader
{
    /// <summary>
    ///     Chunk data structure
    /// </summary>
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
        public long Length => (End - Start) + 1;
        public int MaxTryAgainOnFailover { get; set; }
        public int Timeout { get; set; }
        public int FailoverCount { get; private set; }
        public IStorage Storage { get; set; }

        public bool CanTryAgainOnFailover()
        {
            return FailoverCount++ <= MaxTryAgainOnFailover;
        }

        public void Clear()
        {
            Position = 0;
            FailoverCount = 0;
            Timeout = 0;
            Storage?.Clear();
        }

        public bool IsDownloadCompleted()
        {
            var streamLength = Storage?.GetLength();
            return Start + Position >= End &&
                   streamLength == Length;
        }

        public bool IsValidPosition()
        {
            return Position < Length &&
                   Storage != null;
        }
    }
}