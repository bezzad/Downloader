using Downloader.DummyHttpServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

namespace Downloader.Test.UnitTests
{
    [TestClass]
    public class DownloadPackageTestOnMemory : DownloadPackageTest
    {
        [TestInitialize]
        public override async Task Initial()
        {
            Package = new DownloadPackage() {
                Urls = new[] { DummyFileHelper.GetFileWithNameUrl(DummyFileHelper.SampleFile16KbName, DummyFileHelper.FileSize16Kb) },
                TotalFileSize = DummyFileHelper.FileSize16Kb
            };

            await base.Initial();
        }
    }
}
