using Downloader.Extensions.Logging;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader
{
    internal class ChunkDownloader
    {
        private readonly ILogger _logger;
        private readonly DownloadConfiguration _configuration;
        private readonly int _timeoutIncrement = 10;
        private ThrottledStream _sourceStream;
        private ConcurrentStream _storage;
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
            if (e.PropertyName == nameof(_configuration.MaximumBytesPerSecond) &&
                _sourceStream?.CanRead == true)
            {
                _sourceStream.BandwidthLimit = _configuration.MaximumSpeedPerChunk;
            }
        }

        public async Task<Chunk> Download(Request downloadRequest, PauseToken pause, CancellationToken cancelToken)
        {
            try
            {
                _logger?.Debug($"Starting download the chunk {Chunk.Id}");
                await DownloadChunk(downloadRequest, pause, cancelToken).ConfigureAwait(false);
                return Chunk;
            }
            catch (TaskCanceledException error) // when stream reader timeout occurred 
            {
                _logger?.Warning($"Task Canceled on download chunk {Chunk.Id} with retry", error);
                return await ContinueWithDelay(downloadRequest, pause, cancelToken).ConfigureAwait(false);
            }
            catch (ObjectDisposedException error) // when stream reader cancel/timeout occurred 
            {
                _logger?.Warning($"Disposed object error on download chunk {Chunk.Id} with retry", error);
                return await ContinueWithDelay(downloadRequest, pause, cancelToken).ConfigureAwait(false);
            }
            catch (Exception error) when (Chunk.CanTryAgainOnFailover() && error.IsMomentumError())
            {
                _logger?.Error($"Error on download chunk {Chunk.Id} with retry", error);
                return await ContinueWithDelay(downloadRequest, pause, cancelToken).ConfigureAwait(false);
            }
            catch (Exception error)
            {
                // Can't handle this exception
                _logger?.Fatal($"Fatal error on download chunk {Chunk.Id}", error);
                throw;
            }
            finally
            {
                _logger?.Debug($"Exit from download method of the chunk {Chunk.Id}");
                await Task.Yield();
            }
        }

        private async Task<Chunk> ContinueWithDelay(Request request, PauseToken pause, CancellationToken cancelToken)
        {
            _logger?.Debug($"ContinueWithDelay of the chunk {Chunk.Id}");
            await request.ThrowIfIsNotSupportDownloadInRange();
            await Task.Delay(Chunk.Timeout, cancelToken).ConfigureAwait(false);
            // Increasing reading timeout to reduce stress and conflicts
            Chunk.Timeout += _timeoutIncrement;
            // re-request and continue downloading
            return await Download(request, pause, cancelToken).ConfigureAwait(false);
        }

        private async Task DownloadChunk(Request downloadRequest, PauseToken pauseToken, CancellationToken cancelToken)
        {
            cancelToken.ThrowIfCancellationRequested();
            if (Chunk.IsDownloadCompleted() == false)
            {
                HttpWebRequest request = downloadRequest.GetRequest();
                SetRequestRange(request);
                using HttpWebResponse downloadResponse = request.GetResponse() as HttpWebResponse;
                if (downloadResponse.StatusCode == HttpStatusCode.OK ||
                    downloadResponse.StatusCode == HttpStatusCode.PartialContent ||
                    downloadResponse.StatusCode == HttpStatusCode.Created ||
                    downloadResponse.StatusCode == HttpStatusCode.Accepted ||
                    downloadResponse.StatusCode == HttpStatusCode.ResetContent)
                {
                    _configuration.RequestConfiguration.CookieContainer = request.CookieContainer;
                    using Stream responseStream = downloadResponse?.GetResponseStream();
                    if (responseStream != null)
                    {
                        using (_sourceStream = new ThrottledStream(responseStream, _configuration.MaximumSpeedPerChunk))
                        {
                            await ReadStream(_sourceStream, pauseToken, cancelToken).ConfigureAwait(false);
                        }
                    }
                }
                else
                {
                    _logger?.Error($"Throw WebException of the chunk {Chunk.Id}: Download response status was {downloadResponse.StatusCode}: {downloadResponse.StatusDescription}");
                    throw new WebException($"Download response status was {downloadResponse.StatusCode}: {downloadResponse.StatusDescription}");
                }
            }
        }

        private void SetRequestRange(HttpWebRequest request)
        {
            var startOffset = Chunk.Start + Chunk.Position;

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
                // close stream on cancellation because, it's not work on .Net Framework
                using var _ = cancelToken.Register(stream.Close);
                while (readSize > 0 && Chunk.CanWrite)
                {
                    cancelToken.ThrowIfCancellationRequested();
                    await pauseToken.WaitWhilePausedAsync().ConfigureAwait(false);
                    byte[] buffer = new byte[_configuration.BufferBlockSize];
                    using var innerCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken);
                    innerToken = innerCts.Token;
                    innerCts.CancelAfter(Chunk.Timeout);
                    using (innerToken.Value.Register(stream.Close))
                    {
                        // if innerToken timeout occurs, close the stream just during the reading stream
                        readSize = await stream.ReadAsync(buffer, 0, buffer.Length, innerToken.Value).ConfigureAwait(false);
                        _logger?.Debug($"Read {readSize}bytes of the chunk {Chunk.Id} stream");
                    }

                    readSize = (int)Math.Min(Chunk.EmptyLength, readSize);
                    if (readSize > 0)
                    {
                        await _storage.WriteAsync(Chunk.Start + Chunk.Position - _configuration.RangeLow, buffer, readSize).ConfigureAwait(false);
                        _logger?.Debug($"Write {readSize}bytes in the chunk {Chunk.Id}");
                        Chunk.Position += readSize;
                        _logger?.Debug($"The chunk {Chunk.Id} current position is: {Chunk.Position} of {Chunk.Length}");

                        OnDownloadProgressChanged(new DownloadProgressChangedEventArgs(Chunk.Id) {
                            TotalBytesToReceive = Chunk.Length,
                            ReceivedBytesSize = Chunk.Position,
                            ProgressedByteSize = readSize,
                            ReceivedBytes = buffer.Take(readSize).ToArray()
                        });
                    }
                }
            }
            catch (ObjectDisposedException exp) // When closing stream manually, ObjectDisposedException will be thrown
            {
                _logger?.Warning($"ReadAsync of the chunk {Chunk.Id} stream was canceled or closed forcibly from server", exp);
                cancelToken.ThrowIfCancellationRequested();
                if (innerToken?.IsCancellationRequested == true)
                {
                    _logger?.Warning($"ReadAsync of the chunk {Chunk.Id} stream has been timed out", exp);
                    throw new TaskCanceledException($"ReadAsync of the chunk {Chunk.Id} stream has been timed out", exp);
                }

                throw; // throw origin stack trace of exception 
            }

            _logger?.Debug($"ReadStream of the chunk {Chunk.Id} completed successfully");
        }

        private void OnDownloadProgressChanged(DownloadProgressChangedEventArgs e)
        {
            DownloadProgressChanged?.Invoke(this, e);
        }
    }
}