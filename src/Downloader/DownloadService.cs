using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader
{
    public class DownloadService : AbstractDownloadService
    {
        public DownloadService(DownloadConfiguration options) : base(options) { }
        public DownloadService() : base(null) { }

        protected override async Task<Stream> StartDownload()
        {
            try
            {
                await _singleInstanceSemaphore.WaitAsync();
                Package.TotalFileSize = await _requestInstances.First().GetFileSize().ConfigureAwait(false);
                Package.IsSupportDownloadInRange = await _requestInstances.First().IsSupportDownloadInRange().ConfigureAwait(false);
                Package.BuildStorage(Options.ReserveStorageSpaceBeforeStartingDownload, Options.MaximumMemoryBufferBytes, _globalCancellationTokenSource.Token);
                ValidateBeforeChunking();
                _chunkHub.SetFileChunks(Package);

                // firing the start event after creating chunks
                OnDownloadStarted(new DownloadStartedEventArgs(Package.FileName, Package.TotalFileSize));

                if (Options.ParallelDownload)
                {
                    await ParallelDownload(_pauseTokenSource.Token).ConfigureAwait(false);
                }
                else
                {
                    await SerialDownload(_pauseTokenSource.Token).ConfigureAwait(false);
                }

                SendDownloadCompletionSignal();
            }
            catch (OperationCanceledException exp) // or TaskCanceledException
            {
                Status = DownloadStatus.Stopped;
                OnDownloadFileCompleted(new AsyncCompletedEventArgs(exp, true, Package));
            }
            catch (Exception exp)
            {
                Status = DownloadStatus.Failed;
                OnDownloadFileCompleted(new AsyncCompletedEventArgs(exp, false, Package));
            }
            finally
            {
                _singleInstanceSemaphore.Release();
                await Task.Yield();
            }

            return Package.Storage?.OpenRead();
        }

        private void SendDownloadCompletionSignal()
        {
            Package.IsSaveComplete = true;
            Status = DownloadStatus.Completed;
            OnDownloadFileCompleted(new AsyncCompletedEventArgs(null, false, Package));
        }

        private void ValidateBeforeChunking()
        {
            CheckSingleChunkDownload();
            CheckSupportDownloadInRange();
            SetRangedSizes();
            CheckSizes();
        }

        private void SetRangedSizes()
        {
            if (Options.RangeDownload)
            {
                if (!Package.IsSupportDownloadInRange)
                {
                    throw new NotSupportedException("The server of your desired address does not support download in a specific range");
                }

                if (Options.RangeHigh < Options.RangeLow)
                {
                    Options.RangeLow = Options.RangeHigh - 1;
                }

                if (Options.RangeLow < 0)
                {
                    Options.RangeLow = 0;
                }

                if (Options.RangeHigh < 0)
                {
                    Options.RangeHigh = Options.RangeLow;
                }

                if (Package.TotalFileSize > 0)
                {
                    Options.RangeHigh = Math.Min(Package.TotalFileSize, Options.RangeHigh);
                }

                Package.TotalFileSize = Options.RangeHigh - Options.RangeLow + 1;
            }
            else
            {
                Options.RangeHigh = Options.RangeLow = 0; // reset range options
            }
        }

        private void CheckSizes()
        {
            if (Options.CheckDiskSizeBeforeDownload && !Package.InMemoryStream)
            {
                FileHelper.ThrowIfNotEnoughSpace(Package.TotalFileSize, Package.FileName);
            }
        }

        private void CheckSingleChunkDownload()
        {
            if (Package.TotalFileSize <= 1)
                Package.TotalFileSize = 0;

            if (Package.TotalFileSize <= Options.MinimumSizeOfChunking)
                SetSingleChunkDownload();
        }

        private void CheckSupportDownloadInRange()
        {
            if (Package.IsSupportDownloadInRange == false)
                SetSingleChunkDownload();
        }

        private void SetSingleChunkDownload()
        {
            Options.ChunkCount = 1;
            Options.ParallelCount = 1;
            _parallelSemaphore = new SemaphoreSlim(1, 1);
        }

        private async Task ParallelDownload(PauseToken pauseToken)
        {
            var tasks = GetChunksTasks(pauseToken);
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        private async Task SerialDownload(PauseToken pauseToken)
        {
            var tasks = GetChunksTasks(pauseToken);
            foreach (var task in tasks)
                await task.ConfigureAwait(false);
        }

        private IEnumerable<Task> GetChunksTasks(PauseToken pauseToken)
        {
            for (int i = 0; i < Package.Chunks.Length; i++)
            {
                var request = _requestInstances[i % _requestInstances.Count];
                yield return DownloadChunk(Package.Chunks[i], request, pauseToken, _globalCancellationTokenSource);
            }
        }

        private async Task<Chunk> DownloadChunk(Chunk chunk, Request request, PauseToken pause, CancellationTokenSource cancellationTokenSource)
        {
            ChunkDownloader chunkDownloader = new ChunkDownloader(chunk, Options, Package.Storage);
            chunkDownloader.DownloadProgressChanged += OnChunkDownloadProgressChanged;
            await _parallelSemaphore.WaitAsync(cancellationTokenSource.Token).ConfigureAwait(false);
            try
            {
                cancellationTokenSource.Token.ThrowIfCancellationRequested();
                return await chunkDownloader.Download(request, pause, cancellationTokenSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                cancellationTokenSource.Token.ThrowIfCancellationRequested();
                cancellationTokenSource.Cancel(false);
                throw;
            }
            finally
            {
                _parallelSemaphore.Release();
            }
        }
    }
}