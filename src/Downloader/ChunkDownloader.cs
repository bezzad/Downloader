using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader
{
    internal class ChunkDownloader
    {
        private const int TimeoutIncrement = 10;
        private ThrottledStream sourceStream;
        public event EventHandler<DownloadProgressChangedEventArgs> DownloadProgressChanged;
        public DownloadConfiguration Configuration { get; protected set; }
        public Chunk Chunk { get; protected set; }

        public ChunkDownloader(Chunk chunk, DownloadConfiguration config)
        {
            Chunk = chunk;
            Configuration = config;
            Configuration.PropertyChanged += ConfigurationPropertyChanged;
        }

        private void ConfigurationPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Configuration.MaximumBytesPerSecond) &&
                sourceStream?.CanRead == true)
            {
                sourceStream.BandwidthLimit = Configuration.MaximumSpeedPerChunk;
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
            Chunk.Timeout += TimeoutIncrement;
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
                    Configuration.RequestConfiguration.CookieContainer = request.CookieContainer;
                    using Stream responseStream = downloadResponse?.GetResponseStream();
                    if (responseStream != null)
                    {
                        using (sourceStream = new ThrottledStream(responseStream, Configuration.MaximumSpeedPerChunk))
                        {
                            await ReadStream(sourceStream, pauseToken, cancelToken).ConfigureAwait(false);
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
                (Configuration.ChunkCount > 1 || Chunk.Position > 0 || Configuration.RangeDownload))
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

            if (Chunk.Storage is null)
                Chunk.Refresh();

            try
            {
                // close stream on cancellation because, it's not work on .Net Framework
                using (cancelToken.Register(stream.Close))
                {
                    while (readSize > 0)
                    {
                        cancelToken.ThrowIfCancellationRequested();
                        await pauseToken.WaitWhilePausedAsync().ConfigureAwait(false);
                        byte[] buffer = new byte[Configuration.BufferBlockSize];
                        using var innerCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken);
                        innerCts.CancelAfter(Chunk.Timeout);
                        innerToken = innerCts.Token;
                        using (innerToken?.Register(stream.Close))
                        {
                            // if innerToken timeout occurs, close the stream just during the reading stream
                            readSize = await stream.ReadAsync(buffer, 0, buffer.Length, innerToken.Value).ConfigureAwait(false);
                        }

                        await ChangeStreamIfMappingStreamOverflowed(readSize);
                        await Chunk.Storage.WriteAsync(buffer, 0, readSize, cancelToken).ConfigureAwait(false);
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
            finally
            {
                Chunk.Storage.Flush();
            }
        }

        private async Task<bool> ChangeStreamIfMappingStreamOverflowed(int readSize)
        {
            if (CheckStorageOverflowed(readSize) && Chunk.Storage is MemoryMappedViewStream)
            {
                Debugger.Break();
                await Chunk.Storage.FlushAsync();
                Chunk.Storage.Dispose();
                Chunk.Storage = Chunk.StorageProvider(Chunk.Start + Chunk.Position, readSize);
                return true;
            }

            return false;
        }

        private bool CheckStorageOverflowed(int readSize)
        {
            return readSize + Chunk.Storage.Position > Chunk.Length && Chunk.End > Chunk.Start;
        }

        private void OnDownloadProgressChanged(DownloadProgressChangedEventArgs e)
        {
            DownloadProgressChanged?.Invoke(this, e);
        }
    }
}