using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Downloader.Test
{
    [TestClass]
    public class FileChunkTest : ChunkTest
    {
        [TestInitialize]
        public override void InitialTest()
        {
            Storage = new FileStorage();
        }
    }
}
