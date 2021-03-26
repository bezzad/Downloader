using System;
using System.IO;
using System.Threading.Tasks;

namespace Downloader
{
    [Serializable]
    public class FileStorage : IStorage, IDisposable
    {
        [NonSerialized] private FileStream _stream;
        private string _fileName;
        public string FileName
        {
            get => _fileName ??= FileHelper.GetTempFile();
            set => _fileName = value;
        }

        public FileStorage() { }

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
        
        public FileStorage(string directory, string fileExtension)
        {
            FileName = FileHelper.GetTempFile(directory, fileExtension);
        }

        public Stream OpenRead()
        {
            Close();
            return File.Open(FileName, FileMode.OpenOrCreate, FileAccess.Read, FileShare.Delete | FileShare.ReadWrite);
        }

        public async Task WriteAsync(byte[] data, int offset, int count)
        {
            if (_stream?.CanWrite != true)
            {
                _stream = new FileStream(FileName, FileMode.Append, FileAccess.Write, FileShare.Delete | FileShare.ReadWrite);
            }
            await _stream.WriteAsync(data, offset, count).ConfigureAwait(false);
        }

        public void Clear()
        {
            Close();
            if (File.Exists(FileName))
            {
                File.Delete(FileName);
            }
        }

        public void Flush()
        {
            Close();
        }

        public void Close()
        {
            if (_stream?.CanWrite == true)
            {
                _stream?.Flush();
            }
            _stream?.Dispose();
        }

        public long GetLength()
        {
            using var stream = OpenRead();
            return stream?.Length ?? 0;
        }

        public void Dispose()
        {
            Clear();
        }
    }
}