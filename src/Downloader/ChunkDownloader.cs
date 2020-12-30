using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader
{
    public abstract class ChunkDownloader
    {
        private const int TimeoutIncrement = 10;

        protected ChunkDownloader(Chunk chunk, int blockSize)
        {
            Chunk = chunk;
            BufferBlockSize = blockSize;
        }

        protected Chunk Chunk { get; }
        protected int BufferBlockSize { get; }
        public event EventHandler<DownloadProgressChangedEventArgs> DownloadProgressChanged;

        public async Task<Chunk> Download(Request downloadRequest, int maximumSpeed, CancellationToken token)
        {
            try
            {
                await DownloadChunk(downloadRequest, maximumSpeed, token);
                return Chunk;
            }
            catch (TaskCanceledException) // when stream reader timeout occurred 
            {
                // re-request and continue downloading...
                return await Download(downloadRequest, maximumSpeed, token);
            }
            catch (WebException) when (Chunk.CanTryAgainOnFailover())
            {
                // when the host forcibly closed the connection.
                await Task.Delay(Chunk.Timeout, token);
                // re-request and continue downloading...
                return await Download(downloadRequest, maximumSpeed, token);
            }
            catch (Exception error) when (Chunk.CanTryAgainOnFailover() &&
                                          (HasSource(error, "System.Net.Http") ||
                                           HasSource(error, "System.Net.Sockets") ||
                                           HasSource(error, "System.Net.Security") ||
                                           error.InnerException is SocketException))
            {
                Chunk.Timeout += TimeoutIncrement; // decrease download speed to down pressure on host
                await Task.Delay(Chunk.Timeout, token);
                // re-request and continue downloading...
                return await Download(downloadRequest, maximumSpeed, token);
            }
        }

        private async Task DownloadChunk(Request downloadRequest, int maximumSpeed, CancellationToken token)
        {
            if (token.IsCancellationRequested ||
                IsDownloadCompleted())
            {
                return;
            }

            if (IsValidPosition() == false)
            {
                Chunk.Position = 0;
            }

            HttpWebRequest request = downloadRequest.GetRequest();
            request.AddRange(Chunk.Start + Chunk.Position, Chunk.End);
            using HttpWebResponse downloadResponse = request.GetResponse() as HttpWebResponse;
            using Stream responseStream = downloadResponse?.GetResponseStream();

            if (responseStream != null)
            {
                using ThrottledStream destinationStream = new ThrottledStream(responseStream, maximumSpeed);
                await ReadStream(destinationStream, token);
            }
        }

        protected bool HasSource(Exception exp, string source)
        {
            Exception innerException = exp;
            while (innerException != null)
            {
                if (string.Equals(innerException.Source, source, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                innerException = innerException.InnerException;
            }

            return false;
        }

        protected virtual bool IsDownloadCompleted()
        {
            return Chunk.Start + Chunk.Position >= Chunk.End;
        }

        protected virtual bool IsValidPosition()
        {
            return Chunk.Position < Chunk.Length;
        }

        protected async Task ReadStream(Stream stream, CancellationToken token)
        {
            try
            {
                CreateChunkStorage();
                long bytesToReceiveCount = Chunk.Length - Chunk.Position;
                while (bytesToReceiveCount > 0)
                {
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    using CancellationTokenSource innerCts = new CancellationTokenSource(Chunk.Timeout);
                    int count = bytesToReceiveCount > BufferBlockSize
                        ? BufferBlockSize
                        : (int)bytesToReceiveCount;

                    byte[] buffer = new byte[count];
                    int readSize = await stream.ReadAsync(buffer, 0, count, innerCts.Token);
                    await WriteChunk(buffer, readSize);

                    Chunk.Position += readSize;
                    bytesToReceiveCount = Chunk.Length - Chunk.Position;

                    if (readSize <= 0) // stream ended
                        break;

                    OnDownloadProgressChanged(new DownloadProgressChangedEventArgs(Chunk.Id) {
                        TotalBytesToReceive = Chunk.Length,
                        BytesReceived = Chunk.Position,
                        ProgressedByteSize = readSize
                    });
                }
            }
            finally
            {
                OnCloseStream();
            }
        }

        protected abstract void CreateChunkStorage();
        protected abstract Task WriteChunk(byte[] data, int count);
        protected virtual void OnCloseStream() { }

        private void OnDownloadProgressChanged(DownloadProgressChangedEventArgs e)
        {
            DownloadProgressChanged?.Invoke(this, e);
        }
    }
}