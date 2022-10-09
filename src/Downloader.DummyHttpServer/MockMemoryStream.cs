using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader.DummyHttpServer
{
    public class MockMemoryStream : MemoryStream
    {
        private long _failureOffset = 0;
        private readonly TimeSpan _delayTime = new TimeSpan(1000);
        private readonly byte _value = 255;

        public MockMemoryStream(long size, long failureOffset = 0)
        {
            SetLength(size);
            _failureOffset = failureOffset;
            GC.Collect();
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            int validCount = (int)Math.Min(Math.Min(count, Length - Position), _failureOffset - Position);

            if (validCount == 0)
                throw new Exception("The download broke after failure offset");

            Array.Fill(buffer, _value, offset, validCount);
            await Task.Delay(_delayTime);
            Position += validCount;

            return validCount;
        }
    }
}
