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
        protected ChunkDownloader(Chunk chunk, int blockSize)
        {
            Chunk = chunk;
            BufferBlockSize = blockSize;
        }

        public Chunk Chunk { get; }
        protected int BufferBlockSize { get; set; }
        protected int TimeoutIncrement = 100;
        public event EventHandler<DownloadProgressChangedEventArgs> DownloadProgressChanged;

        public async Task<Chunk> Download(Request downloadRequest, long maximumSpeed, CancellationToken token)
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
                Chunk.Checkpoint();
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
                Chunk.Checkpoint();
                await Task.Delay(Chunk.Timeout, token);
                // re-request and continue downloading...
                return await Download(downloadRequest, maximumSpeed, token);
            }
        }
        private async Task DownloadChunk(Request downloadRequest, long maximumSpeed, CancellationToken token)
        {
            if (token.IsCancellationRequested ||
                IsDownloadCompleted())
                return;

            if (IsValidPosition() == false)
                Chunk.Position = 0;

            var request = downloadRequest.GetRequest();
            request.AddRange(Chunk.Start + Chunk.Position, Chunk.End);
            using var downloadResponse = request.GetResponse() as HttpWebResponse;
            using var responseStream = downloadResponse?.GetResponseStream();

            if (responseStream != null)
            {
                using var destinationStream = new ThrottledStream(responseStream, maximumSpeed);
                await ReadStream(destinationStream, token);
            }
        }
        protected bool HasSource(Exception exp, string source)
        {
            var e = exp;
            while (e != null)
            {
                if (string.Equals(e.Source, source, StringComparison.OrdinalIgnoreCase))
                    return true;

                e = e.InnerException;
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
        protected abstract Task ReadStream(Stream stream, CancellationToken token);
        protected virtual void OnDownloadProgressChanged(DownloadProgressChangedEventArgs e)
        {
            DownloadProgressChanged?.Invoke(this, e);
        }
    }
}
