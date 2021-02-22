using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace Downloader.Test
{
    [TestClass]
    public class DownloadPackageTest
    {
        private IStorage Storage { get; set; }
        private DownloadPackage Package { get; set; }


        [TestInitialize]
        public void Initial()
        {
            Storage = new FileStorage("");
        }
    }
}
