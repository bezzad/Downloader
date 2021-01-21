using System;
using System.Threading;

namespace Downloader
{
    public class Bandwidth
    {
        private const double OneSecond = 1000; // millisecond
        private long _count;
        private int _lastSecondCheckpoint;
        private long _lastTransferredBytesCount;
        private int _speedRetrieveTime;
        public double Speed { get; private set; }
        public double AverageSpeed { get; private set; }
        public long BandwidthLimit { get; set; }

        public Bandwidth()
        {
            BandwidthLimit = long.MaxValue;
            Reset();
        }

        public void CalculateSpeed(long bytesReceived)
        {
            int elapsedTime = Environment.TickCount - _lastSecondCheckpoint + 1;
            Interlocked.Add(ref _lastTransferredBytesCount, bytesReceived);
            double momentSpeed = _lastTransferredBytesCount * OneSecond / elapsedTime; // B/s
            CalculateSpeedRetrieveTime(BandwidthLimit, momentSpeed, elapsedTime);

            if (OneSecond <= elapsedTime)
            {
                Speed = momentSpeed;
                AverageSpeed = ((AverageSpeed * _count) + Speed) / (_count + 1);
                _count++;
                SecondCheckpoint();
            }
        }

        private void CalculateSpeedRetrieveTime(long bandwidthLimit, double momentSpeed, int elapsedTime)
        {
            if (momentSpeed >= bandwidthLimit)
            {
                int expectedTime = (int)(_lastTransferredBytesCount * OneSecond / bandwidthLimit);
                Interlocked.Add(ref _speedRetrieveTime, expectedTime - elapsedTime);
                if (_speedRetrieveTime > 0)
                {
                    SecondCheckpoint();
                }
            }
        }

        public int PopSpeedRetrieveTime()
        {
            int speedRetrieveTime = _speedRetrieveTime;
            Interlocked.Exchange(ref _speedRetrieveTime, 0);
            return speedRetrieveTime;
        }

        public void Reset()
        {
            SecondCheckpoint();
            _count = 0;
            Speed = 0;
            AverageSpeed = 0;
        }

        private void SecondCheckpoint()
        {
            Interlocked.Exchange(ref _lastSecondCheckpoint, Environment.TickCount);
            Interlocked.Exchange(ref _lastTransferredBytesCount, 0);
        }
    }
}
