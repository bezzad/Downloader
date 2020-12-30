using System;
using System.IO;
using System.Threading;

namespace Downloader
{
    /// <summary>
    ///     Class for streaming data with throttling support.
    /// </summary>
    public class ThrottledStream : Stream
    {
        private int _bandwidthLimit;
        private readonly Stream _baseStream;
        public const int Infinite = int.MaxValue;
        private long _lastTransferredBytesCount;
        private int _lastStartTime;
        private const double OneSecond = 1000;

        /// <summary>
        ///     Initializes a new instance of the <see cref="T:ThrottledStream" /> class.
        /// </summary>
        /// <param name="baseStream">The base stream.</param>
        /// <param name="maximumBytesPerSecond">The maximum bytes per second that can be transferred through the base stream.</param>
        /// <exception cref="ArgumentNullException">Thrown when <see cref="baseStream" /> is a null reference.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <see cref="maximumBytesPerSecond" /> is a negative value.</exception>
        public ThrottledStream(Stream baseStream, int maximumBytesPerSecond = Infinite)
        {
            if (maximumBytesPerSecond < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumBytesPerSecond),
                    maximumBytesPerSecond, "The maximum number of bytes per second can't be negative.");
            }

            _baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
            BandwidthLimit = maximumBytesPerSecond;
            _lastStartTime = Environment.TickCount;
            _lastTransferredBytesCount = 0;
        }

        /// <summary>
        ///     Bandwith Limit (in B/s)
        /// </summary>
        /// <value>The maximum bytes per second.</value>
        public int BandwidthLimit
        {
            get => _bandwidthLimit;
            set
            {
                if (value < 0)
                    throw new ArgumentException("BandwidthLimit has to be greater than 0");

                _bandwidthLimit = value == 0 ? Infinite : value;
                Reset();
            }
        }

        /// <inheritdoc />
        public override bool CanRead => _baseStream.CanRead;

        /// <inheritdoc />
        public override bool CanSeek => _baseStream.CanSeek;

        /// <inheritdoc />
        public override bool CanWrite => _baseStream.CanWrite;

        /// <inheritdoc />
        public override long Length => _baseStream.Length;

        /// <inheritdoc />
        public override long Position
        {
            get => _baseStream.Position;
            set => _baseStream.Position = value;
        }

        /// <summary>
        ///     Will reset the byte-count to 0 and reset the start time to the current time.
        /// </summary>
        private void Reset()
        {
            long difference = Environment.TickCount - _lastStartTime;

            // Only reset counters when a known history is available of more then 1 second.
            if (difference > OneSecond)
            {
                _lastTransferredBytesCount = 0;
                _lastStartTime = Environment.TickCount;
            }
        }

        /// <inheritdoc />
        public override void Flush()
        {
            _baseStream.Flush();
        }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            Throttle(count);

            return _baseStream.Read(buffer, offset, count);
        }

        private void Throttle(int bufferSizeInBytes)
        {
            // Make sure the buffer isn't empty.
            if (_bandwidthLimit <= 0 || bufferSizeInBytes <= 0)
            {
                return;
            }

            _lastTransferredBytesCount += bufferSizeInBytes;
            int elapsedMilliseconds = Environment.TickCount - _lastStartTime;

            // Calculate the current bytesPerSecond.
            int bytesPerSecond = (int)Math.Ceiling(_lastTransferredBytesCount * OneSecond / elapsedMilliseconds);

            // If the bytesPerSecond are more than the maximum bytesPerSecond, try to wait.
            if (bytesPerSecond >= _bandwidthLimit)
            {
                // Calculate the time to sleep.
                double expectedTime = _lastTransferredBytesCount * OneSecond / _bandwidthLimit;
                int sleepTime = (int)Math.Ceiling(expectedTime - elapsedMilliseconds) + 1;
                Reset();

                try
                {
                    // The time to sleep is more then a millisecond, so sleep.
                    Thread.Sleep(sleepTime);
                }
                catch (ThreadAbortException)
                {
                    // ignore ThreadAbortException.
                }
            }
        }

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin)
        {
            return _baseStream.Seek(offset, origin);
        }

        /// <inheritdoc />
        public override void SetLength(long value)
        {
            _baseStream.SetLength(value);
        }

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count)
        {
            Throttle(count);
            _baseStream.Write(buffer, offset, count);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return _baseStream.ToString();
        }
    }
}