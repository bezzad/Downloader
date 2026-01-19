using System;

namespace Downloader;

internal class Packet(long position, ReadOnlyMemory<byte> data, int len) : IDisposable, ISizeableObject
{
    public ReadOnlyMemory<byte> Data { get; private set; } = data[..len];
    public int Length { get; } = len;
    public readonly long Position = position;
    public readonly long EndOffset = position + len;

    public void Dispose()
    {
        Data = null;
    }
}