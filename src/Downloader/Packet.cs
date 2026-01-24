using System;

namespace Downloader;

internal class Packet(long position, byte[] data, int length) : IDisposable, ISizeableObject
{
    /// <summary>
    /// Exposes only the valid data without copying or slicing.
    /// </summary>
    public Memory<byte> Data = data.AsMemory(0, length);
    public int Length { get; } = length;
    public readonly long Position = position;
    public readonly long EndOffset = position + length;

    public void Dispose()
    {
        Data = null;
    }
}