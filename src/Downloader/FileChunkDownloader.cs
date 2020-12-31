using System;
using System.IO;
using System.Threading.Tasks;

namespace Downloader
{
    public class FileChunkDownloader : ChunkDownloader
    {
        private readonly string _tempDirectory;
        private readonly string _tempFilesExtension;
        private readonly FileChunk _fileChunk;
        private Stream _storageStream;

        public FileChunkDownloader(FileChunk chunk, int blockSize, string tempDirectory, string tempFileExtension)
            : base(chunk, blockSize)
        {
            _tempDirectory = tempDirectory;
            _tempFilesExtension = tempFileExtension;
            _fileChunk = chunk;
        }

        protected override void CreateChunkStorage()
        {
            if (string.IsNullOrWhiteSpace(_fileChunk.FileName) || File.Exists(_fileChunk.FileName) == false)
            {
                _fileChunk.FileName = GetTempFile(_tempDirectory, _tempFilesExtension);
            }
            _storageStream ??= new FileStream(_fileChunk.FileName, FileMode.Append, FileAccess.Write, FileShare.Delete);
        }

        protected override async Task WriteChunk(byte[] data, int count)
        {
            await _storageStream.WriteAsync(data, 0, count);
        }

        protected override void OnCloseStream()
        {
            _storageStream?.Dispose();
            _storageStream = null;
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