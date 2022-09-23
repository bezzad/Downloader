using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader
{
    internal class ChunkHub
    {
        private readonly DownloadConfiguration _config;

        public ChunkHub(DownloadConfiguration config)
        {
            _config = config;
        }

        public Chunk[] ChunkFile(long fileSize)
        {
            var parts = _config.ChunkCount;
            var start = _config.RangeLow;

            if (start < 0)
            {
                start = 0;
            }

            if (fileSize < parts)
            {
                parts = (int)fileSize;
            }

            if (parts < 1)
            {
                parts = 1;
            }

            long chunkSize = fileSize / parts;
            Chunk[] chunks = new Chunk[parts];
            for (int i = 0; i < parts; i++)
            {
                long startPosition = start + (i * chunkSize);
                long endPosition = startPosition + chunkSize - 1;
                chunks[i] = GetChunk(i.ToString(), startPosition, endPosition);
            }
            chunks.Last().End += fileSize % parts; // add remaining bytes to last chunk

            return chunks;
        }

        private Chunk GetChunk(string id, long start, long end)
        {
            var chunk = new Chunk(start, end) {
                Id = id,
                MaxTryAgainOnFailover = _config.MaxTryAgainOnFailover,
                Timeout = _config.Timeout
            };
            return GetStorableChunk(chunk);
        }

        private Chunk GetStorableChunk(Chunk chunk)
        {
            if (_config.OnTheFlyDownload)
            {
                chunk.Storage = new MemoryStorage();
            }
            else
            {
                chunk.Storage = new FileStorage(_config.TempDirectory, _config.TempFilesExtension);
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