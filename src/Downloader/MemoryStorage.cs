using System;
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
            count = Math.Min(count, data.Length);

            if (offset +  count > GetLength())
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "The count from the given offset is more than this storage length.");
            }

            for (int i = 0; i < count; i++)
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
