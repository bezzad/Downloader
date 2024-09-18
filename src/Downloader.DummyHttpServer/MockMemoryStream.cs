using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader.DummyHttpServer;

[ExcludeFromCodeCoverage]
public class MockMemoryStream : MemoryStream
{
    private readonly long _failureOffset = 0;
    private readonly TimeSpan _delayTime = new TimeSpan(1000);
    private readonly byte _value = 255;
    private readonly bool _timeout = false;
    public TimeSpan TimeoutDelay { get; set; }

    public MockMemoryStream(long size, long failureOffset = 0, bool timeout = false)
    {
        SetLength(size);
        _failureOffset = failureOffset;
        _timeout = timeout;
        TimeoutDelay = new TimeSpan(10000 * 60); // 1min
        GC.Collect();
    }

    // called when framework will be .NetCore 3.1
    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var validCount = await ReadAsync(count);
        Array.Fill(buffer, _value, offset, validCount);
        return validCount;
    }

    // called when framework will be .Net 6.0
    public override async ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var validCount = await ReadAsync(destination.Length);
        destination.Span.Fill(_value);
        return validCount;
    }

    private async ValueTask<int> ReadAsync(int count)
    {
        var validCount = Math.Min(Math.Min(count, Length - Position), _failureOffset - Position);
        if (validCount == 0 && _failureOffset > 0)
        {
            if (_failureOffset < Length)
            {
                if (_timeout)
                    await Task.Delay(TimeoutDelay);
                else
                    throw new DummyApiException("The download broke after failure offset");
            }
            else
            {
                validCount = _failureOffset - Position;
            }
        }

        await Task.Delay(_delayTime);
        Position += validCount;
        return (int)validCount;
    }
}
