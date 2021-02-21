using System;
using System.IO;
using System.Threading.Tasks;

namespace Downloader
{
    public class FileStorage : IStorage, IDisposable
    {
        public string FileName { get; }
        private FileStream _stream;

        public FileStorage(string fileName)
        {
            if (File.Exists(fileName) == false)
            {
                var directory = Path.GetDirectoryName(fileName);
                var extension = Path.GetExtension(fileName);
                FileName= FileHelper.GetTempFile(directory, extension);
            }
            else
            {
                FileName = fileName;
            }
        }

        public FileStorage(string directory, string fileExtension = "")
        {
            FileName = FileHelper.GetTempFile(directory, fileExtension);
        }

        public Stream OpenRead()
        {
            if (_stream?.CanWrite == true)
            {
                _stream.Flush();
                _stream.Dispose();
            }
            return new FileStream(FileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Delete | FileShare.ReadWrite);
        }

        public async Task WriteAsync(byte[] data, int offset, int count)
        {
            if (_stream?.CanWrite != true)
            {
                _stream = new FileStream(FileName, FileMode.Append, FileAccess.Write, FileShare.Delete | FileShare.ReadWrite);
            }
            await _stream.WriteAsync(data, offset, count);
        }

        public void Clear()
        {
            _stream?.Dispose();
            if (File.Exists(FileName))
            {
                File.Delete(FileName);
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