using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Downloader;

/// <summary>
/// Represents the configuration settings for a download operation.
/// </summary>
public class DownloadConfiguration : ICloneable, INotifyPropertyChanged
{
    private int _activeChunks = 1; // number of active chunks
    private int _bufferBlockSize = 1024; // usually, hosts support max to 8000 bytes
    private int _chunkCount = 1; // file parts to download
    private long _maximumBytesPerSecond = ThrottledStream.Infinite; // No-limitation in download speed
    private int _maximumTryAgainOnFailover = int.MaxValue; // the maximum number of times to fail.
    private long _maximumMemoryBufferBytes;
    private bool _checkDiskSizeBeforeDownload = true; // check disk size for temp and file path
    private bool _parallelDownload; // download parts of file as parallel or not
    private int _parallelCount; // number of parallel downloads
    private int _timeout = 1000; // timeout (millisecond) per stream block reader
    private bool _rangeDownload; // enable ranged download
    private long _rangeLow; // starting byte offset
    private long _rangeHigh; // ending byte offset

    private bool
        _clearPackageOnCompletionWithFailure; // Clear package and downloaded data when download completed with failure

    private long _minimumSizeOfChunking = 512; // minimum size of chunking to download a file in multiple parts

    private bool
        _reserveStorageSpaceBeforeStartingDownload; // Before starting the download, reserve the storage space of the file as file size.

    private bool
        _enableLiveStreaming; // Get on demand downloaded data with ReceivedBytes on downloadProgressChanged event 

    /// <summary>
    /// To bind view models to fire changes in MVVM pattern
    /// </summary>
    public event PropertyChangedEventHandler PropertyChanged = delegate { };
    
    private void OnPropertyChanged<T>(ref T field, T newValue, [CallerMemberName] string name = null)
    {
        if (!field.Equals(newValue))
        {
            field = newValue;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    /// <summary>
    /// Gets the number of active chunks.
    /// </summary>
    public int ActiveChunks
    {
        get => _activeChunks;
        internal set => OnPropertyChanged(ref _activeChunks, value);
    }

    /// <summary>
    /// Gets or sets the stream buffer size which is used for the size of blocks.
    /// </summary>
    public int BufferBlockSize
    {
        get => (int)Math.Min(MaximumSpeedPerChunk, _bufferBlockSize);
        set => OnPropertyChanged(ref _bufferBlockSize, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether to check the disk available size for the download file before starting the download.
    /// </summary>
    public bool CheckDiskSizeBeforeDownload
    {
        get => _checkDiskSizeBeforeDownload;
        set => OnPropertyChanged(ref _checkDiskSizeBeforeDownload, value);
    }

    /// <summary>
    /// Gets or sets the file chunking parts count.
    /// </summary>
    public int ChunkCount
    {
        get => _chunkCount;
        set => OnPropertyChanged(ref _chunkCount, Math.Max(1, value));
    }

    /// <summary>
    /// Gets or sets the maximum bytes per second that can be transferred through the base stream.
    /// </summary>
    public long MaximumBytesPerSecond
    {
        get => _maximumBytesPerSecond;
        set => OnPropertyChanged(ref _maximumBytesPerSecond, value <= 0 ? long.MaxValue : value);
    }

    /// <summary>
    /// Gets the maximum bytes per second that can be transferred through the base stream at each chunk downloader.
    /// This property is read-only.
    /// </summary>
    public long MaximumSpeedPerChunk => ParallelDownload
        ? MaximumBytesPerSecond / Math.Max(Math.Min(Math.Min(ChunkCount, ParallelCount), ActiveChunks), 1)
        : MaximumBytesPerSecond;

    /// <summary>
    /// Gets or sets the maximum number of times to try again to download on failure.
    /// </summary>
    public int MaxTryAgainOnFailover
    {
        get => _maximumTryAgainOnFailover;
        set => OnPropertyChanged(ref _maximumTryAgainOnFailover, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether to download file chunks in parallel or serially.
    /// </summary>
    public bool ParallelDownload
    {
        get => _parallelDownload;
        set => OnPropertyChanged(ref _parallelDownload, value);
    }

    /// <summary>
    /// Gets or sets the count of chunks to download in parallel.
    /// If ParallelCount is less than or equal to 0, then ParallelCount is equal to ChunkCount.
    /// </summary>
    public int ParallelCount
    {
        get => _parallelCount <= 0 ? ChunkCount : _parallelCount;
        set => OnPropertyChanged(ref _parallelCount, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether to download a range of bytes.
    /// </summary>
    public bool RangeDownload
    {
        get => _rangeDownload;
        set => OnPropertyChanged(ref _rangeDownload, value);
    }

    /// <summary>
    /// Gets or sets the starting byte offset for ranged download.
    /// </summary>
    public long RangeLow
    {
        get => _rangeLow;
        set => OnPropertyChanged(ref _rangeLow, value);
    }

    /// <summary>
    /// Gets or sets the ending byte offset for ranged download.
    /// </summary>
    public long RangeHigh
    {
        get => _rangeHigh;
        set => OnPropertyChanged(ref _rangeHigh, value);
    }

    /// <summary>
    /// Gets or sets the custom body of your requests.
    /// </summary>
    public RequestConfiguration RequestConfiguration { get; set; } = new(); // default requests configuration

    /// <summary>
    /// Gets or sets the download timeout per stream file blocks.
    /// </summary>
    public int Timeout
    {
        get => _timeout;
        set => OnPropertyChanged(ref _timeout, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether to clear the package and downloaded data when the download completes with failure.
    /// </summary>
    public bool ClearPackageOnCompletionWithFailure
    {
        get => _clearPackageOnCompletionWithFailure;
        set => OnPropertyChanged(ref _clearPackageOnCompletionWithFailure, value);
    }

    /// <summary>
    /// Gets or sets the minimum size of chunking and multiple part downloading.
    /// </summary>
    public long MinimumSizeOfChunking
    {
        get => _minimumSizeOfChunking;
        set => OnPropertyChanged(ref _minimumSizeOfChunking, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether to reserve the storage space of the file as file size before starting the download.
    /// Default value is false.
    /// </summary>
    public bool ReserveStorageSpaceBeforeStartingDownload
    {
        get => _reserveStorageSpaceBeforeStartingDownload;
        set => OnPropertyChanged(ref _reserveStorageSpaceBeforeStartingDownload, value);
    }

    /// <summary>
    /// Gets or sets the maximum amount of memory, in bytes, that the Downloader library is allowed
    /// to allocate for buffering downloaded content. Once this limit is reached, the library will
    /// stop downloading and start writing the buffered data to a file stream before continuing.
    /// The default value for is 0, which indicates unlimited buffering.
    /// </summary>
    /// <example>
    /// The following example sets the maximum memory buffer to 50 MB, causing the library to release
    /// the memory buffer after each 50 MB of downloaded content:
    /// <code>
    /// MaximumMemoryBufferBytes = 1024 * 1024 * 50
    /// </code>
    /// </example>
    public long MaximumMemoryBufferBytes
    {
        get => _maximumMemoryBufferBytes;
        set => OnPropertyChanged(ref _maximumMemoryBufferBytes, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether live-streaming is enabled or not. If it's enabled, get the on-demand downloaded data
    /// with ReceivedBytes on the downloadProgressChanged event.
    /// Note: This option may consume more memory because it copies each block of downloaded data into ReceivedBytes.
    /// </summary>
    public bool EnableLiveStreaming
    {
        get => _enableLiveStreaming;
        set => OnPropertyChanged(ref _enableLiveStreaming, value);
    }

    /// <summary>
    /// Creates a shallow copy of the current object.
    /// </summary>
    /// <returns>A shallow copy of the current object.</returns>
    public object Clone()
    {
        return MemberwiseClone();
    }
}