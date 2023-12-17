using Downloader.Extensions.Logging;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader;

public class ConcurrentStream : TaskStateManagement, IDisposable
{
    private ConcurrentPacketBuffer<Packet> _inputBuffer;
    private volatile bool _disposed;
    private Stream _stream;
    private string _path;
    private Task _watcher;
    private CancellationTokenSource _watcherCancelSource;

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
            if (_disposed)
                throw new ObjectDisposedException(nameof(_stream));

            if (_stream is MemoryStream mem)
                return mem.ToArray();

            return null;
        }
        set
        {
            if (value != null)
            {
                // Don't pass straight value to MemoryStream,
                // because causes stream to be an immutable array
                _stream = new MemoryStream();
                _stream.Write(value, 0, value.Length);
            }
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
        get => _inputBuffer.BufferSize;
        set => _inputBuffer.BufferSize = value;
    }

    // parameterless constructor for deserialization
    public ConcurrentStream(ILogger logger = null) : this(0, logger) { }

    public ConcurrentStream(long maxMemoryBufferBytes = 0, ILogger logger = null) : base(logger)
    {
        _stream = new MemoryStream();
        Initial(maxMemoryBufferBytes);
    }

    public ConcurrentStream(Stream stream, long maxMemoryBufferBytes = 0, ILogger logger = null) : base(logger)
    {
        _stream = stream;
        Initial(maxMemoryBufferBytes);
    }

    public ConcurrentStream(string filename, long initSize, long maxMemoryBufferBytes = 0, ILogger logger = null) : base(logger)
    {
        _path = filename;
        _stream = new FileStream(filename, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);

        if (initSize > 0)
            SetLength(initSize);

        Initial(maxMemoryBufferBytes);
    }

    private void Initial(long maxMemoryBufferBytes, ILogger logger = null)
    {
        _inputBuffer = new ConcurrentPacketBuffer<Packet>(maxMemoryBufferBytes, logger);
        _watcherCancelSource = new CancellationTokenSource();

        Task<Task> task = Task.Factory.StartNew(
            function: Watcher,
            cancellationToken: _watcherCancelSource.Token,
            creationOptions: TaskCreationOptions.LongRunning,
            scheduler: TaskScheduler.Default);

        _watcher = task.Unwrap();
    }

    public Stream OpenRead()
    {
        Seek(0, SeekOrigin.Begin);
        return _stream;
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        var stream = OpenRead();
        return stream.Read(buffer, offset, count);
    }

    public async Task WriteAsync(long position, byte[] bytes, int length, bool fireAndForget = true)
    {
        if (bytes.Length < length)
            throw new ArgumentOutOfRangeException(nameof(length));

        if (IsFaulted && Exception is not null)
            throw Exception;

        await _inputBuffer.TryAdd(new Packet(position, bytes, length));

        if (fireAndForget == false)
        {
            // to ensure that the written packet is actually stored on the stream
            await FlushAsync().ConfigureAwait(false);
        }
    }

    private async Task Watcher()
    {
        try
        {
            StartState();
            while (!_watcherCancelSource.IsCancellationRequested)
            {
                await _inputBuffer.WaitTryTakeAsync(_watcherCancelSource.Token, WritePacketOnFile).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is TaskCanceledException || ex is OperationCanceledException)
        {
            Logger?.LogError(ex, "ConcurrentStream: Call CancelState()");
            CancelState();
        }
        catch (Exception ex)
        {
            SetException(ex);
            _watcherCancelSource.Cancel(false);
        }
        finally
        {
            await Task.Yield();
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

    private async Task WritePacketOnFile(Packet packet)
    {
        // seek with SeekOrigin.Begin is so faster than SeekOrigin.Current
        Seek(packet.Position, SeekOrigin.Begin);
        await _stream.WriteAsync(packet.Data, 0, packet.Length).ConfigureAwait(false);
        packet.Dispose();
    }

    public async Task FlushAsync()
    {
        await _inputBuffer.WaitToComplete().ConfigureAwait(false);

        if (_stream?.CanRead == true)
        {
            await _stream.FlushAsync().ConfigureAwait(false);
        }

        GC.Collect();
    }

    public void Dispose()
    {
        if (_disposed == false)
        {
            _disposed = true;
            _watcherCancelSource.Cancel(); // request the cancellation
            _stream.Dispose();
            _inputBuffer.Dispose();
        }
    }
}
