using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Downloader.Test
{
    [TestClass]
    public class MemoryDownloadPackageTest : DownloadPackageTest
    {
        [TestInitialize]
        public override void Initial()
        {
            Configuration = new DownloadConfiguration() { OnTheFlyDownload = true };
            base.Initial();
        }
    }
}
