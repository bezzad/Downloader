using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace Downloader.DummyHttpServer;

public enum DummyDataType
{
    Random,
    Order,
    Single
}

[ExcludeFromCodeCoverage]
public class DummyLazyStream : Stream
{
    private readonly Random _random;
    private readonly DummyDataType _type;
    private long _length;

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => _length;
    public override long Position { get; set; }
    public byte? SingleByte { get; set; }

    public DummyLazyStream(DummyDataType type, long size, byte? singleByte = null)
    {
        if (size < 1)
            throw new ArgumentException("size has to be > 0");

        _random = new Random(DateTime.Now.GetHashCode());
        _type = type;
        _length = size;
        SingleByte = singleByte;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int i = 0;
        for (; i < count && Position < Length; i++, Position++)
        {
            buffer[i + offset] = (byte)ReadByte();
        }

        return i;
    }

    public override int ReadByte()
    {
        return _type switch {
            DummyDataType.Random => _random.Next(0, 255),
            DummyDataType.Order => (byte)(Position % 256),
            DummyDataType.Single => SingleByte.HasValue ? SingleByte.Value : 0,
            _ => 0,
        };
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        Position = offset;
        return Position;
    }

    public override void SetLength(long value)
    {
        _length = value;
    }

    public override void Flush()
    {
        throw new System.NotImplementedException("This is a readonly stream");
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new System.NotImplementedException("This is a readonly stream");
    }

    public byte[] ToArray()
    {
        Seek(0, SeekOrigin.Begin);
        byte[] result = new byte[Length];
        Read(result, 0, (int)Length);
        return result;
    }
}
