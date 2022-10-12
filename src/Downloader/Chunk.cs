using System;

namespace Downloader
{
    /// <summary>
    ///     Chunk data structure
    /// </summary>
    public class Chunk
    {
        public Chunk()
        {
            Id = Guid.NewGuid().ToString("N");
        }
        public Chunk(long start, long end) : this()
        {
            Start = start;
            End = end;
        }

        public string Id { get; set; }
        public long Start { get; set; }
        public long End { get; set; }
        public long Position { get; set; }
        public int MaxTryAgainOnFailover { get; set; }
        public int Timeout { get; set; }
        public int FailoverCount { get; private set; }
        public long Length => End - Start + 1;

        public bool CanTryAgainOnFailover()
        {
            return FailoverCount++ < MaxTryAgainOnFailover;
        }

        public void Clear()
        {
            Position = 0;
            FailoverCount = 0;
        }

        public bool IsDownloadCompleted()
        {
            var isNoneEmptyFile = Length > 0;
            var isChunkedFilledWithBytes = Start + Position >= End;

            return isNoneEmptyFile && isChunkedFilledWithBytes;
        }

        public bool IsValidPosition()
        {
            return Length == 0 || (Position >= 0 && Position <= Length);
        }
    }
}