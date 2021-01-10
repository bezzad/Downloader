using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Downloader
{
    public class ChunkHub
    {
        private readonly DownloadConfiguration _configuration;

        public ChunkHub(DownloadConfiguration config)
        {
            _configuration = config;
        }

        public Chunk[] ChunkFile(long fileSize, long parts)
        {
            if (fileSize < parts)
            {
                parts = fileSize;
            }

            if (parts < 1)
            {
                parts = 1;
            }

            long chunkSize = fileSize / parts;
            Chunk[] chunks = new Chunk[parts];
            for (int i = 0; i < parts; i++)
            {
                bool isLastChunk = i == parts - 1;
                long startPosition = i * chunkSize;
                long endPosition = (isLastChunk ? fileSize : startPosition + chunkSize) - 1;
                chunks[i] = GetChunk(startPosition, endPosition);
            }

            return chunks;
        }

        private Chunk GetChunk(long start, long end)
        {
            var chunk = new Chunk(start, end) {
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

        public async Task MergeChunks(IEnumerable<Chunk> chunks, string fileName)
        {
            using Stream destinationStream = FileHelper.CreateFile(fileName);
            foreach (Chunk chunk in chunks.OrderBy(c => c.Start))
            {
                using Stream reader = chunk.Storage.OpenRead();
                await reader.CopyToAsync(destinationStream);
            }
        }
    }
}