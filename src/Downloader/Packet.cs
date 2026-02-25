using System;
using System.Buffers;

namespace Downloader;

internal class Packet : IDisposable, ISizeableObject
{
    private byte[] rentedData;

    /// <summary>
    /// Exposes only the valid data without copying or slicing.
    /// </summary>
    public Memory<byte> Data => rentedData.AsMemory(0, Length);
    public int Length { get; }
    public long Position { get; }
    public long EndOffset { get; }

    public Packet(long position, byte[] data, int length)
    {
        rentedData = data;
        Length = length;
        Position = position;
        EndOffset = position + length;
    }

    public void Dispose()
    {
        if (rentedData != null)
        {
            try
            {
                ArrayPool<byte>.Shared.Return(rentedData);
            }
            catch (ArgumentException)
            {
                // Buffer was not rented from ArrayPool — let GC collect it
            }
            rentedData = null;
        }
    }
}