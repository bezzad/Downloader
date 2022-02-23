using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Downloader.Test.UnitTests
{
    [TestClass]
    public class MemoryChunkTest : ChunkTest
    {
        [TestInitialize]
        public override void InitialTest()
        {
            Storage = new MemoryStorage();
        }
    }
}
