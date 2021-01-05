using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Downloader
{
    public class ChunkHub
    {
        private readonly int _maxTryAgainOnFailover;
        private readonly int _timeout;

        public ChunkHub(int maxTryAgainOnFailover, int timeout)
        {
            _maxTryAgainOnFailover = maxTryAgainOnFailover;
            _timeout = timeout;
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
                chunks[i] =
                    new Chunk(startPosition, endPosition) {
                        MaxTryAgainOnFailover = _maxTryAgainOnFailover,
                        Timeout = _timeout
                    };
            }

            return chunks;
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