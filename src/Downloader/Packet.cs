using System;
using System.Buffers;
using System.Threading.Tasks;

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
    public bool IsRented { get; }

    /// <summary>
    /// True for downloaded file bytes, false for anything else (such as resume metadata).
    /// Only data packets count as download progress.
    /// </summary>
    public bool IsData { get; init; }

    /// <summary>
    /// A copy of the bytes for live streaming, or empty.
    /// </summary>
    public Memory<byte> LiveData { get; init; }

    /// <summary>
    /// Set only for a flush marker. The writer completes it after every packet queued before it is
    /// written and the base stream is flushed. A marker that is dropped is canceled.
    /// </summary>
    public TaskCompletionSource Flushed { get; private init; }

    /// <summary>
    /// A packet with no bytes that asks the writer to flush. It does not count as data or buffer size.
    /// </summary>
    public static Packet FlushMarker() => new(0, [], 0, false) {
        Flushed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
    };

    public Packet(long position, byte[] data, int length, bool isRented = true)
    {
        rentedData = data;
        Length = length;
        Position = position;
        EndOffset = position + length;
        IsRented = isRented;
    }

    public void Dispose()
    {
        Flushed?.TrySetCanceled(); // no effect when the flush is already done
        if (rentedData != null)
        {
            if (IsRented)
            {
                ArrayPool<byte>.Shared.Return(rentedData);
            }
            rentedData = null;
        }
    }
}