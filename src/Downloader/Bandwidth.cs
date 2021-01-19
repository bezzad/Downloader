using System;

namespace Downloader
{
    public class Bandwidth
    {
        private const int OneSecond = 1000; // millisecond
        private long _count;
        private int _lastTickCountCheckpoint;
        public long Speed { get; private set; }
        public long AverageSpeed { get; private set; }

        public Bandwidth()
        {
            Reset();
        }

        public void CalculateSpeed(long bytesReceived)
        {
            int elapsedTime = Environment.TickCount - _lastTickCountCheckpoint + 1;
            Speed = bytesReceived * OneSecond / elapsedTime; // bytes per second
            AverageSpeed = ((AverageSpeed * _count) + Speed) / (_count + 1);
            _lastTickCountCheckpoint = Environment.TickCount;
            _count++;
        }

        public void Reset()
        {
            _lastTickCountCheckpoint = Environment.TickCount;
            _count = 0;
            Speed = 0;
            AverageSpeed = 0;
        }
    }
}
