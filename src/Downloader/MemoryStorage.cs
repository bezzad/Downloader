using System;
using System.IO;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader
{
    [Serializable]
    public class MemoryStorage : IStorage, IDisposable, ISerializable
    {
        [NonSerialized]
        private MemoryStream _dataStream;
        public MemoryStream DataStream => _dataStream ??= new MemoryStream();
        public long Length => _dataStream?.Length ?? 0;

        public string Data
        {
            get
            {
                if (_dataStream?.CanRead == true)
                {
                    return Convert.ToBase64String(_dataStream.ToArray());
                }

                return null;
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value) == false)
                {
                    Close();
                    _dataStream = new MemoryStream();
                    var bytes = Convert.FromBase64String(value);
                    // Note: new MemoryStream(bytes) is not expandable
                    WriteAsync(bytes, 0, bytes.Length, new CancellationToken()).Wait();
                }
            }
        }

        public MemoryStorage() { }

        public MemoryStorage(SerializationInfo info, StreamingContext context)
        {
            if (info.ObjectType == typeof(MemoryStorage))
            {
                Data = info.GetValue(nameof(Data), typeof(string)) as string;
            }
        }

        public Stream OpenRead()
        {
            Flush();
            _dataStream?.Seek(0, SeekOrigin.Begin);
            return _dataStream;
        }

        public async Task WriteAsync(byte[] data, int offset, int count, CancellationToken cancellationToken)
        {
            count = Math.Min(count, data.Length);
            await DataStream.WriteAsync(data, offset, count, cancellationToken).ConfigureAwait(false);
        }

        public void Clear()
        {
            Close();
            _dataStream = null;
        }

        public void Flush()
        {
            _dataStream?.Flush();
        }

        public void Close()
        {
            _dataStream?.Dispose();
        }

        public long GetLength()
        {
            return Length;
        }

        public void Dispose()
        {
            Clear();
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue(nameof(Data), Data, typeof(string));
        }
    }
}
