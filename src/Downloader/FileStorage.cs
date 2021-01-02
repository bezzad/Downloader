using System.IO;
using System.Threading.Tasks;

namespace Downloader
{
    public class FileStorage : IStorage
    {
        private readonly string _fileName;

        public FileStorage(string directory, string fileExtension = "")
        {
            _fileName = FileHelper.GetTempFile(directory, fileExtension);
        }

        public Stream OpenRead()
        { 
            return new FileStream(_fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Delete | FileShare.ReadWrite);
        }

        public async Task WriteAsync(byte[] data, int offset, int count)
        {
            using var writer = new FileStream(_fileName, FileMode.Append, FileAccess.Write, FileShare.Delete | FileShare.ReadWrite);
            await writer.WriteAsync(data, 0, count);
        }

        public void Clear()
        {
            if (File.Exists(_fileName))
            {
                File.Delete(_fileName);
            }
        }

        public long GetLength()
        {
            return OpenRead()?.Length ?? 0;
        }
    }
}
