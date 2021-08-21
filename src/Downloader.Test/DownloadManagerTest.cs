using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Downloader.Test
{
    [TestClass]
    public class DownloadManagerTest
    {
        private long _mockFileTotalSize = 1024 * 100;
        private IDownloadService _correctDownloadService;

        [TestInitialize]
        public void Initial()
        {
            _correctDownloadService = MockHelper.GetCorrectDownloadService(_mockFileTotalSize);
        }

        [TestMethod]
        public void MockDownloadTest()
        {

        }
    }
}
