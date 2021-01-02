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

        public Chunk[] ChunkFile(long fileSize, int parts)
        {
            if (parts < 1)
            {
                parts = 1;
            }

            long chunkSize = fileSize / parts;

            if (chunkSize < 1)
            {
                chunkSize = 1;
                parts = (int)fileSize;
            }

            Chunk[] chunks = new Chunk[parts];
            for (int i = 0; i < parts; i++)
            {
                bool isLastChunk = i == parts - 1;
                long startPosition = i * chunkSize;
                long endPosition = isLastChunk ? fileSize - 1 : (startPosition + chunkSize) - 1;

                Chunk chunk =
                    new Chunk(startPosition, endPosition) {
                        MaxTryAgainOnFailover = _maxTryAgainOnFailover,
                        Timeout = _timeout
                    };
                chunks[i] = chunk;
            }

            return chunks;
        }

        public async Task MergeChunks(IEnumerable<Chunk> chunks, string fileName)
        {
            using Stream destinationStream = FileHelper.CreateFile(fileName);
            foreach (Chunk chunk in chunks.OrderBy(c => c.Start))
            {
                using Stream reader = chunk.Storage.Read();
                await reader.CopyToAsync(destinationStream);
            }
        }
    }
}