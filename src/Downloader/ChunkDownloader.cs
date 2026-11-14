using Downloader.Exceptions;
using Downloader.Extensions;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Net;
using System.ComponentModel;
using System.Net.Http.Headers;

namespace Downloader;

internal class ChunkDownloader : IDisposable
{
    private readonly ILogger _logger;
    private readonly Func<Chunk, bool> _chunkProgressed;
    private readonly DownloadConfiguration _configuration;
    private readonly DownloadPlan _plan;
    // Each retry widens the per-block read deadline so a borderline-slow/throttled server gets
    // progressively more headroom instead of tripping the same tight deadline every attempt. A flat
    // +10ms was effectively a no-op against a multi-second BlockTimeout; add a full BlockTimeout per
    // retry so attempt N tolerates roughly N× the original stall.
    private readonly int _timeoutIncrement;
    private const int MaxBackoffMs = 10_000; // upper bound for a single retry delay
    private const int MaxReadTimeoutMs = 60_000; // upper bound for the read timeout after retries
    private ThrottledStream _sourceStream;
    private readonly ConcurrentStream _storage;
    private readonly SocketClient _client;
    private readonly PauseToken _streamRoom;
    internal Chunk Chunk { get; set; }
    public event EventHandler<DownloadProgressChangedEventArgs> DownloadProgressChanged;

    public ChunkDownloader(Chunk chunk, DownloadConfiguration config, ConcurrentStream storage, SocketClient client,
        ILogger logger = null, Func<Chunk, bool> chunkProgressed = null, DownloadPlan plan = null,
        PauseToken streamRoom = null)
    {
        _streamRoom = streamRoom;
        Chunk = chunk;
        _configuration = config;
        _plan = plan ?? DownloadPlan.From(config);
        _storage = storage;
        _client = client;
        _logger = logger;
        _chunkProgressed = chunkProgressed;
        _timeoutIncrement = Math.Max(1000, config.BlockTimeout);
        _configuration.PropertyChanged += ConfigurationPropertyChanged;
    }

    /// <summary>
    /// Stops listening to the configuration, so a shared configuration does not keep this chunk alive.
    /// </summary>
    public void Dispose()
    {
        _configuration.PropertyChanged -= ConfigurationPropertyChanged;
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
        // A loop, not recursion: a long outage must not keep one more async frame per retry.
        while (true)
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
            catch (Exception error) when (!cancelToken.IsCancellationRequested &&
                                          (error.IsMomentumError() || error is IncompleteDownloadException) &&
                                          Chunk.CanTryAgainOnFailure())
            {
                _logger?.LogError(error, "Error on download chunk {ChunkId}. Retry ...", Chunk.Id);
                if (!await _client.IsSupportDownloadInRange(downloadRequest, cancelToken).ConfigureAwait(false))
                    throw;

                await WaitBeforeRetry(cancelToken).ConfigureAwait(false);
            }
            catch (Exception error)
            {
                // Can't handle this exception
                _logger?.LogError(error, "Fatal error on download chunk {ChunkId}.", Chunk.Id);
                throw;
            }
        }
    }

    /// <summary>
    /// Determines whether a chunk with a known length stopped before all of its bytes were
    /// received. Open-ended chunks (unknown size, <see cref="Chunk.Length"/> == 0) are never
    /// considered incomplete because their end is defined by the server closing the stream.
    /// </summary>
    private bool IsChunkIncomplete() => Chunk.Length > 0 && Chunk.Position < Chunk.Length;

    private async Task WaitBeforeRetry(CancellationToken cancelToken)
    {
        // Exponential backoff with full jitter (issue #226). When a server throttles many
        // parallel chunks at once (e.g. HTTP 428/429/503), retrying them all after the same
        // fixed delay just recreates the burst. Spreading retries over an exponentially
        // growing, randomized window lets the herd disperse so the chunks get through.
        TimeSpan delay = GetBackoffDelay();
        _logger?.LogDebug("Backing off {DelayMs}ms before retry #{Attempt} of chunk {ChunkId}",
            delay.TotalMilliseconds, Chunk.FailureCount, Chunk.Id);
        await Task.Delay(delay, cancelToken).ConfigureAwait(false);

        // A longer read timeout to reduce stress and conflicts, but never above the cap
        // (it used to grow without a limit and overflow to a negative value).
        int cap = Math.Max(_configuration.BlockTimeout, MaxReadTimeoutMs);
        Chunk.Timeout = (int)Math.Min((long)Chunk.Timeout + _timeoutIncrement, cap);
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
        if (requestMsg.Headers.Range?.Ranges.FirstOrDefault()?.From is long from)
            EnsureRangeAnswer(responseMsg, from);

        await using Stream responseStream =
            await responseMsg.Content.ReadAsStreamAsync(cancelToken).ConfigureAwait(false);

        _sourceStream = new ThrottledStream(responseStream, _configuration.MaximumSpeedPerChunk);
        await ReadStream(_sourceStream, pauseToken, cancelToken).ConfigureAwait(false);
        await _sourceStream.DisposeAsync();
    }

    /// <summary>
    /// A range request must get the asked part: <c>206</c> from the asked byte, or <c>200</c> with the
    /// whole file when the asked part starts at byte 0. Anything else would write wrong bytes.
    /// </summary>
    private static void EnsureRangeAnswer(HttpResponseMessage response, long from)
    {
        bool rightPart = response.StatusCode == HttpStatusCode.PartialContent &&
                         (response.Content.Headers.ContentRange?.From ?? from) == from;
        bool wholeFileFromStart = response.StatusCode == HttpStatusCode.OK && from == 0;
        if (rightPart || wholeFileFromStart)
            return;

        throw new InvalidRangeResponseException(
            $"The server did not send the asked range from byte {from} (status {(int)response.StatusCode}).");
    }

    private void SetRequestRange(HttpRequestMessage request)
    {
        long startOffset = Chunk.Start + Chunk.Position;

        // has limited range
        // Use <= (not <): when exactly one byte remains, startOffset == Chunk.End and that final
        // byte (index End) still must be range-requested. With <, the request fell through to a
        // full GET from offset 0 and wrote byte 0's value at the chunk's end offset — corrupting
        // the last byte of a resumed chunk.
        if (Chunk.End > 0 && startOffset <= Chunk.End &&
            (_plan.ChunkCount > 1 || Chunk.Position > 0 || _configuration.RangeDownload))
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
                // Live streaming: wait while the handler has too many blocks it did not take yet.
                if (_streamRoom is not null)
                    await _streamRoom.WaitWhilePausedAsync().WaitAsync(cancelToken).ConfigureAwait(false);
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
                        Memory<byte> liveStreamData = _plan.LiveStreaming
                            ? buffer.AsSpan(0, readSize).ToArray().AsMemory() // create a copy of the data to avoid being overwritten when buffer is returned to pool
                            : default;

                        // The storage owns the buffer now, also when the write throws: it returns it to the
                        // pool. Returning it here too gave two later reads the same array.
                        byte[] block = buffer;
                        buffer = null;
                        await _storage.WriteAsync(Chunk.Start + Chunk.Position - _plan.RangeLow, block, readSize, liveData: liveStreamData)
                            .ConfigureAwait(false);

                        _logger?.LogDebug("Write {ReadSize}bytes in the chunk {ChunkId}", readSize, Chunk.Id);
                        Chunk.AdvancePosition(readSize);
                        _logger?.LogDebug("The chunk {ChunkId} current position is: {ChunkPosition} of {ChunkLength}", Chunk.Id, Chunk.Position, Chunk.Length);

                        _chunkProgressed?.Invoke(Chunk);

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