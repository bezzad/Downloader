using System;
using System.Threading;

namespace Downloader
{
    public class Bandwidth
    {
        private const double OneSecond = 1000; // millisecond
        private long _count;
        private int _lastTickCountCheckpoint;
        private long _lastTransferredBytesCount;
        public double Speed { get; private set; }
        public double AverageSpeed { get; private set; }

        public Bandwidth()
        {
            Reset();
        }

        public void CalculateSpeed(long bytesReceived)
        {
            int elapsedTime = Environment.TickCount - _lastTickCountCheckpoint + 1;
            Interlocked.Add(ref _lastTransferredBytesCount, bytesReceived);

            // perform moment speed limitation
            if (OneSecond <= elapsedTime)
            {
                Speed = _lastTransferredBytesCount * OneSecond / elapsedTime; // B/s
                AverageSpeed = ((AverageSpeed * _count) + Speed) / (_count + 1);
                _count++;
                Checkpoint();
            }
        }

        public void Reset()
        {
            Checkpoint();
            _count = 0;
            Speed = 0;
            AverageSpeed = 0;
        }

        private void Checkpoint()
        {
            Interlocked.Exchange(ref _lastTickCountCheckpoint, Environment.TickCount);
            Interlocked.Exchange(ref _lastTransferredBytesCount, 0);
        }
    }
}
