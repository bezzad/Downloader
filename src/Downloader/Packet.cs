using System;

namespace Downloader
{
    internal class Packet : IDisposable, IIndexable
    {
        public byte[] Data { get; set; }
        public long Position { get; set; }
        public int Length { get; set; }
        public long NextPosition => Position + Length;

        public Packet(long position, byte[] data, int length)
        {
            Data = data;
            Position = position;
            Length = length;
        }

        public void Dispose()
        {
            Data = null;
            Position = 0;
            Length = 0;
        }
    }
}
