namespace Downloader
{
    public class DownloadConfiguration
    {
        public DownloadConfiguration()
        {
            MaxTryAgainOnFailover = int.MaxValue;  // the maximum number of times to fail.
            ParallelDownload = true; // download parts of file as parallel or not
            ChunkCount = 2; // file parts to download
            Timeout = 2000; // timeout (millisecond) per stream block reader
            OnTheFlyDownload = true; // caching in-memory mode
            BufferBlockSize = 10240; // usually, hosts support max to 8000 bytes
        }

        /// <summary>
        /// Download file chunks as Parallel
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
        /// Stream buffer size which is used for size of blocks
        /// </summary>
        public int BufferBlockSize { get; set; }

        /// <summary>
        /// How many time try again to download on failed
        /// </summary>
        public int MaxTryAgainOnFailover { get; set; }
    }
}
