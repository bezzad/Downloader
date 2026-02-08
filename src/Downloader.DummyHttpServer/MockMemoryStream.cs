using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;

namespace Downloader.DummyHttpServer;

[ExcludeFromCodeCoverage]
public class MockMemoryStream : MemoryStream
{
    private readonly long _failureOffset;
    private readonly TimeSpan _delayTime = new(1000);
    private const byte Value = 255;
    private readonly bool _timeout;
    private TimeSpan TimeoutDelay { get; set; }

    public MockMemoryStream(long size, long failureOffset = 0, bool timeout = false)
    {
        SetLength(size);
        _failureOffset = failureOffset;
        _timeout = timeout;
        TimeoutDelay = TimeSpan.FromSeconds(2); // 2 seconds timeout
    }

    public sealed override void SetLength(long value)
    {
        base.SetLength(value);
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return await ReadAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> destination,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int validCount = await ReadAsync(destination.Length);
        destination.Span.Fill(Value);
        return validCount;
    }

    private async ValueTask<int> ReadAsync(int count)
    {
        long validCount = Math.Min(Math.Min(count, Length - Position), _failureOffset - Position);
        if (validCount == 0 && _failureOffset > 0)
        {
            if (_failureOffset < Length)
            {
                if (_timeout)
                {
                    //await Task.Delay(TimeoutDelay);
                    throw new HttpRequestException("The download timed out after failure offset", null, HttpStatusCode.GatewayTimeout);
                }
                throw new HttpRequestException("The download failed after failure offset", null, HttpStatusCode.ServiceUnavailable);
            }
            validCount = _failureOffset - Position;
        }

        await Task.Delay(_delayTime);
        Position += validCount;
        return (int)validCount;
    }
}