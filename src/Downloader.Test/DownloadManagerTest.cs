using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Downloader.Test
{
    [TestClass]
    public class DownloadManagerTest
    {
        private long _mockFileTotalSize = 1024 * 100;
        private IDownloadService _downloadService;

        [TestInitialize]
        public void Initial()
        {
            var downloadServiceMock = new Mock<IDownloadService>(MockBehavior.Strict);
            downloadServiceMock.SetupAllProperties();
            downloadServiceMock.Setup(d => d.DownloadFileTaskAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string>((url, filename) => {
                    downloadServiceMock.Object.Package = new DownloadPackage() {
                        Address = url,
                        FileName = filename,
                        TotalFileSize = _mockFileTotalSize
                    };

                    // Start download...
                    downloadServiceMock.Object.Package.IsSaving = true;
                    downloadServiceMock.Raise(d => d.DownloadStarted+=null, new DownloadStartedEventArgs(url, _mockFileTotalSize));

                    // Raise progress events
                    var bytes = 1024;
                    while (bytes < _mockFileTotalSize)
                    {
                        downloadServiceMock.Object.Package.SaveProgress = 100.0*bytes/_mockFileTotalSize;
                        downloadServiceMock.Raise(d => d.DownloadProgressChanged+=null, new DownloadProgressChangedEventArgs(null) {
                            BytesPerSecondSpeed = _mockFileTotalSize,
                            AverageBytesPerSecondSpeed = _mockFileTotalSize,
                            ProgressedByteSize = bytes,
                            ReceivedBytesSize = 1024,
                            TotalBytesToReceive = _mockFileTotalSize
                        });
                        bytes += 1024;
                    }

                    // Complete download
                    downloadServiceMock.Object.Package.SaveProgress = 100.0*bytes/_mockFileTotalSize;
                    downloadServiceMock.Object.Package.IsSaveComplete = true;
                    downloadServiceMock.Object.Package.IsSaving = false;
                    downloadServiceMock.Setup(d => d.CancelAsync()).Raises(d => d.DownloadFileCompleted+=null, new AsyncCompletedEventArgs(null, false, null));
                })
                .Returns(Task.Delay(100));

            downloadServiceMock.Setup(d => d.IsBusy).Returns(true);
            downloadServiceMock.Verify();
            _downloadService = downloadServiceMock.Object;
        }

        [TestMethod]
        public void DownloadTest()
        {
            _downloadService.DownloadStarted += _downloadService_DownloadStarted;
            _downloadService.DownloadFileCompleted +=_downloadService_DownloadFileCompleted;
            ;
            _downloadService.DownloadFileTaskAsync("test", "test1").Wait();
            _downloadService.DownloadFileTaskAsync("test1", "test1").Wait();
            _downloadService.CancelAsync();
            Assert.IsTrue(_downloadService.IsBusy);
        }

        private void _downloadService_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            Debug.WriteLine(e.Error);
            Debug.WriteLine(e.Cancelled);
        }

        private void _downloadService_DownloadStarted(object sender, DownloadStartedEventArgs e)
        {
            Debug.WriteLine(e.FileName);
        }
    }
}
