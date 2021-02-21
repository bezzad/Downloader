using System;

namespace Downloader
{
    /// <summary>
    ///     Chunk data structure
    /// </summary>
    [Serializable]
    public class Chunk
    {
        public Chunk(long start, long end)
        {
            Id = Guid.NewGuid().ToString("N");
            Start = start;
            End = end;
            Position = 0;
        }

        public string Id { get; set; }
        public long Start { get; }
        public long End { get; }
        public long Position { get; set; }
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
            var isNoneEmptyFile = streamLength > 0 && Length > 0;
            var isChunkedFilledWithBytes = Start + Position >= End;
            var streamSizeIsEqualByChunk = streamLength == Length;

            return isNoneEmptyFile && isChunkedFilledWithBytes && streamSizeIsEqualByChunk;
        }

        public bool IsValidPosition()
        {
            return Position == 0 || Length == 0 ||
                   (Position > 0 && Position < Length && Position == Storage?.GetLength());
        }

        public void SetValidPosition()
        {
            if (!IsValidPosition())
                Position = 0;
        }
    }
}