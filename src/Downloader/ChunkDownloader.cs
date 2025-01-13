using Downloader.Extensions.Helpers;
using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader;

internal class ChunkDownloader
{
    private readonly ILogger _logger;
    private readonly DownloadConfiguration _configuration;
    private readonly int _timeoutIncrement = 10;
    private ThrottledStream _sourceStream;
    private readonly ConcurrentStream _storage;
    internal Chunk Chunk { get; set; }
    public event EventHandler<DownloadProgressChangedEventArgs> DownloadProgressChanged;

    public ChunkDownloader(Chunk chunk, DownloadConfiguration config, ConcurrentStream storage, ILogger logger = null)
    {
        Chunk = chunk;
        _configuration = config;
        _storage = storage;
        _logger = logger;
        _configuration.PropertyChanged += ConfigurationPropertyChanged;
    }

    private void ConfigurationPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        _logger?.LogDebug($"Changed configuration {e.PropertyName} property");
        if (e.PropertyName is nameof(_configuration.MaximumBytesPerSecond) or nameof(_configuration.ActiveChunks)  &&
            _sourceStream?.CanRead == true)
        {
            _sourceStream.BandwidthLimit = _configuration.MaximumSpeedPerChunk;
        }
    }

    public async Task<Chunk> Download(Request downloadRequest, PauseToken pause, CancellationToken cancelToken)
    {
        try
        {
            _logger?.LogDebug($"Starting download the chunk {Chunk.Id}");
            await DownloadChunk(downloadRequest, pause, cancelToken).ConfigureAwait(false);
            return Chunk;
        }
        catch (TaskCanceledException error) // when stream reader timeout occurred 
        {
            _logger?.LogError(error, $"Task Canceled on download chunk {Chunk.Id} with retry");
            return await ContinueWithDelay(downloadRequest, pause, cancelToken).ConfigureAwait(false);
        }
        catch (ObjectDisposedException error) // when stream reader cancel/timeout occurred 
        {
            _logger?.LogError(error, $"Disposed object error on download chunk {Chunk.Id} with retry");
            return await ContinueWithDelay(downloadRequest, pause, cancelToken).ConfigureAwait(false);
        }
        catch (Exception error) when (Chunk.CanTryAgainOnFailover() && error.IsMomentumError())
        {
            _logger?.LogError(error, $"Error on download chunk {Chunk.Id} with retry");
            return await ContinueWithDelay(downloadRequest, pause, cancelToken).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            // Can't handle this exception
            _logger?.LogCritical(error, $"Fatal error on download chunk {Chunk.Id}");
            throw;
        }
        finally
        {
            _logger?.LogDebug($"Exit from download method of the chunk {Chunk.Id}");
            await Task.Yield();
        }
    }

    private async Task<Chunk> ContinueWithDelay(Request request, PauseToken pause, CancellationToken cancelToken)
    {
        _logger?.LogDebug($"ContinueWithDelay of the chunk {Chunk.Id}");
        await request.ThrowIfIsNotSupportDownloadInRange().ConfigureAwait(false);
        await Task.Delay(Chunk.Timeout, cancelToken).ConfigureAwait(false);
        // Increasing reading timeout to reduce stress and conflicts
        Chunk.Timeout += _timeoutIncrement;
        // re-request and continue downloading
        return await Download(request, pause, cancelToken).ConfigureAwait(false);
    }

    private async Task DownloadChunk(Request downloadRequest, PauseToken pauseToken, CancellationToken cancelToken)
    {
        cancelToken.ThrowIfCancellationRequested();
        _logger?.LogDebug($"DownloadChunk of the chunk {Chunk.Id}");
        if (Chunk.IsDownloadCompleted() == false)
        {
            HttpWebRequest request = downloadRequest.GetRequest();
            SetRequestRange(request);
            using HttpWebResponse downloadResponse = request.GetResponse() as HttpWebResponse;
            if (downloadResponse?.StatusCode == HttpStatusCode.OK ||
                downloadResponse?.StatusCode == HttpStatusCode.PartialContent ||
                downloadResponse?.StatusCode == HttpStatusCode.Created ||
                downloadResponse?.StatusCode == HttpStatusCode.Accepted ||
                downloadResponse?.StatusCode == HttpStatusCode.ResetContent)
            {
                _logger?.LogDebug(
                    $"DownloadChunk of the chunk {Chunk.Id} with response status: {downloadResponse.StatusCode}");
                _configuration.RequestConfiguration.CookieContainer = request.CookieContainer;
                await using Stream responseStream = downloadResponse.GetResponseStream();
                await using (_sourceStream = new ThrottledStream(responseStream, _configuration.MaximumSpeedPerChunk))
                {
                    await ReadStream(_sourceStream, pauseToken, cancelToken).ConfigureAwait(false);
                }
            }
            else
            {
                throw new WebException($"Download response status of the chunk {Chunk.Id} was " +
                                       $"{downloadResponse?.StatusCode}: " + downloadResponse?.StatusDescription);
            }
        }
    }

    private void SetRequestRange(HttpWebRequest request)
    {
        long startOffset = Chunk.Start + Chunk.Position;

        // has limited range
        if (Chunk.End > 0 &&
            (_configuration.ChunkCount > 1 || Chunk.Position > 0 || _configuration.RangeDownload))
        {
            if (startOffset < Chunk.End)
                request.AddRange(startOffset, Chunk.End);
            else
                request.AddRange(startOffset);
        }
    }

    internal async Task ReadStream(Stream stream, PauseToken pauseToken, CancellationToken cancelToken)
    {
        int readSize = 1;
        CancellationToken? innerToken = null;

        try
        {
            // close stream on cancellation because, it doesn't work on .Net Framework
            await using CancellationTokenRegistration _ = cancelToken.Register(stream.Close);
            while (readSize > 0 && Chunk.CanWrite)
            {
                cancelToken.ThrowIfCancellationRequested();
                await pauseToken.WaitWhilePausedAsync().ConfigureAwait(false);
                byte[] buffer = new byte[_configuration.BufferBlockSize];
                using var innerCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken);
                innerToken = innerCts.Token;
                innerCts.CancelAfter(Chunk.Timeout);
                await using (innerToken.Value.Register(stream.Close))
                {
                    // if innerToken timeout occurs, close the stream just during the reading stream
                    readSize = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), innerToken.Value).ConfigureAwait(false);
                    _logger?.LogDebug($"Read {readSize}bytes of the chunk {Chunk.Id} stream");
                }

                readSize = (int)Math.Min(Chunk.EmptyLength, readSize);
                if (readSize > 0)
                {
                    await _storage.WriteAsync(Chunk.Start + Chunk.Position - _configuration.RangeLow, buffer, readSize)
                        .ConfigureAwait(false);
                    _logger?.LogDebug($"Write {readSize}bytes in the chunk {Chunk.Id}");
                    Chunk.Position += readSize;
                    _logger?.LogDebug($"The chunk {Chunk.Id} current position is: {Chunk.Position} of {Chunk.Length}");

                    OnDownloadProgressChanged(new DownloadProgressChangedEventArgs(Chunk.Id) {
                        TotalBytesToReceive = Chunk.Length,
                        ReceivedBytesSize = Chunk.Position,
                        ProgressedByteSize = readSize,
                        ReceivedBytes = _configuration.EnableLiveStreaming
                            ? buffer.Take(readSize).ToArray()
                            : []
                    });
                }
            }
        }
        catch (ObjectDisposedException exp) // When closing stream manually, ObjectDisposedException will be thrown
        {
            _logger?.LogError(exp,
                $"ReadAsync of the chunk {Chunk.Id} stream was canceled or closed forcibly from server");
            cancelToken.ThrowIfCancellationRequested();
            if (innerToken?.IsCancellationRequested == true)
            {
                _logger?.LogError(exp, $"ReadAsync of the chunk {Chunk.Id} stream has been timed out");
                throw new TaskCanceledException($"ReadAsync of the chunk {Chunk.Id} stream has been timed out", exp);
            }

            throw; // throw origin stack trace of exception 
        }

        _logger?.LogDebug($"ReadStream of the chunk {Chunk.Id} completed successfully");
    }

    private void OnDownloadProgressChanged(DownloadProgressChangedEventArgs e)
    {
        DownloadProgressChanged?.Invoke(this, e);
    }
}