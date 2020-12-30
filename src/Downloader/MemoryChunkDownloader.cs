using System.Threading.Tasks;

namespace Downloader
{
    public class MemoryChunkDownloader : ChunkDownloader
    {
        private readonly MemoryChunk _memoryChunk;

        public MemoryChunkDownloader(MemoryChunk chunk, int blockSize)
            : base(chunk, blockSize)
        {
            _memoryChunk = chunk;
        }

        protected override bool IsDownloadCompleted()
        {
            return base.IsDownloadCompleted() && _memoryChunk.Data?.LongLength == Chunk.Length;
        }

        protected override bool IsValidPosition()
        {
            return base.IsValidPosition() && _memoryChunk.Data != null;
        }

        protected override void CreateChunkStorage()
        {
            _memoryChunk.Data ??= new byte[Chunk.Length];
        }

        protected override Task WriteChunk(byte[] data, int count)
        {
            for (int i = 0; i < count && i < data.Length; i++)
            {
                _memoryChunk.Data[Chunk.Position + i] = data[i];
            }
            return Task.CompletedTask;
        }
    }
}