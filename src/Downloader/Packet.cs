using System;

namespace Downloader
{
    internal struct Packet : IDisposable
    {
        public byte[] Data;
        public long Position;
        public int Length;

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
