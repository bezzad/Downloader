using System;

namespace Downloader;

internal class Packet(long position, byte[] data, int len) : IDisposable, ISizeableObject
{
    public byte[] Data { get; set; } = data;
    public int Length { get; set; } = len;
    public long Position { get; set; } = position;
    public long EndOffset => Position + Length;

    public void Dispose()
    {
        Data = null;
        Position = 0;
    }
}