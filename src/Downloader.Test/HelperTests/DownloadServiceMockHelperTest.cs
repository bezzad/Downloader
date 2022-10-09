using Downloader.DummyHttpServer;
using Downloader.Test.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

namespace Downloader.Test.HelperTests
{
    [TestClass]
    public class DownloadServiceMockHelperTest
    {
        [TestMethod]
        public async Task GetSuccessDownloadServiceTest()
        {
            // arrange
            var totalSize = 102400;
            var bytesCountPerProgress = 1024;
            var mockDownloadService = DownloadServiceMockHelper.GetSuccessDownloadService(totalSize, bytesCountPerProgress, 1);
            var states = new DownloadServiceEventsState(mockDownloadService);
            var url = DummyFileHelper.GetFileWithNameUrl(DummyFileHelper.SampleFile1KbName, DummyFileHelper.FileSize1Kb);

            // act
            await mockDownloadService.DownloadFileTaskAsync(url, DummyFileHelper.SampleFile1KbName).ConfigureAwait(false);

            // assert
            Assert.AreEqual(url, mockDownloadService.Package.Address);
            Assert.AreEqual(DummyFileHelper.SampleFile1KbName, mockDownloadService.Package.FileName);
            Assert.AreEqual(DummyFileHelper.SampleFile1KbName, states.ActualFileName);
            Assert.AreEqual(totalSize/bytesCountPerProgress, states.DownloadProgressCount);
            Assert.IsTrue(states.DownloadSuccessfullCompleted);
            Assert.IsTrue(states.DownloadProgressIsCorrect);
            Assert.IsNull(states.DownloadError);
            Assert.IsTrue(mockDownloadService.Package.IsSaveComplete);
            Assert.IsFalse(mockDownloadService.Package.IsSaving);
        }

        [TestMethod]
        public async Task GetCancelledDownloadServiceOn50PercentTest()
        {
            // arrange
            var totalSize = 102400;
            var bytesCountPerProgress = 1024;
            var mockDownloadService = DownloadServiceMockHelper.GetCancelledDownloadServiceOn50Percent(totalSize, bytesCountPerProgress, 1);
            var states = new DownloadServiceEventsState(mockDownloadService);
            var url = DummyFileHelper.GetFileWithNameUrl(DummyFileHelper.SampleFile1KbName, DummyFileHelper.FileSize1Kb);

            // act
            await mockDownloadService.DownloadFileTaskAsync(url, DummyFileHelper.SampleFile1KbName).ConfigureAwait(false);

            // assert
            Assert.AreEqual(url, mockDownloadService.Package.Address);
            Assert.AreEqual(DummyFileHelper.SampleFile1KbName, mockDownloadService.Package.FileName);
            Assert.AreEqual(DummyFileHelper.SampleFile1KbName, states.ActualFileName);
            Assert.AreEqual(totalSize/bytesCountPerProgress*0.5, states.DownloadProgressCount);
            Assert.IsFalse(states.DownloadSuccessfullCompleted);
            Assert.IsTrue(states.DownloadProgressIsCorrect);
            Assert.IsNull(states.DownloadError);
            Assert.IsFalse(mockDownloadService.Package.IsSaveComplete);
            Assert.IsFalse(mockDownloadService.Package.IsSaving);
        }

        [TestMethod]
        public async Task GetCorruptedDownloadServiceOn30PercentTest()
        {
            // arrange
            var totalSize = 102400;
            var bytesCountPerProgress = 1024;
            var mockDownloadService = DownloadServiceMockHelper.GetCorruptedDownloadServiceOn30Percent(totalSize, bytesCountPerProgress, 1);
            var states = new DownloadServiceEventsState(mockDownloadService);
            var url = DummyFileHelper.GetFileWithNameUrl(DummyFileHelper.SampleFile1KbName, DummyFileHelper.FileSize1Kb);

            // act
            await mockDownloadService.DownloadFileTaskAsync(url, DummyFileHelper.SampleFile1KbName).ConfigureAwait(false);

            // assert
            Assert.AreEqual(url, mockDownloadService.Package.Address);
            Assert.AreEqual(DummyFileHelper.SampleFile1KbName, mockDownloadService.Package.FileName);
            Assert.AreEqual(DummyFileHelper.SampleFile1KbName, states.ActualFileName);
            Assert.AreEqual(totalSize/bytesCountPerProgress*0.3, states.DownloadProgressCount);
            Assert.IsFalse(states.DownloadSuccessfullCompleted);
            Assert.IsTrue(states.DownloadProgressIsCorrect);
            Assert.IsNotNull(states.DownloadError);
            Assert.IsFalse(mockDownloadService.Package.IsSaveComplete);
            Assert.IsFalse(mockDownloadService.Package.IsSaving);
        }
    }
}
