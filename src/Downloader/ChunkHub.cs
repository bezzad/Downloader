using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader
{
    internal class ChunkHub
    {
        private readonly DownloadConfiguration _configuration;

        public ChunkHub(DownloadConfiguration config)
        {
            _configuration = config;
        }

        public Chunk[] ChunkFile(long fileSize, long parts) => ChunkFileRange(fileSize, 0, fileSize, parts);
        
        public Chunk[] ChunkFileRange(long fileSize, long rangeLow, long rangeHigh, long parts)
        {
            if (rangeLow >= fileSize)
            {
                rangeLow = fileSize - 1;
            }

            if (rangeLow < 0)
            {
                rangeLow = 0;
            }

            if (rangeHigh < 0)
            {
                rangeHigh = 0;
            }

            if (rangeLow > rangeHigh)
            {
                rangeLow = rangeHigh;
            }

            if (rangeHigh >= fileSize)
            {
                rangeHigh = fileSize - 1;
            }

            long downloadSize = rangeHigh - rangeLow;

            if (downloadSize < parts)
            {
                parts = downloadSize + 1;
            }

            if (parts < 1)
            {
                parts = 1;
            }

            long chunkSize = downloadSize / parts;
            Chunk[] chunks = new Chunk[parts];
            long startPosition = rangeLow;
            for (int i = 0; i < parts; i++)
            {
                bool isLastChunk = i == parts - 1;
                long endPosition = isLastChunk ? rangeHigh : System.Math.Min(rangeHigh, startPosition + chunkSize) - 1;
                chunks[i] = GetChunk(i.ToString(), startPosition, endPosition);
                startPosition += chunkSize;
            }

            return chunks;
        }

        private Chunk GetChunk(string id, long start, long end)
        {
            var chunk = new Chunk(start, end) {
                Id = id,
                MaxTryAgainOnFailover = _configuration.MaxTryAgainOnFailover,
                Timeout = _configuration.Timeout
            };
            return GetStorableChunk(chunk);
        }

        private Chunk GetStorableChunk(Chunk chunk)
        {
            if (_configuration.OnTheFlyDownload)
            {
                chunk.Storage = new MemoryStorage();
            }
            else
            {
                chunk.Storage = new FileStorage(_configuration.TempDirectory, _configuration.TempFilesExtension);
            }

            return chunk;
        }

        public async Task MergeChunks(IEnumerable<Chunk> chunks, Stream destinationStream, CancellationToken cancellationToken)
        {
            foreach (Chunk chunk in chunks.OrderBy(c => c.Start))
            {
                cancellationToken.ThrowIfCancellationRequested();
                using Stream reader = chunk.Storage.OpenRead();
                await reader.CopyToAsync(destinationStream).ConfigureAwait(false);
            }
        }
    }
}