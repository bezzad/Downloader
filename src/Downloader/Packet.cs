using System;

namespace Downloader
{
    internal struct Packet : IDisposable
    {
        public byte[] Data;
        public long Position;

        public Packet(long position, byte[] data)
        {
            Data = data;
            Position = position;
        }

        public void Dispose()
        {
            Data = null;
            Position = 0;
        }
    }
}
