using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader
{
    internal class ChunkDownloader
    {
        private readonly DownloadConfiguration _configuration;
        private readonly int _timeoutIncrement = 10;
        private ThrottledStream _sourceStream;
        private ConcurrentStream _storage;
        internal Chunk Chunk { get; set; }

        public event EventHandler<DownloadProgressChangedEventArgs> DownloadProgressChanged;

        public ChunkDownloader(Chunk chunk, DownloadConfiguration config, ConcurrentStream storage)
        {
            Chunk = chunk;
            _configuration = config;
            _storage = storage;
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
                await DownloadChunk(downloadRequest, pause, cancelToken).ConfigureAwait(false);
                return Chunk;
            }
            catch (TaskCanceledException) // when stream reader timeout occurred 
            {
                return await ContinueWithDelay(downloadRequest, pause, cancelToken).ConfigureAwait(false);
            }
            catch (ObjectDisposedException) // when stream reader cancel/timeout occurred 
            {
                return await ContinueWithDelay(downloadRequest, pause, cancelToken).ConfigureAwait(false);
            }
            catch (WebException) when (Chunk.CanTryAgainOnFailover())
            {
                return await ContinueWithDelay(downloadRequest, pause, cancelToken).ConfigureAwait(false);
            }
            catch (Exception error) when (Chunk.CanTryAgainOnFailover() &&
                                          (error.HasSource("System.Net.Http") ||
                                           error.HasSource("System.Net.Sockets") ||
                                           error.HasSource("System.Net.Security") ||
                                           error.InnerException is SocketException))
            {
                return await ContinueWithDelay(downloadRequest, pause, cancelToken).ConfigureAwait(false);
            }
            finally
            {
                await Task.Yield();
            }
        }

        private async Task<Chunk> ContinueWithDelay(Request request, PauseToken pause, CancellationToken cancelToken)
        {
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
                using (cancelToken.Register(stream.Close))
                {
                    while (readSize > 0)
                    {
                        cancelToken.ThrowIfCancellationRequested();
                        await pauseToken.WaitWhilePausedAsync().ConfigureAwait(false);
                        byte[] buffer = new byte[_configuration.BufferBlockSize];
                        using var innerCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken);
                        innerCts.CancelAfter(Chunk.Timeout);
                        innerToken = innerCts.Token;
                        using (innerToken?.Register(stream.Close))
                        {
                            // if innerToken timeout occurs, close the stream just during the reading stream
                            readSize = await stream.ReadAsync(buffer, 0, buffer.Length, innerToken.Value).ConfigureAwait(false);
                        }

                        _storage.WriteAsync(Chunk.Start + Chunk.Position, buffer, readSize);
                        Chunk.Position += readSize;

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
                cancelToken.ThrowIfCancellationRequested();
                if (innerToken?.IsCancellationRequested == true)
                    throw new TaskCanceledException("The ReadAsync function has timed out", exp);

                throw; // throw origin stack trace of exception 
            }
        }

        private void OnDownloadProgressChanged(DownloadProgressChangedEventArgs e)
        {
            DownloadProgressChanged?.Invoke(this, e);
        }
    }
}