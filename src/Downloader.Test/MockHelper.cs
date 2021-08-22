using Moq;
using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace Downloader.Test
{
    public static class MockHelper
    {        
        public static IDownloadService GetSuccessDownloadService(long fileTotalSize, int bytesCountPerProgress)
        {
            var mock = GetDownloadServiceMock();
            mock.Setup(d => d.DownloadFileTaskAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string>((url, filename) => {
                    // Start download...
                    mock.StartDownloadServiceMock(url, filename, fileTotalSize);

                    // Raise progress events
                    mock.RaiseDownloadProgressEvent(fileTotalSize, bytesCountPerProgress, (int)fileTotalSize/bytesCountPerProgress);

                    // Complete download
                    mock.CompleteDownloadServiceMock(false, null);
                })
                .Returns(Task.Delay(100));
            
            mock.Verify();
            return mock.Object;
        }
        public static IDownloadService GetCancelledDownloadService(long fileTotalSize, int bytesCountPerProgress)
        {
            var mock = GetDownloadServiceMock();
            mock.Setup(d => d.DownloadFileTaskAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string>((url, filename) => {
                    // Start download...
                    mock.StartDownloadServiceMock(url, filename, fileTotalSize);

                    // Raise progress events util half of file
                    mock.RaiseDownloadProgressEvent(fileTotalSize, bytesCountPerProgress, (int)fileTotalSize/(bytesCountPerProgress*2)); 

                    // Complete download
                    mock.CompleteDownloadServiceMock(true, null);
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
        private static void RaiseDownloadProgressEvent(this Mock<IDownloadService> mock, long totalSize, int bytesPerProgress, int progressCount)
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
            }
        }
        private static void CompleteDownloadServiceMock(this Mock<IDownloadService> mock, bool isCancelled, Exception error)
        {
            if (isCancelled == false && error == null)
            {
                mock.Object.Package.IsSaveComplete = true;
            }

            mock.Object.Package.IsSaving = false;
            mock.Raise(d => d.DownloadFileCompleted+=null, new AsyncCompletedEventArgs(null, isCancelled, error));
        }
    }
}
