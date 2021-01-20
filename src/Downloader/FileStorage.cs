using System;
using System.IO;
using System.Threading.Tasks;

namespace Downloader
{
    public class FileStorage : IStorage, IDisposable
    {
        private readonly string _fileName;
        private readonly FileStream _writer;

        public FileStorage(string directory, string fileExtension = "")
        {
            _fileName = FileHelper.GetTempFile(directory, fileExtension);
            _writer = new FileStream(_fileName, FileMode.Append, FileAccess.Write, FileShare.Delete | FileShare.ReadWrite);
        }

        public Stream OpenRead()
        { 
            return new FileStream(_fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Delete | FileShare.ReadWrite);
        }

        public async Task WriteAsync(byte[] data, int offset, int count)
        {
            await _writer.WriteAsync(data, offset, count);
        }

        public void Clear()
        {
            _writer.Close();
            
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
