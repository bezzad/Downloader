using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader
{
    public class FileChunkDownloader : ChunkDownloader<FileChunk>
    {
        public FileChunkDownloader(FileChunk chunk, int blockSize, string tempDirectory, string tempFileExtension)
            : base(chunk, blockSize)
        {
            TempDirectory = tempDirectory;
            TempFilesExtension = tempFileExtension;
        }

        protected string TempDirectory { get; }
        protected string TempFilesExtension { get; }

        protected override async Task ReadStream(Stream stream, CancellationToken token)
        {
            var bytesToReceiveCount = Chunk.Length - Chunk.Position;
            if (string.IsNullOrWhiteSpace(Chunk.FileName) || File.Exists(Chunk.FileName) == false)
                Chunk.FileName = GetTempFile(TempDirectory, TempFilesExtension);

            using var writer = new FileStream(Chunk.FileName, FileMode.Append, FileAccess.Write, FileShare.Delete);
            while (bytesToReceiveCount > 0)
            {
                if (token.IsCancellationRequested)
                    return;

                using var innerCts = new CancellationTokenSource(Chunk.Timeout);
                var count = bytesToReceiveCount > BufferBlockSize
                    ? BufferBlockSize : (int)bytesToReceiveCount;
                var buffer = new byte[count];
                var readSize = await stream.ReadAsync(buffer, 0, count, innerCts.Token);
                await writer.WriteAsync(buffer, 0, readSize, innerCts.Token);
                Chunk.Position += readSize;
                bytesToReceiveCount = Chunk.Length - Chunk.Position;

                OnDownloadProgressChanged(new DownloadProgressChangedEventArgs(Chunk.Id) {
                    TotalBytesToReceive = Chunk.Length,
                    BytesReceived = Chunk.Position,
                    ProgressedByteSize = readSize
                });
            }
        }
        protected string GetTempFile(string baseDirectory, string fileExtension = "")
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
                return Path.GetTempFileName();

            if (!Directory.Exists(baseDirectory))
                Directory.CreateDirectory(baseDirectory);

            var filename = Path.Combine(baseDirectory, Guid.NewGuid().ToString("N") + fileExtension);
            File.Create(filename).Close();

            return filename;
        }
    }
}
