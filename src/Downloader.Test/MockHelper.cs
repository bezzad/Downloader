using Moq;
using System.ComponentModel;
using System.Threading.Tasks;

namespace Downloader.Test
{
    public static class MockHelper
    {
        public static IDownloadService GetCorrectDownloadService(long fileTotalSize)
        {
            var mock = new Mock<IDownloadService>(MockBehavior.Strict);
            mock.SetupAllProperties();
            mock.Setup(d => d.DownloadFileTaskAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string>((url, filename) => {
                    mock.Object.Package = new DownloadPackage() {
                        Address = url,
                        FileName = filename,
                        TotalFileSize = fileTotalSize
                    };

                    // Start download...
                    mock.Object.Package.IsSaving = true;
                    mock.Raise(d => d.DownloadStarted+=null, new DownloadStartedEventArgs(filename, fileTotalSize));

                    // Raise progress events
                    var bytes = 1024;
                    while (bytes < fileTotalSize)
                    {
                        mock.Object.Package.SaveProgress = 100.0*bytes/fileTotalSize;
                        mock.Raise(d => d.DownloadProgressChanged+=null, new DownloadProgressChangedEventArgs(null) {
                            BytesPerSecondSpeed = fileTotalSize,
                            AverageBytesPerSecondSpeed = fileTotalSize,
                            ProgressedByteSize = 1024,
                            ReceivedBytesSize = bytes,
                            TotalBytesToReceive = fileTotalSize
                        });
                        bytes += 1024;
                    }

                    // Complete download
                    mock.Object.Package.SaveProgress = 100.0*bytes/fileTotalSize;
                    mock.Object.Package.IsSaveComplete = true;
                    mock.Object.Package.IsSaving = false;
                    mock.Raise(d => d.DownloadFileCompleted+=null, new AsyncCompletedEventArgs(null, false, null));
                })
                .Returns(Task.Delay(100));

            mock.Setup(d => d.CancelAsync()).Raises(d => d.DownloadFileCompleted+=null, new AsyncCompletedEventArgs(null, true, null));
            mock.Setup(d => d.IsBusy).Returns(true);
            mock.Verify();
            return mock.Object;
        }
    }
}
