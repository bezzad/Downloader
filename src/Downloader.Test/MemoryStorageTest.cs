using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Downloader.Test
{
    [TestClass]
    public class MemoryStorageTest : StorageTest
    {
        [TestInitialize]
        public override void Initial()
        {
            Storage = new MemoryStorage();
        }
    }
}
