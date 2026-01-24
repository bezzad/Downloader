using System;

namespace Downloader;

internal class Packet(long position, ReadOnlyMemory<byte> data, int len) : IDisposable, ISizeableObject
{
    /// <summary>
    /// Exposes only the valid slice without copying.
    /// Note: Please don't use `data[..len]` instead of this code, because it has a performance issue.
    /// </summary>
    public ReadOnlyMemory<byte> Data = data.Slice(0, len);

    /// <summary>
    /// Fast path for consumers that can work with spans.
    /// </summary>
    public ReadOnlySpan<byte> Span => Data.Span;
    
    public int Length { get; } = len;
    public readonly long Position = position;
    public readonly long EndOffset = position + len;

    public void Dispose()
    {
        Data = null;
    }
}