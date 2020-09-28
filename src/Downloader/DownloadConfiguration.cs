using System;
using System.IO;

namespace Downloader
{
    public class DownloadConfiguration
    {
        public DownloadConfiguration()
        {
            MaxTryAgainOnFailover = int.MaxValue;  // the maximum number of times to fail.
            ParallelDownload = false; // download parts of file as parallel or not
            ChunkCount = 1; // file parts to download
            Timeout = 1000; // timeout (millisecond) per stream block reader
            OnTheFlyDownload = true; // caching in-memory mode
            BufferBlockSize = 8000; // usually, hosts support max to 8000 bytes
            MaximumBytesPerSecond = ThrottledStream.Infinite; // No-limitation in download speed
            RequestConfiguration = new RequestConfiguration(); // Default requests configuration
            TempDirectory = Path.GetTempPath(); // Default chunks path
        }


        /// <summary>
        /// Can fetch file size by HEAD request or must be used GET method to support host.
        /// Default value is True.
        /// </summary>
        public bool AllowedHeadRequest { get; set; } = true;

        /// <summary>
        /// Size per Buffer blocks in Byte unit
        /// </summary>
        public int MinimumBufferBlockSize { get; } = 1024;

        /// <summary>
        /// Download file chunks as Parallel or Serial?
        /// </summary>
        public bool ParallelDownload { get; set; }

        /// <summary>
        /// Download timeout per stream file blocks
        /// </summary>
        public int Timeout { get; set; }

        /// <summary>
        /// download file without caching chunks in disk. In the other words,
        /// all chunks stored in memory.
        /// </summary>
        public bool OnTheFlyDownload { get; set; }

        /// <summary>
        /// File chunking parts count
        /// </summary>
        public int ChunkCount { get; set; }

        /// <summary>
        /// Chunk files storage path when the OnTheFlyDownload is false.
        /// </summary>
        public string TempDirectory { get; set; }
            
        /// <summary>
        /// Stream buffer size which is used for size of blocks
        /// </summary>
        public int BufferBlockSize { get; set; }

        /// <summary>
        /// How many time try again to download on failed
        /// </summary>
        public int MaxTryAgainOnFailover { get; set; }

        /// <summary>
        /// The maximum bytes per second that can be transferred through the base stream.
        /// </summary>
        public long MaximumBytesPerSecond { get; set; }
        
        /// <summary>
        /// Custom body of your requests
        /// </summary>
        public RequestConfiguration RequestConfiguration { get; set; }



        public void Validate()
        {
            var maxSpeedPerChunk = MaximumBytesPerSecond / ChunkCount;
            ChunkCount = Math.Max(1, ChunkCount);
            MaximumBytesPerSecond = Math.Max(0, MaximumBytesPerSecond);
            BufferBlockSize = (int)Math.Min(Math.Max(maxSpeedPerChunk, MinimumBufferBlockSize), BufferBlockSize);
        }
    }
}
