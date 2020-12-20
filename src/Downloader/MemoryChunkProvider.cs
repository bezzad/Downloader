using System.Linq;
using System.Threading.Tasks;

namespace Downloader
{
    public class MemoryChunkProvider : ChunkProvider
    {
        public MemoryChunkProvider(DownloadConfiguration config) : base(config)
        { }

        public override Chunk Factory(long startPosition, long endPosition)
        {
            return new MemoryChunk(startPosition, endPosition);
        }

        public override async Task MergeChunks(Chunk[] chunks, string fileName)
        {
            using var destinationStream = CreateFile(fileName);
            foreach (var chunk in chunks.OrderBy(c => c.Start))
            {
                if (chunk is MemoryChunk memoryChunk)
                {
                    await destinationStream.WriteAsync(memoryChunk.Data, 0, (int)chunk.Length);
                }
            }
        }
        public override ChunkDownloader GetChunkDownloader(Chunk chunk)
        {
            return new MemoryChunkDownloader((MemoryChunk)chunk, Configuration.BufferBlockSize);
        }
    }
}
