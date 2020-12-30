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
        private readonly FileChunk _fileChunk;

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
        }

        protected override async Task WriteChunk(byte[] data, int count)
        {
            using FileStream writer = new FileStream(_fileChunk.FileName, FileMode.Append, FileAccess.Write, FileShare.Delete);
            await writer.WriteAsync(data, 0, count);
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