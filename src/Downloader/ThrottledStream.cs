using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader;

/// <summary>
///     Class for streaming data with throttling support.
/// </summary>
internal class ThrottledStream : Stream
{
    public const long Infinite = long.MaxValue;
    private readonly Stream _baseStream;
    private long _bandwidthLimit;
    private Bandwidth _bandwidth;

    /// <summary>
    ///     Initializes a new instance of the <see cref="T:ThrottledStream" /> class.
    /// </summary>
    /// <param name="baseStream">The base stream.</param>
    /// <param name="bandwidthLimit">The maximum bytes per second that can be transferred through the base stream.</param>
    /// <exception cref="ArgumentNullException">Thrown when baseStream /> is a null reference.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <see cref="BandwidthLimit" /> is a negative value.</exception>
    public ThrottledStream(Stream baseStream, long bandwidthLimit)
    {
        if (bandwidthLimit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bandwidthLimit),
                bandwidthLimit, "The maximum number of bytes per second can't be negative.");
        }

        _baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
        BandwidthLimit = bandwidthLimit;
    }

    /// <summary>
    ///     Bandwidth Limit (in B/s)
    /// </summary>
    /// <value>The maximum bytes per second.</value>
    public long BandwidthLimit
    {
        get => _bandwidthLimit;
        set
        {
            _bandwidthLimit = value <= 0 ? Infinite : value;
            _bandwidth ??= new Bandwidth();
            _bandwidth.BandwidthLimit = _bandwidthLimit;
        }
    }

    /// <inheritdoc />
    public override bool CanRead => _baseStream.CanRead;

    /// <inheritdoc />
    public override bool CanSeek => _baseStream.CanSeek;

    /// <inheritdoc />
    public override bool CanWrite => _baseStream.CanWrite;

    /// <inheritdoc />
    public override long Length => _baseStream.Length;

    /// <inheritdoc />
    public override long Position
    {
        get => _baseStream.Position;
        set => _baseStream.Position = value;
    }

    /// <inheritdoc />
    public override void Flush()
    {
        _baseStream.Flush();
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin)
    {
        return _baseStream.Seek(offset, origin);
    }

    /// <inheritdoc />
    public override void SetLength(long value)
    {
        _baseStream.SetLength(value);
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        Throttle(count).Wait();
        return _baseStream.Read(buffer, offset, count);
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count,
        CancellationToken cancellationToken)
    {
        await Throttle(count).ConfigureAwait(false);
        return await _baseStream.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        Throttle(count).Wait();
        _baseStream.Write(buffer, offset, count);
    }

    /// <inheritdoc />
    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        await Throttle(count).ConfigureAwait(false);
        await _baseStream.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
    }

    public override void Close()
    {
        _baseStream.Close();
        base.Close();
    }

    private async Task Throttle(int transmissionVolume)
    {
        // Make sure the buffer isn't empty.
        if (BandwidthLimit > 0 && transmissionVolume > 0)
        {
            // Calculate the time to sleep.
            _bandwidth.CalculateSpeed(transmissionVolume);
            await Sleep(_bandwidth.PopSpeedRetrieveTime()).ConfigureAwait(false);
        }
    }

    private static async Task Sleep(int time)
    {
        if (time > 0)
        {
            await Task.Delay(time).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return _baseStream.ToString();
    }
}