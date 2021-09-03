using Moq;
using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace Downloader.Test
{
    public static class DownloadServiceMockHelper
    {
        /// <summary>
        /// Get a mocked instance of download service to simulate downloader for successful downloading.
        /// </summary>
        /// <param name="fileTotalSize">The total size of the file which must be downloaded</param>
        /// <param name="bytesSizePerProgress">The byte size of each progress event</param>
        /// <param name="delayPerProcess">delay per each progress of a download service</param>
        /// <returns>A mock instance of download service</returns>
        public static IDownloadService GetSuccessDownloadService(long fileTotalSize, int bytesSizePerProgress, int delayPerProcess)
        {
            return GetSpecialDownloadService(fileTotalSize, bytesSizePerProgress,
                (int)fileTotalSize/bytesSizePerProgress, delayPerProcess, false, null);
        }

        /// <summary>
        /// Get a mocked instance of download service to simulate downloader 
        /// and cancelling at half of download progress.
        /// </summary>
        /// <param name="fileTotalSize">The total size of the file which must be downloaded</param>
        /// <param name="bytesSizePerProgress">The byte size of each progress event</param>
        /// <param name="delayPerProcess">delay per each progress of a download service</param>
        /// <returns>A mock instance of download service</returns>
        public static IDownloadService GetCancelledDownloadServiceOn50Percent(long fileTotalSize, int bytesSizePerProgress, int delayPerProcess)
        {
            return GetSpecialDownloadService(fileTotalSize, bytesSizePerProgress,
                (int)(fileTotalSize/bytesSizePerProgress*0.5), delayPerProcess, true, null);
        }

        /// <summary>
        /// Get a mock object of download service which simulates the corrupted state of the downloader 
        /// that has been down after 30% of progress.
        /// </summary>
        /// <param name="fileTotalSize">The total size of the file which must be downloaded</param>
        /// <param name="bytesSizePerProgress">The byte size of each progress event</param>
        /// <param name="delayPerProcess">delay per each progress of a download service</param>
        /// <returns>A mock instance of download service</returns>
        public static IDownloadService GetCorruptedDownloadServiceOn30Percent(long fileTotalSize, int bytesSizePerProgress, int delayPerProcess)
        {
            return GetSpecialDownloadService(fileTotalSize, bytesSizePerProgress,
                (int)(fileTotalSize/bytesSizePerProgress*0.3), delayPerProcess,
                false, new System.IO.IOException("Test IO Exception"));
        }

        /// <summary>
        /// Get a mock object of download service which simulates the corrupted state of the downloader 
        /// that has been down after 30% of progress.
        /// </summary>
        /// <param name="fileTotalSize">The total size of the file which must be downloaded</param>
        /// <param name="bytesSizePerProgress">The byte size of each progress event</param>
        /// <param name="countOfProgressEvents">The count of download progress events</param>
        /// <param name="delayPerProcess">delay per each progress of a download service</param>
        /// <param name="isCancelled">Do you want to cancel download after all progresses?</param>
        /// <param name="exception">Do you want to corrupt download after all progresses with an exception?</param>
        /// <returns>A mock instance of download service</returns>
        public static IDownloadService GetSpecialDownloadService(long fileTotalSize, int bytesSizePerProgress,
            int countOfProgressEvents, int delayPerProcess, bool isCancelled, Exception exception)
        {
            var mock = GetDownloadServiceMock();
            mock.Setup(d => d.DownloadFileTaskAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string>((url, filename) => {
                    // Start download...
                    mock.StartDownloadServiceMock(url, filename, fileTotalSize);

                    // Raise progress events util half of file
                    mock.RaiseDownloadProgressEvent(fileTotalSize, bytesSizePerProgress, countOfProgressEvents, delayPerProcess).Wait();

                    // Complete download
                    mock.CompleteDownloadServiceMock(isCancelled, exception);
                })
                .Returns(Task.Delay(100));

            mock.Verify();
            return mock.Object;
        }

        private static Mock<IDownloadService> GetDownloadServiceMock()
        {
            var mock = new Mock<IDownloadService>(MockBehavior.Strict);
            mock.SetupAllProperties();
            mock.Setup(d => d.CancelAsync()).Raises(d => d.DownloadFileCompleted+=null, new AsyncCompletedEventArgs(null, true, null));
            return mock;
        }
        private static void StartDownloadServiceMock(this Mock<IDownloadService> mock, string url, string filename, long fileTotalSize)
        {
            mock.Object.Package = new DownloadPackage() {
                Address = url,
                FileName = filename,
                TotalFileSize = fileTotalSize,
                IsSaving = true
            };
            mock.Raise(d => d.DownloadStarted+=null, new DownloadStartedEventArgs(filename, fileTotalSize));
        }
        private static async Task RaiseDownloadProgressEvent(this Mock<IDownloadService> mock, long totalSize, int bytesPerProgress, int progressCount, int delayPerProcess)
        {
            var counter = 1;
            var bytesCount = bytesPerProgress;

            while (bytesCount <= totalSize && counter++ <= progressCount)
            {
                mock.Object.Package.SaveProgress = 100.0*bytesCount/totalSize;
                mock.Raise(d => d.DownloadProgressChanged+=null, new DownloadProgressChangedEventArgs(null) {
                    BytesPerSecondSpeed = bytesPerProgress,
                    AverageBytesPerSecondSpeed = bytesPerProgress,
                    ProgressedByteSize = bytesPerProgress,
                    ReceivedBytesSize = bytesCount,
                    TotalBytesToReceive = totalSize
                });
                bytesCount += bytesPerProgress;
                await Task.Delay(delayPerProcess);
            }
        }
        private static void CompleteDownloadServiceMock(this Mock<IDownloadService> mock, bool isCancelled, Exception error)
        {
            if (isCancelled == false && error == null)
            {
                mock.Object.Package.IsSaveComplete = true;
            }

            mock.Object.Package.IsSaving = false;
            mock.Raise(d => d.DownloadFileCompleted+=null, new AsyncCompletedEventArgs(error, isCancelled, null));
        }
    }
}
