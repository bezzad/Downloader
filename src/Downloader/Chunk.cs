using System;
using System.IO;

namespace Downloader
{
    internal delegate Stream StreamProvider(long offset, long size);

    /// <summary>
    ///     Chunk data structure
    /// </summary>
    [Serializable]
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
        internal Stream Storage { get; set; }
        internal StreamProvider StorageProvider { get; set; }

        public bool CanTryAgainOnFailover()
        {
            return FailoverCount++ < MaxTryAgainOnFailover;
        }

        public void Clear()
        {
            Position = 0;
            FailoverCount = 0;
            Storage?.Close();
        }

        public void Refresh()
        {
            Clear();            
            Storage = StorageProvider?.Invoke(Start, End-Start+1);
        }

        public void Flush()
        {
            if (Storage?.CanWrite == true)
                Storage?.Flush();
        }

        public bool IsDownloadCompleted()
        {
            var streamLength = Storage?.Position;
            var isNoneEmptyFile = streamLength > 0 && Length > 0;
            var isChunkedFilledWithBytes = Start + Position >= End;
            var streamSizeIsEqualByChunk = streamLength == Length;

            return isNoneEmptyFile && isChunkedFilledWithBytes && streamSizeIsEqualByChunk;
        }

        public bool IsValidPosition()
        {
            var storagePosition = Storage?.Position ?? 0;
            return Length == 0 || (Position >= 0 && Position <= Length && Position == storagePosition);
        }

        public void SetValidPosition()
        {
            Position = Storage?.Position ?? 0;
        }
    }
}