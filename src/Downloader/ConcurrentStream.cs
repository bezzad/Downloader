using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader
{
    public class ConcurrentStream : IDisposable
    {
        private readonly SemaphoreSlim _queueConsumerLocker = new SemaphoreSlim(0);
        private readonly ManualResetEventSlim _completionEvent = new ManualResetEventSlim(true);
        private readonly ConcurrentPacketBuffer<Packet> _inputBuffer = new ConcurrentPacketBuffer<Packet>();
        private long _maxMemoryBufferBytes = 0;
        private bool _disposed;
        private Stream _stream;
        private string _path;

        public string Path
        {
            get => _path;
            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _path = value;
                    _stream = new FileStream(_path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                }
            }
        }
        public byte[] Data
        {
            get
            {
                Flush();
                if (_stream is MemoryStream mem)
                    return mem.ToArray();

                return null;
            }
            set
            {
                if (value != null)
                    _stream = new MemoryStream(value, true);
            }
        }
        public bool CanRead => _stream?.CanRead == true;    
        public bool CanSeek => _stream?.CanSeek == true;
        public bool CanWrite => _stream?.CanWrite == true;
        public long Length => _stream?.Length ?? 0;
        public long Position
        {
            get => _stream?.Position ?? 0;
            set => _stream.Position = value;
        }
        public long MaxMemoryBufferBytes
        {
            get => _maxMemoryBufferBytes;
            set
            {
                _maxMemoryBufferBytes = (value <= 0) ? long.MaxValue : value;
            }
        }

        public ConcurrentStream(Stream stream, long maxMemoryBufferBytes = 0)
        {
            _stream = stream;
            MaxMemoryBufferBytes = maxMemoryBufferBytes;
            Initial();
        }

        public ConcurrentStream(string filename, long initSize, long maxMemoryBufferBytes = 0)
        {
            _path = filename;
            _stream = new FileStream(filename, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
            MaxMemoryBufferBytes = maxMemoryBufferBytes;

            if (initSize > 0)
                SetLength(initSize);

            Initial();
        }

        // parameterless constructor for deserialization
        public ConcurrentStream()
        {
            _stream = new MemoryStream();
            Initial();
        }

        private void Initial()
        {
            Task.Run(Watcher).ConfigureAwait(false);
        }

        public Stream OpenRead()
        {
            Flush();
            Seek(0, SeekOrigin.Begin);
            return _stream;
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            var stream = OpenRead();
            return stream.Read(buffer, offset, count);
        }

        public void WriteAsync(long position, byte[] bytes, int length)
        {
            if (_inputBuffer.TryAdd(new Packet(position, bytes, length)))
            {
                _completionEvent.Reset();
                _queueConsumerLocker.Release();
            }
            StopWritingToQueueIfLimitIsExceeded(length);
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            WriteAsync(Position, buffer.Skip(offset).ToArray(), count);
        }

        private async Task Watcher()
        {
            while (!_disposed)
            {
                ResumeWriteOnQueueIfBufferEmpty();
                await _queueConsumerLocker.WaitAsync().ConfigureAwait(false);
                var packet = await _inputBuffer.TryTake().ConfigureAwait(false);
                if (packet is not null)
                {
                    await WritePacketOnFile(packet).ConfigureAwait(false);
                }
            }
        }

        public long Seek(long offset, SeekOrigin origin)
        {
            if (offset != Position && CanSeek)
            {
                _stream.Seek(offset, origin);
            }

            return Position;
        }

        public void SetLength(long value)
        {
            _stream.SetLength(value);
        }

        private void StopWritingToQueueIfLimitIsExceeded(long packetSize)
        {
            if (MaxMemoryBufferBytes < packetSize * _inputBuffer.Count)
            {
                // Stop writing packets to the queue until the memory is free
                _inputBuffer.CompleteAdding();
            }
        }

        private void ResumeWriteOnQueueIfBufferEmpty()
        {
            if (_inputBuffer.IsEmpty)
            {
                // resume writing packets to the queue
                _inputBuffer.ResumeAdding();
                _completionEvent.Set();
            }
        }

        private async Task WritePacketOnFile(Packet packet)
        {
            // seek with SeekOrigin.Begin is so faster than SeekOrigin.Current
            Seek(packet.Position, SeekOrigin.Begin);

            await _stream.WriteAsync(packet.Data, 0, packet.Length).ConfigureAwait(false);
            packet.Dispose();
        }

        public void Flush()
        {
            _completionEvent.Wait();
            _stream?.Flush();
            GC.Collect();
        }

        public void Dispose()
        {
            if (_disposed == false)
            {
                Flush();
                _disposed = true;
                _queueConsumerLocker.Dispose();
                _stream.Dispose();
                _inputBuffer.Dispose();
            }
        }
    }
}
