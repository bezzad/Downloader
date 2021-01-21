using System;
using System.IO;
using System.Threading.Tasks;

namespace Downloader
{
    public class FileStorage : IStorage, IDisposable
    {
        private readonly string _fileName;
        private FileStream _stream;

        public FileStorage(string directory, string fileExtension = "")
        {
            _fileName = FileHelper.GetTempFile(directory, fileExtension);
        }

        public Stream OpenRead()
        {
            if (_stream?.CanWrite == true)
            {
                _stream.Flush();
                _stream.Dispose();
            }
            return new FileStream(_fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Delete | FileShare.ReadWrite);
        }

        public async Task WriteAsync(byte[] data, int offset, int count)
        {
            if (_stream?.CanWrite != true)
            {
                _stream = new FileStream(_fileName, FileMode.Append, FileAccess.Write, FileShare.Delete | FileShare.ReadWrite);
            }
            await _stream.WriteAsync(data, offset, count);
        }

        public void Clear()
        {
            _stream?.Dispose();
            if (File.Exists(_fileName))
            {
                File.Delete(_fileName);
            }
        }

        public long GetLength()
        {
            return OpenRead()?.Length ?? 0;
        }

        public void Dispose()
        {
            Clear();
        }
    }
}