using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Downloader.Test
{
    [TestClass]
    public class DownloadManagerTest
    {
        [TestInitialize]
        public virtual void Initial()
        {
            var testData = DummyData.GenerateOrderedBytes(DownloadTestHelper.FileSize16Kb);
            var x = new DownloadRequest(null, null);
            
        }
    }
}
