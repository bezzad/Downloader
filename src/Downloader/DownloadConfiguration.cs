using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Downloader;

/// <summary>
/// Represents the configuration settings for a download operation.
/// </summary>
public class DownloadConfiguration : ICloneable, INotifyPropertyChanged
{
    /// <summary>
    /// To bind view models to fire changes in MVVM pattern
    /// </summary>
    public event PropertyChangedEventHandler PropertyChanged = delegate { };

    /// <summary>
    /// Notify every change of configuration properties
    /// </summary>
    private void OnPropertyChanged<T>(ref T field, T newValue, [CallerMemberName] string name = null)
    {
        if (field.Equals(newValue))
            return;

        field = newValue;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// Gets the number of active chunks.
    /// </summary>
    public int ActiveChunks
    {
        get;
        internal set => OnPropertyChanged(ref field, value);
    } = 1;

    /// <summary>
    /// Gets or sets the stream buffer size which is used for the size of blocks.
    /// </summary>
    public int BufferBlockSize
    {
        get => (int)Math.Min(MaximumSpeedPerChunk, field);
        set
        {
            if (value is < 1 or > 1048576) // 1MB = 1024 * 1024 bytes
                throw new ArgumentOutOfRangeException(nameof(BufferBlockSize),
                    "Buffer block size must be between 1 byte and 1024KB");
            OnPropertyChanged(ref field, value);
        }
    } = 1024;

    /// <summary>
    /// Gets or sets a value indicating whether to check the disk available size for the download file before starting the download.
    /// </summary>
    public bool CheckDiskSizeBeforeDownload
    {
        get;
        set => OnPropertyChanged(ref field, value);
    } = true;

    /// <summary>
    /// Gets or sets the file chunking parts count.
    /// </summary>
    public int ChunkCount
    {
        get;
        set
        {
            if (value < 1)
                throw new ArgumentOutOfRangeException(nameof(ChunkCount), "Chunk count must be greater than 0");
            OnPropertyChanged(ref field, Math.Max(1, value));
        }
    } = 1;

    /// <summary>
    /// Gets or sets the maximum bytes per second that can be transferred through the base stream.
    /// </summary>
    public long MaximumBytesPerSecond
    {
        get;
        set
        {
            if (value < 0) value = 0;
            OnPropertyChanged(ref field, value <= 0 ? long.MaxValue : value);
        }
    } = ThrottledStream.Infinite;

    /// <summary>
    /// Gets the maximum bytes per second that can be transferred through the base stream at each chunk downloader.
    /// This property is read-only.
    /// </summary>
    public long MaximumSpeedPerChunk => MaximumBytesPerSecond /
                                        Math.Max(Math.Min(Math.Min(ChunkCount, ParallelCount), ActiveChunks), 1);

    /// <summary>
    /// Gets or sets the maximum number of times to try again to download on failure.
    /// </summary>
    public int MaxTryAgainOnFailure
    {
        get;
        set => OnPropertyChanged(ref field, value);
    } = int.MaxValue;

    /// <summary>
    /// Gets or sets a value indicating whether to download file chunks in parallel or serially.
    /// </summary>
    public bool ParallelDownload
    {
        get;
        set => OnPropertyChanged(ref field, value);
    }

    /// <summary>
    /// Gets or sets the count of chunks to download in parallel.
    /// If ParallelCount is less than or equal to 0, then ParallelCount is equal to ChunkCount.
    /// </summary>
    public int ParallelCount
    {
        get => field <= 0 ? ChunkCount : field;
        set => OnPropertyChanged(ref field, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether to download a range of bytes.
    /// </summary>
    public bool RangeDownload
    {
        get;
        set => OnPropertyChanged(ref field, value);
    }

    /// <summary>
    /// Gets or sets the starting byte offset for ranged download.
    /// </summary>
    public long RangeLow
    {
        get;
        set => OnPropertyChanged(ref field, value);
    }

    /// <summary>
    /// Gets or sets the ending byte offset for ranged download.
    /// </summary>
    public long RangeHigh
    {
        get;
        set
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(RangeHigh),
                    "Range high cannot be negative");
            OnPropertyChanged(ref field, value);
        }
    }

    /// <summary>
    /// Gets or sets the custom body of your requests.
    /// </summary>
    public RequestConfiguration RequestConfiguration { get; set; } = new(); // default requests configuration

    /// <summary>
    /// Gets or sets the download timeout per stream file blocks.
    /// </summary>
    public int BlockTimeout
    {
        get;
        set
        {
            if (value < 100)
                throw new ArgumentOutOfRangeException(nameof(BlockTimeout),
                    "Timeout must be at least 100 milliseconds");
            OnPropertyChanged(ref field, value);
        }
    } = 1000;

    /// <summary>
    /// Gets or sets the timeout for the HTTPClient in Milliseconds
    /// </summary>
    public int HttpClientTimeout
    {
        get;
        set
        {
            if (value < 1000)
                throw new ArgumentOutOfRangeException(nameof(HttpClientTimeout),
                    "Timeout must be at least 1000 milliseconds");
            OnPropertyChanged(ref field, value);
        }
    } = 100 * 1000;

    /// <summary>
    /// Gets or sets a value indicating whether to clear the package and downloaded data when the download completes with failure.
    /// </summary>
    public bool ClearPackageOnCompletionWithFailure
    {
        get;
        set => OnPropertyChanged(ref field, value);
    }

    /// <summary>
    /// Gets or sets the minimum size of file to enable chunking and multiple part downloading.
    /// </summary>
    public long MinimumSizeOfChunking
    {
        get;
        set => OnPropertyChanged(ref field, value);
    } = 512;

    /// <summary>
    /// Gets or sets the minimum size of a single chunk
    /// If it is not 0 it dynamically reduces the chunk count to keep the chunk size above this value
    /// Keeps ChunkCount as a Maximum
    /// Default value is 0
    /// </summary>
    public long MinimumChunkSize
    {
        get;
        set => OnPropertyChanged(ref field, value);
    }

    /// <summary>
    /// Gets or sets the maximum amount of memory, in bytes, that the Downloader library is allowed
    /// to allocate for buffering downloaded content. Once this limit is reached, the library will
    /// stop downloading and start writing the buffered data to a file stream before continuing.
    /// The default value is 0, which indicates unlimited buffering.
    /// 
    /// This setting is particularly useful when:
    /// 1. Downloading large files on systems with limited memory
    /// 2. Preventing out-of-memory exceptions during parallel downloads
    /// 3. Optimizing memory usage for long-running download operations
    /// 
    /// Recommended values:
    /// - For systems with 4GB RAM: 256MB (268435456 bytes)
    /// - For systems with 8GB RAM: 512MB (536870912 bytes)
    /// - For systems with 16GB RAM: 1GB (1073741824 bytes)
    /// 
    /// Note: Setting this value too low may impact download performance due to frequent disk I/O operations.
    /// </summary>
    /// <example>
    /// The following example sets the maximum memory buffer to 50 MB:
    /// <code>
    /// MaximumMemoryBufferBytes = 1024 * 1024 * 50
    /// </code>
    /// </example>
    public long MaximumMemoryBufferBytes
    {
        get;
        set
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(MaximumMemoryBufferBytes),
                    "Maximum memory buffer bytes cannot be negative");
            OnPropertyChanged(ref field, value);
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether live-streaming is enabled or not. If it's enabled, get the on-demand downloaded data
    /// with ReceivedBytes on the downloadProgressChanged event.
    /// 
    /// Important considerations:
    /// 1. Enabling this option will increase memory usage as each downloaded block is copied to the ReceivedBytes buffer
    /// 2. The memory impact is proportional to the BufferBlockSize and download speed
    /// 3. For large files, consider setting MaximumMemoryBufferBytes to limit memory usage
    /// 4. This feature is best used when you need to process downloaded data in real-time
    /// 
    /// Default value is false.
    /// </summary>
    public bool EnableLiveStreaming
    {
        get;
        set => OnPropertyChanged(ref field, value);
    }

    /// <summary>
    /// Determine what to do when initializing a download session.
    /// <seealso cref="FileExistPolicy"/>
    /// Default value is Delete. (Preserving older version behavior)
    /// </summary>
    public FileExistPolicy FileExistPolicy { get; set; } = FileExistPolicy.Delete;

    /// <summary>
    /// Resume download from previews position if the file downloaded before this and file continuable
    /// </summary>
    public bool EnableResumeDownload { get; set; } = false;
    
    /// <summary>
    /// The extension of inprogress downloading file. Default value is "download"
    /// </summary>
    public string DownloadFileExtension
    {
        get => field;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("DownloadFileExtension cannot be empty");

            OnPropertyChanged(ref field, '.' + value.Trim('.').Trim(',').Trim(' ').ToLower().Replace(" ", "").ToLower());
        }
    } = ".download";

    [Obsolete("This option has no affect on downloading and all downloads pre-allocate space before start. Unless, the file hasn't length header from server-side.")]
    public bool ReserveStorageSpaceBeforeStartingDownload { get; set; } = true;
    
    /// <summary>
    /// Creates a shallow copy of the current object.
    /// </summary>
    /// <returns>A shallow copy of the current object.</returns>
    public object Clone()
    {
        return MemberwiseClone();
    }
}