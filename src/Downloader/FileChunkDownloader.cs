using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader
{
    public class FileChunkDownloader : ChunkDownloader
    {
        private readonly string _tempDirectory;
        private readonly string _tempFilesExtension;
        private FileChunk FileChunk => (FileChunk)Chunk;

        public FileChunkDownloader(FileChunk chunk, int blockSize, string tempDirectory, string tempFileExtension)
            : base(chunk, blockSize)
        {
            _tempDirectory = tempDirectory;
            _tempFilesExtension = tempFileExtension;
        }

        protected override async Task ReadStream(Stream stream, CancellationToken token)
        {
            long bytesToReceiveCount = Chunk.Length - Chunk.Position;
            if (string.IsNullOrWhiteSpace(FileChunk.FileName) || File.Exists(FileChunk.FileName) == false)
            {
                FileChunk.FileName = GetTempFile(_tempDirectory, _tempFilesExtension);
            }

            using FileStream writer =
                new FileStream(FileChunk.FileName, FileMode.Append, FileAccess.Write, FileShare.Delete);
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
                byte[] buffer = new byte[count];
                int readSize = await stream.ReadAsync(buffer, 0, count, innerCts.Token);
                await writer.WriteAsync(buffer, 0, readSize, innerCts.Token);
                Chunk.Position += readSize;
                bytesToReceiveCount = Chunk.Length - Chunk.Position;

                OnDownloadProgressChanged(new DownloadProgressChangedEventArgs(Chunk.Id) {
                    TotalBytesToReceive = Chunk.Length, BytesReceived = Chunk.Position, ProgressedByteSize = readSize
                });
            }
        }

        protected string GetTempFile(string baseDirectory, string fileExtension = "")
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                return Path.GetTempFileName();
            }

            if (!Directory.Exists(baseDirectory))
            {
                Directory.CreateDirectory(baseDirectory);
            }

            string filename = Path.Combine(baseDirectory, Guid.NewGuid().ToString("N") + fileExtension);
            File.Create(filename).Close();

            return filename;
        }
    }
}