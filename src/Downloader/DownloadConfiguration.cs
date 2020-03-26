namespace Downloader
{
    public class DownloadConfiguration
    {
        public DownloadConfiguration()
        {
            MaxTryAgainOnFailover = 5;
            ParallelDownload = true;
            ChunkCount = 2;
            Timeout = 6000; // 6sec
            OnTheFlyDownload = true;
            BufferBlockSize = 10240;
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
