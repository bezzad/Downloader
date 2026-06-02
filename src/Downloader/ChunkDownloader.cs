using Downloader.Exceptions;
using Downloader.Extensions;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.ComponentModel;
using System.Net.Http.Headers;

namespace Downloader;

internal class ChunkDownloader
{
    private readonly ILogger _logger;
    private readonly DownloadConfiguration _configuration;
    private readonly int _timeoutIncrement = 10;
    private const int MaxBackoffMs = 10_000; // upper bound for a single retry delay
    private ThrottledStream _sourceStream;
    private readonly ConcurrentStream _storage;
    private readonly SocketClient _client;
    internal Chunk Chunk { get; set; }
    public event EventHandler<DownloadProgressChangedEventArgs> DownloadProgressChanged;

    public ChunkDownloader(Chunk chunk, DownloadConfiguration config, ConcurrentStream storage, SocketClient client,
        ILogger logger = null)
    {
        Chunk = chunk;
        _configuration = config;
        _storage = storage;
        _client = client;
        _logger = logger;
        _configuration.PropertyChanged += ConfigurationPropertyChanged;
    }

    private void ConfigurationPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        _logger?.LogDebug($"Changed configuration {e.PropertyName} property");
        if (e.PropertyName is nameof(_configuration.MaximumBytesPerSecond) or nameof(_configuration.ActiveChunks) &&
            _sourceStream?.CanRead == true)
        {
            _sourceStream.BandwidthLimit = _configuration.MaximumSpeedPerChunk;
        }
    }

    public async ValueTask<Chunk> Download(Request downloadRequest, PauseToken pause, CancellationToken cancelToken)
    {
        try
        {
            _logger?.LogDebug("Starting download the chunk {ChunkId}.", Chunk.Id);
            await DownloadChunk(downloadRequest, pause, cancelToken).ConfigureAwait(false);

            // issue #231: the server may end the response stream before the whole chunk has been
            // received (premature EOF / dropped connection) without raising a transport error.
            // ReadStream returns normally in that case, so without this guard the partial chunk
            // would be silently accepted as complete — leaving an unfinished .download file with
            // no error and no retry. Treat it like a retryable failure instead.
            if (!cancelToken.IsCancellationRequested && IsChunkIncomplete())
            {
                throw new IncompleteDownloadException(
                    $"The download of chunk {Chunk.Id} ended prematurely: received {Chunk.Position} of {Chunk.Length} bytes.");
            }

            return Chunk;
        }
        catch (Exception error)
        {
            if (!cancelToken.IsCancellationRequested &&
                (error.IsMomentumError() || error is IncompleteDownloadException) &&
                Chunk.CanTryAgainOnFailure())
            {
                _logger?.LogError(error, "Error on download chunk {ChunkId}. Retry ...", Chunk.Id);
                return await ContinueWithDelay(error, downloadRequest, pause, cancelToken).ConfigureAwait(false);
            }

            // Can't handle this exception
            _logger?.LogError(error, "Fatal error on download chunk {ChunkId}.", Chunk.Id);
            throw;
        }
    }

    /// <summary>
    /// Determines whether a chunk with a known length stopped before all of its bytes were
    /// received. Open-ended chunks (unknown size, <see cref="Chunk.Length"/> == 0) are never
    /// considered incomplete because their end is defined by the server closing the stream.
    /// </summary>
    private bool IsChunkIncomplete() => Chunk.Length > 0 && Chunk.Position < Chunk.Length;

    private async ValueTask<Chunk> ContinueWithDelay(Exception exception, Request request, PauseToken pause, CancellationToken cancelToken)
    {
        if (cancelToken.IsCancellationRequested)
            return Chunk;

        _logger?.LogDebug("ContinueWithDelay of the chunk {ChunkId}", Chunk.Id);
        if (await _client.IsSupportDownloadInRange(request, cancelToken).ConfigureAwait(false))
        {
            // Exponential backoff with full jitter (issue #226). When a server throttles many
            // parallel chunks at once (e.g. HTTP 428/429/503), retrying them all after the same
            // fixed delay just recreates the burst. Spreading retries over an exponentially
            // growing, randomized window lets the herd disperse so the chunks get through.
            TimeSpan delay = GetBackoffDelay();
            _logger?.LogDebug("Backing off {DelayMs}ms before retry #{Attempt} of chunk {ChunkId}",
                delay.TotalMilliseconds, Chunk.FailureCount, Chunk.Id);
            await Task.Delay(delay, cancelToken).ConfigureAwait(false);
            // Increasing reading timeout to reduce stress and conflicts
            Chunk.Timeout += _timeoutIncrement;
            // re-request and continue downloading
            return await Download(request, pause, cancelToken).ConfigureAwait(false);
        }
        throw exception;
    }

    /// <summary>
    /// Computes the retry delay using exponential backoff with full jitter, based on the configured
    /// <see cref="DownloadConfiguration.BlockTimeout"/> and the chunk's current failure count. The
    /// delay is a uniformly random value in <c>[0, min(MaxBackoffMs, BlockTimeout * 2^(attempt-1))]</c>.
    /// </summary>
    private TimeSpan GetBackoffDelay()
    {
        // FailureCount was already incremented by CanTryAgainOnFailure(), so it is 1 on the first retry.
        int attempt = Math.Max(1, Chunk.FailureCount);
        long baseDelayMs = Math.Max(1, _configuration.BlockTimeout);
        // baseDelayMs * 2^(attempt-1), guarded against overflow by the MaxBackoffMs cap.
        double window = baseDelayMs * Math.Pow(2, attempt - 1);
        int cappedWindowMs = (int)Math.Min(MaxBackoffMs, window);
        // Full jitter: a uniformly random point within [0, cappedWindowMs].
        return TimeSpan.FromMilliseconds(Random.Shared.Next(cappedWindowMs + 1));
    }

    private async ValueTask DownloadChunk(Request request, PauseToken pauseToken, CancellationToken cancelToken)
    {
        if (cancelToken.IsCancellationRequested ||
            Chunk.IsDownloadCompleted())
            return;

        _logger?.LogDebug($"DownloadChunk of the chunk {Chunk.Id}");
        HttpRequestMessage requestMsg = request.GetRequest();
        SetRequestRange(requestMsg);
        using HttpResponseMessage responseMsg =
            await _client.SendRequestAsync(requestMsg, cancelToken).ConfigureAwait(false);

        _logger?.LogDebug("Downloading the chunk {ChunkId} with response status code: {ResponseMsgStatusCode}", Chunk.Id, responseMsg.StatusCode);

        await using Stream responseStream =
            await responseMsg.Content.ReadAsStreamAsync(cancelToken).ConfigureAwait(false);

        _sourceStream = new ThrottledStream(responseStream, _configuration.MaximumSpeedPerChunk);
        await ReadStream(_sourceStream, pauseToken, cancelToken).ConfigureAwait(false);
        await _sourceStream.DisposeAsync();
    }

    private void SetRequestRange(HttpRequestMessage request)
    {
        long startOffset = Chunk.Start + Chunk.Position;

        // has limited range
        if (Chunk.End > 0 && startOffset < Chunk.End &&
            (_configuration.ChunkCount > 1 || Chunk.Position > 0 || _configuration.RangeDownload))
        {
            request.Headers.Range = new RangeHeaderValue(startOffset, Chunk.End);
        }
    }

    internal async Task ReadStream(Stream stream, PauseToken pauseToken, CancellationToken cancelToken)
    {
        int readSize = 1;
        CancellationToken? innerToken = null;

        try
        {
            // close stream on cancellation because, it doesn't work on .NetFramework
            await using CancellationTokenRegistration _ = cancelToken.Register(stream.Close);
            while (readSize > 0 && Chunk.CanWrite)
            {
                cancelToken.ThrowIfCancellationRequested();
                await pauseToken.WaitWhilePausedAsync().ConfigureAwait(false);
                byte[] buffer = ArrayPool<byte>.Shared.Rent(_configuration.BufferBlockSize);
                try
                {
                    using CancellationTokenSource innerCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken);
                    innerToken = innerCts.Token;
                    innerCts.CancelAfter(Chunk.Timeout);
                    await using (innerToken.Value.Register(stream.Close))
                    {
                        readSize = await stream.ReadAsync(buffer, innerToken.Value).ConfigureAwait(false);
                        _logger?.LogDebug("Read {ReadSize}bytes of the chunk {ChunkId} stream", readSize, Chunk.Id);
                    }

                    readSize = (int)Math.Min(Chunk.EmptyLength, readSize);
                    if (readSize > 0)
                    {
                        // Capture live streaming data before transferring buffer ownership to Packet
                        Memory<byte> liveStreamData = _configuration.EnableLiveStreaming
                            ? buffer.AsSpan(0, readSize).ToArray().AsMemory() // create a copy of the data to avoid being overwritten when buffer is returned to pool
                            : default;

                        await _storage.WriteAsync(Chunk.Start + Chunk.Position - _configuration.RangeLow, buffer, readSize, true)
                            .ConfigureAwait(false);

                        buffer = null; // ownership transferred to Packet; will be returned to pool after disk write
                        _logger?.LogDebug("Write {ReadSize}bytes in the chunk {ChunkId}", readSize, Chunk.Id);
                        Chunk.Position += readSize;
                        _logger?.LogDebug("The chunk {ChunkId} current position is: {ChunkPosition} of {ChunkLength}", Chunk.Id, Chunk.Position, Chunk.Length);

                        OnDownloadProgressChanged(new DownloadProgressChangedEventArgs(Chunk.Id) {
                            TotalBytesToReceive = Chunk.Length,
                            ReceivedBytesSize = Chunk.Position,
                            ProgressedByteSize = readSize,
                            ReceivedBytes = liveStreamData
                        });
                    }
                }
                finally
                {
                    // Return buffer to pool if ownership was NOT transferred to Packet
                    if (buffer != null)
                        ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }
        catch (ObjectDisposedException exp) // When closing stream manually, ObjectDisposedException will be thrown
        {
            _logger?.LogError(exp,
                "ReadAsync of the chunk {ChunkId} stream was canceled or closed forcibly from server", Chunk.Id);
            cancelToken.ThrowIfCancellationRequested();
            if (innerToken?.IsCancellationRequested == true)
            {
                _logger?.LogError(exp, "ReadAsync of the chunk {ChunkId} stream has been timed out", Chunk.Id);
                throw new TaskCanceledException($"ReadAsync of the chunk {Chunk.Id} stream has been timed out", exp);
            }

            throw; // throw origin stack trace of exception 
        }

        _logger?.LogDebug("ReadStream of the chunk {ChunkId} completed successfully", Chunk.Id);
    }

    private void OnDownloadProgressChanged(DownloadProgressChangedEventArgs e)
    {
        DownloadProgressChanged?.Invoke(this, e);
    }
}