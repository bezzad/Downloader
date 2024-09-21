using System;

namespace Downloader;

internal class Packet(long position, byte[] data, int len) : IDisposable, ISizeableObject
{
    public Memory<byte> Data { get; set; } = data.AsMemory(0, len);
    public int Length { get; } = len;
    public long Position { get; set; } = position;
    public long EndOffset => Position + Length;

    public void Dispose()
    {
        Data = null;
        Position = 0;
    }
}