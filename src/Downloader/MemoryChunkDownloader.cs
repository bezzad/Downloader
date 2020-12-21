using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader
{
    public class MemoryChunkDownloader : ChunkDownloader
    {
        public MemoryChunkDownloader(MemoryChunk chunk, int blockSize)
            : base(chunk, blockSize)
        { }

        private MemoryChunk MemoryChunk => (MemoryChunk)Chunk;

        protected override bool IsDownloadCompleted()
        {
            return base.IsDownloadCompleted() && MemoryChunk.Data?.LongLength == Chunk.Length;
        }
        protected override bool IsValidPosition()
        {
            return base.IsValidPosition() && MemoryChunk.Data != null;
        }
        protected override async Task ReadStream(Stream stream, CancellationToken token)
        {
            var bytesToReceiveCount = Chunk.Length - Chunk.Position;
            MemoryChunk.Data ??= new byte[Chunk.Length];
            while (bytesToReceiveCount > 0)
            {
                if (token.IsCancellationRequested)
                    return;

                using var innerCts = new CancellationTokenSource(Chunk.Timeout);
                var count = bytesToReceiveCount > BufferBlockSize
                    ? BufferBlockSize : (int)bytesToReceiveCount;
                var readSize = await stream.ReadAsync(MemoryChunk.Data, Chunk.Position, count, innerCts.Token);
                Chunk.Position += readSize;
                bytesToReceiveCount = Chunk.Length - Chunk.Position;

                OnDownloadProgressChanged(new DownloadProgressChangedEventArgs(Chunk.Id) {
                    TotalBytesToReceive = Chunk.Length,
                    BytesReceived = Chunk.Position,
                    ProgressedByteSize = readSize
                });
            }
        }
    }
}
