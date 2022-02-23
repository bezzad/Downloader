using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Downloader.Test.UnitTests
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
