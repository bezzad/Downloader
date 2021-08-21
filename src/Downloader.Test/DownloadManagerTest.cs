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
        private IDownloadService _mockDownloadService;

        [TestInitialize]
        public void Initial()
        {
            var mock = new Mock<IDownloadService>(MockBehavior.Strict);
            mock.SetupAllProperties();
            mock.Setup(d => d.DownloadFileTaskAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string>((url, filename) => {
                    mock.Object.Package = new DownloadPackage() {
                        Address = url,
                        FileName = filename,
                        TotalFileSize = _mockFileTotalSize
                    };

                    // Start download...
                    mock.Object.Package.IsSaving = true;
                    mock.Raise(d => d.DownloadStarted+=null, new DownloadStartedEventArgs(filename, _mockFileTotalSize));

                    // Raise progress events
                    var bytes = 1024;
                    while (bytes < _mockFileTotalSize)
                    {
                        mock.Object.Package.SaveProgress = 100.0*bytes/_mockFileTotalSize;
                        mock.Raise(d => d.DownloadProgressChanged+=null, new DownloadProgressChangedEventArgs(null) {
                            BytesPerSecondSpeed = _mockFileTotalSize,
                            AverageBytesPerSecondSpeed = _mockFileTotalSize,
                            ProgressedByteSize = 1024,
                            ReceivedBytesSize = bytes,
                            TotalBytesToReceive = _mockFileTotalSize
                        });
                        bytes += 1024;
                    }

                    // Complete download
                    mock.Object.Package.SaveProgress = 100.0*bytes/_mockFileTotalSize;
                    mock.Object.Package.IsSaveComplete = true;
                    mock.Object.Package.IsSaving = false;
                    mock.Raise(d => d.DownloadFileCompleted+=null, new AsyncCompletedEventArgs(null, false, null));
                })
                .Returns(Task.Delay(100));

            mock.Setup(d => d.CancelAsync()).Raises(d => d.DownloadFileCompleted+=null, new AsyncCompletedEventArgs(null, true, null));
            mock.Setup(d => d.IsBusy).Returns(true);
            mock.Verify();
            _mockDownloadService = mock.Object;
        }

        [TestMethod]
        public void MockDownloadTest()
        {
            // arrange
            var ActualFileName = ""; // DownloadTestHelper.File1KbName;
            var downloadWasSuccessfull = false;
            var downloadProgressIsCorrect = true;
            _mockDownloadService.DownloadStarted += (s, e) => ActualFileName = e.FileName;
            _mockDownloadService.DownloadFileCompleted += (s, e) => downloadWasSuccessfull = e.Error == null && !e.Cancelled;
            _mockDownloadService.DownloadProgressChanged += (s, e) =>
                downloadProgressIsCorrect &= (e.ProgressPercentage == _mockDownloadService.Package.SaveProgress);

            // act
            _mockDownloadService.DownloadFileTaskAsync(DownloadTestHelper.File1KbUrl, DownloadTestHelper.File1KbName).Wait();
            
            // assert
            Assert.IsTrue(_mockDownloadService.IsBusy);
            Assert.AreEqual(DownloadTestHelper.File1KbUrl, _mockDownloadService.Package.Address);
            Assert.AreEqual(DownloadTestHelper.File1KbName, _mockDownloadService.Package.FileName);
            Assert.AreEqual(DownloadTestHelper.File1KbName, ActualFileName);
            Assert.IsTrue(downloadWasSuccessfull);
            Assert.IsTrue(downloadProgressIsCorrect);
        }
    }
}
