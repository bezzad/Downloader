using System;

namespace Downloader
{
    internal class Packet : IDisposable
    {
        public byte[] Data;
        public long Position;
        public int Length;
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
