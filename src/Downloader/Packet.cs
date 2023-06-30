using System;
using System.Linq;

namespace Downloader;

internal class Packet : IDisposable, IComparable<Packet>
{
    public byte[] Data { get; set; }
    public int Length => Data?.Length ?? 0;
    public long Position { get; set; }
    public long EndOffset => Position + Length;

    public Packet(long position, byte[] data, int len)
    {
        Data = data.Length > len ? data.Take(len).ToArray() : data;
        Position = position;
    }

    public void Merge(Packet other)
    {
        Data = Data.Concat(other.Data).ToArray();
    }

    public void Dispose()
    {
        Data = null;
        Position = 0;
    }

    public int CompareTo(Packet other)
    {
        return Position > other.Position ? 1
            : Position == other.Position ? 0 : -1;
    }
}
