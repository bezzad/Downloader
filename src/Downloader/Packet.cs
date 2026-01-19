using System;

namespace Downloader;

internal class Packet(long position, ReadOnlyMemory<byte> data, int len) : IDisposable, ISizeableObject
{
    public ReadOnlyMemory<byte> Data { get; private set; } = data[..len];
    public int Length { get; } = len;
    public long Position { get; private set; } = position;
    public long EndOffset => Position + Length;

    public void Dispose()
    {
        Data = null;
        Position = 0;
    }
}