using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

namespace Downloader.Test.UnitTests
{
    [TestClass]
    public class MemoryDownloadPackageTest : DownloadPackageTest
    {
        [TestInitialize]
        public override async Task Initial()
        {
            Config = new DownloadConfiguration() { OnTheFlyDownload = true };
            await base.Initial();
        }
    }
}
