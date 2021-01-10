using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Downloader.Test
{
    [TestClass]
    public class FileStorageTest : StorageTest
    {
        [TestInitialize]
        public override void Initial()
        {
            Storage = new FileStorage("");
        }
    }
}
