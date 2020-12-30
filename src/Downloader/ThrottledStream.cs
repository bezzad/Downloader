using System;
using System.IO;
using System.Threading;
using System.Timers;

namespace Downloader
{
    /// <summary>
    /// Stream that limits the maximal bandwith. 
    /// If the internal counter exceeds the MaxBytePerSecond-Value in under 1s the AutoResetEvent blocks the stream until the second finally elapsed
    /// </summary>
    public class ThrottledStream : Stream
    {
        private int _processed;
        readonly System.Timers.Timer _resetTimer;
        private readonly AutoResetEvent _wh = new AutoResetEvent(true);
        private readonly Stream _baseStream;
        private int _bandwidthLimit;
        public const int Infinite = int.MaxValue;
        private int RefractiveIndexOfTime = 10; 
        private const int OnSecond = 1000; // ms

        /// <summary>
        /// Bandwith Limit (in B/s)
        /// </summary>
        public int BandwidthLimit
        {
            get { return _bandwidthLimit * RefractiveIndexOfTime; }
            set
            {
                if (value < 0)
                    throw new ArgumentException("BandwidthLimit has to be greater than 0");

                _bandwidthLimit = (value == 0 ? Infinite : value) / RefractiveIndexOfTime;
            }
        }

        /// <summary>
        /// Creates a new Stream with Data bandwith cap
        /// </summary>
        /// <param name="baseStreamStream"></param>
        /// <param name="maxBytesPerSecond"></param>
        public ThrottledStream(Stream baseStreamStream, int maxBytesPerSecond = Infinite)
        {

            BandwidthLimit = maxBytesPerSecond;
            _baseStream = baseStreamStream;
            _processed = 0;
            _resetTimer = new System.Timers.Timer { Interval = OnSecond / (double)RefractiveIndexOfTime };
            _resetTimer.Elapsed += resetTimerElapsed;
            _resetTimer.Start();
        }

        private void resetTimerElapsed(object sender, ElapsedEventArgs e)
        {
            _processed = 0;
            _wh.Set();
        }

        /// <inheritdoc />
        public override void Close()
        {
            _resetTimer.Stop();
            _resetTimer.Close();
            base.Close();
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            _resetTimer.Dispose();
            base.Dispose(disposing);
        }

        /// <inheritdoc />
        public override bool CanRead
        {
            get { return _baseStream.CanRead; }
        }

        /// <inheritdoc />
        public override bool CanSeek
        {
            get { return _baseStream.CanSeek; }
        }

        /// <inheritdoc />
        public override bool CanWrite
        {
            get { return _baseStream.CanWrite; }
        }

        /// <inheritdoc />
        public override void Flush()
        {
            _baseStream.Flush();
        }

        /// <inheritdoc />
        public override long Length
        {
            get { return _baseStream.Length; }
        }

        /// <inheritdoc />
        public override long Position
        {
            get
            {
                return _baseStream.Position;
            }
            set
            {
                _baseStream.Position = value;
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
        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = 0;

            // everything fits into this cycle
            if (_processed + count < _bandwidthLimit)
            {
                _processed += count;
                return _baseStream.Read(buffer, offset, count);
            }

            // nothing fits into this cycle, but 1 cycle would be enough 
            if (_processed == _bandwidthLimit && count < _bandwidthLimit)
            {
                _wh.WaitOne();
                _processed += count;
                return _baseStream.Read(buffer, offset, count);
            }

            // everything would fit into 1 cycle, but the current cycle has not enough space so 2 cycles overlap
            if (count < _bandwidthLimit && _processed + count > _bandwidthLimit)
            {
                int first = _bandwidthLimit - _processed;
                int second = count - first;
                read = _baseStream.Read(buffer, offset, first);
                _wh.WaitOne();
                read += _baseStream.Read(buffer, offset + read, second);
                _processed += second;
                return read;
            }

            // many cycles are needed (processed ignored in the first, would cause more problems than use)
            if (count > _bandwidthLimit)
            {
                int current = 0;
                for (int i = 0; i < count; i += current)
                {
                    current = Math.Min(count - i, _bandwidthLimit);
                    read += _baseStream.Read(buffer, offset + i, current);
                    if (current == _bandwidthLimit)
                        _wh.WaitOne();
                }

                _processed += current;
                return read;
            }

            return 0;
        }

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count)
        {
            int current;
            for (int i = 0; i < count; i += current)
            {
                current = Math.Min(count - i, _bandwidthLimit);
                _baseStream.Write(buffer, offset + i, current);
                if (current == _bandwidthLimit)
                    _wh.WaitOne();
            }
        }
    }
}