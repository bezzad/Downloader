using System.IO;
using System.Threading;
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

        protected override async Task ReadStream(Stream stream, CancellationToken token)
        {
            long bytesToReceiveCount = Chunk.Length - Chunk.Position;
            _memoryChunk.Data ??= new byte[Chunk.Length];
            while (bytesToReceiveCount > 0)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                using CancellationTokenSource innerCts = new CancellationTokenSource(Chunk.Timeout);
                int count = bytesToReceiveCount > BufferBlockSize
                    ? BufferBlockSize
                    : (int)bytesToReceiveCount;
                int readSize = await stream.ReadAsync(_memoryChunk.Data, Chunk.Position, count, innerCts.Token);
                Chunk.Position += readSize;
                bytesToReceiveCount = Chunk.Length - Chunk.Position;

                OnDownloadProgressChanged(new DownloadProgressChangedEventArgs(Chunk.Id) {
                    TotalBytesToReceive = Chunk.Length, BytesReceived = Chunk.Position, ProgressedByteSize = readSize
                });
            }
        }
    }
}