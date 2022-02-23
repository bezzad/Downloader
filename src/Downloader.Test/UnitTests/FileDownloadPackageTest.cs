using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Downloader.Test.UnitTests
{
    [TestClass]
    public class FileDownloadPackageTest : DownloadPackageTest
    {
        [TestInitialize]
        public override void Initial()
        {
            Configuration = new DownloadConfiguration() { OnTheFlyDownload = false };
            base.Initial();
        }
    }
}