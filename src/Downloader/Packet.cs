using System;

namespace Downloader;

internal class Packet : IDisposable, ISizeableObject
{
    public volatile bool IsDisposed = false;
    public byte[] Data { get; set; }
    public int Length { get; set; }
    public long Position { get; set; }
    public long EndOffset => Position + Length;

    public Packet(long position, byte[] data, int len)
    {
        Position = position;
        Data = data;
        Length = len;
    }

    public void Dispose()
    {
        IsDisposed = true;
        Data = null;
        Position = 0;
    }
}