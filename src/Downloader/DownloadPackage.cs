using Downloader.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader;

public class DownloadPackage : IDisposable
{
    public bool IsSaving { get; set; }
    public bool IsSaveComplete { get; set; }
    public double SaveProgress { get; set; }
    public DownloadStatus Status { get; set; } = DownloadStatus.None;
    public string[] Urls { get; set; }
    public long TotalFileSize { get; set; }
    public string FileName { get; set; }
    public Chunk[] Chunks { get; set; }
    public long ReceivedBytesSize => Chunks?.Sum(chunk => chunk.Position) ?? 0;
    public bool IsSupportDownloadInRange { get; set; } = true;
    public bool InMemoryStream => string.IsNullOrWhiteSpace(FileName);
    public ConcurrentStream Storage { get; set; }

    public void Clear()
    {
        if (Chunks != null)
        {
            foreach (Chunk chunk in Chunks)
                chunk.Clear();
        }
        Chunks = null;
    }

    public async Task FlushAsync()
    {
        if (Storage?.CanWrite == true)
            await Storage.FlushAsync().ConfigureAwait(false);
    }

    [Obsolete("This method has been deprecated. Please use FlushAsync instead.")]
    public void Flush()
    {
        if (Storage?.CanWrite == true)
            Storage?.FlushAsync().Wait();
    }

    public void Validate()
    {
        foreach (var chunk in Chunks)
        {
            if (chunk.IsValidPosition() == false)
            {
                var realLength = Storage?.Length ?? 0;
                if (realLength <= chunk.Position)
                {
                    chunk.Clear();
                }
            }

            if (!IsSupportDownloadInRange)
                chunk.Clear();
        }
    }

    public void BuildStorage(bool reserveFileSize, long maxMemoryBufferBytes = 0, ILogger logger = null)
    {
        if (string.IsNullOrWhiteSpace(FileName))
            Storage = new ConcurrentStream(maxMemoryBufferBytes, logger);
        else
            Storage = new ConcurrentStream(FileName, reserveFileSize ? TotalFileSize : 0, maxMemoryBufferBytes, logger);
    }

    public void Dispose()
    {
        Clear();
        Storage?.Dispose();
    }
}