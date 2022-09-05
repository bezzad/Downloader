using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace Downloader
{
    public class DownloadConfiguration : ICloneable, INotifyPropertyChanged
    {
        private int _bufferBlockSize;
        private int _chunkCount;
        private long _maximumBytesPerSecond;
        private bool _checkDiskSizeBeforeDownload;
        private int _maxTryAgainOnFailover;
        private bool _onTheFlyDownload;
        private bool _parallelDownload;
        private int _parallelCount;
        private string _tempDirectory;
        private string _tempFilesExtension = ".dsc";
        private int _timeout;
        private bool _rangeDownload;
        private long _rangeLow;
        private long _rangeHigh;
        private bool _clearPackageOnCompletionWithFailure;

        public event PropertyChangedEventHandler PropertyChanged = delegate { };

        public DownloadConfiguration()
        {
            MaxTryAgainOnFailover = int.MaxValue; // the maximum number of times to fail.
            ParallelDownload = false; // download parts of file as parallel or not
            ParallelCount = 0; // number of parallel downloads
            ChunkCount = 1; // file parts to download
            Timeout = 1000; // timeout (millisecond) per stream block reader
            OnTheFlyDownload = true; // caching in-memory mode
            BufferBlockSize = 1024; // usually, hosts support max to 8000 bytes
            MaximumBytesPerSecond = ThrottledStream.Infinite; // No-limitation in download speed
            RequestConfiguration = new RequestConfiguration(); // default requests configuration
            TempDirectory = Path.GetTempPath(); // default chunks path
            CheckDiskSizeBeforeDownload = true; // check disk size for temp and file path
            RangeDownload = false; // enable ranged download
            RangeLow = 0; // starting byte offset
            RangeHigh = 0; // ending byte offset
            ClearPackageOnCompletionWithFailure = true; // clear package or not when download completed with failure
        }

        /// <summary>
        /// Create the OnPropertyChanged method to raise the event
        /// The calling member's name will be used as the parameter.
        /// </summary>
        /// <param name="name">changed property name</param>
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        /// <summary>
        /// Stream buffer size which is used for size of blocks
        /// </summary>
        public int BufferBlockSize
        {
            get => (int)Math.Min(MaximumSpeedPerChunk, _bufferBlockSize);
            set
            {
                _bufferBlockSize = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Check disk available size for download file before starting the download.
        /// </summary>
        public bool CheckDiskSizeBeforeDownload
        {
            get => _checkDiskSizeBeforeDownload;
            set
            {
                _checkDiskSizeBeforeDownload=value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// File chunking parts count
        /// </summary>
        public int ChunkCount
        {
            get => _chunkCount;
            set
            {
                _chunkCount = Math.Max(1, value);
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// The maximum bytes per second that can be transferred through the base stream.
        /// </summary>
        public long MaximumBytesPerSecond
        {
            get => _maximumBytesPerSecond;
            set
            {
                _maximumBytesPerSecond = value <= 0 ? long.MaxValue : value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// The maximum bytes per second that can be transferred through the base stream at each chunk downloader.
        /// This Property is ReadOnly.
        /// </summary>
        public long MaximumSpeedPerChunk => ParallelDownload ? MaximumBytesPerSecond / ChunkCount : MaximumBytesPerSecond;

        /// <summary>
        /// How many time try again to download on failed
        /// </summary>
        public int MaxTryAgainOnFailover
        {
            get => _maxTryAgainOnFailover;
            set
            {
                _maxTryAgainOnFailover=value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// download file without caching chunks in disk. In the other words,
        /// all chunks stored in memory.
        /// </summary>
        public bool OnTheFlyDownload
        {
            get => _onTheFlyDownload;
            set
            {
                _onTheFlyDownload=value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Download file chunks as Parallel or Serial?
        /// </summary>
        public bool ParallelDownload
        {
            get => _parallelDownload;
            set
            {
                _parallelDownload=value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Count of chunks to download in parallel.
        ///
        /// If ParallelCount is &lt;=0, then ParallelCount is equal to ChunkCount.
        /// </summary>
        public int ParallelCount
        {
            get => _parallelCount <= 0 ? ChunkCount : _parallelCount;
            set
            {
                _parallelCount = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Download a range of byte
        /// </summary>
        public bool RangeDownload
        {
            get => _rangeDownload;
            set
            {
                _rangeDownload=value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// The starting byte offset for ranged download
        /// </summary>
        public long RangeLow
        {
            get => _rangeLow;
            set
            {
                _rangeLow=value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// The ending byte offset for ranged download
        /// </summary>
        public long RangeHigh
        {
            get => _rangeHigh;
            set
            {
                _rangeHigh=value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Custom body of your requests
        /// </summary>
        public RequestConfiguration RequestConfiguration { get; set; }

        /// <summary>
        /// Chunk files storage path when the OnTheFlyDownload is false.
        /// </summary>
        public string TempDirectory
        {
            get => _tempDirectory;
            set
            {
                _tempDirectory=value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Chunk files extension, the default value is ".dsc" which is the acronym of "Downloader Service Chunks" file
        /// </summary>
        public string TempFilesExtension
        {
            get => _tempFilesExtension;
            set
            {
                _tempFilesExtension=value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Download timeout per stream file blocks
        /// </summary>
        public int Timeout
        {
            get => _timeout;
            set
            {
                _timeout=value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Clear package when download completed with failure
        /// </summary>
        public bool ClearPackageOnCompletionWithFailure
        {
            get => _clearPackageOnCompletionWithFailure;
            set
            {
                _clearPackageOnCompletionWithFailure=value;
                OnPropertyChanged();
            }
        }

        public object Clone()
        {
            return MemberwiseClone();
        }
    }
}