using System.IO;
using System.Threading.Tasks;

namespace Downloader
{
    public class MemoryStorage : IStorage
    {
        private byte[] _data;

        public MemoryStorage(long length)
        {
            _data = new byte[length];
        }

        public Stream OpenRead()
        {
            return new MemoryStream(_data);
        }

        public Task WriteAsync(byte[] data, int offset, int count)
        {
            for (int i = 0; i < count && i < data.Length; i++)
            {
                _data[offset + i] = data[i];
            }
            return Task.CompletedTask;
        }

        public void Clear()
        {
            _data = null;
        }

        public long GetLength()
        {
            return _data?.LongLength ?? 0;
        }
    }
}
