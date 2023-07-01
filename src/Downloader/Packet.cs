using System;

namespace Downloader;

internal class Packet : IDisposable
{
    public volatile bool IsDisposed = false;
    public readonly object SyncRoot = new object();
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

    public bool Merge(Packet other)
    {
        lock (SyncRoot)
        {
            if (IsDisposed)
                return false;

            // fast merge
            var combinedArray = new byte[Length + other.Length];
            Buffer.BlockCopy(Data, 0, combinedArray, 0, Length);
            Buffer.BlockCopy(other.Data, 0, combinedArray, Length, other.Length);

            Data = combinedArray;
            Length = combinedArray.Length;

            return true;
        }
    }

    public void Dispose()
    {
        IsDisposed = true;
        Data = null;
        Position = 0;
    }
}
