using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Downloader.Test
{
    [TestClass]
    public class MemoryChunkTest
    {
        [TestMethod]
        public void ClearTest()
        {
            // arrange
            var chunk = new MemoryChunk(0, 1000);
            chunk.Data = new byte[chunk.Length];
            chunk.Position = 100;
            chunk.Timeout = 100;
            chunk.CanTryAgainOnFailover();

            // act
            chunk.Clear();

            // assert
            Assert.AreEqual(0, chunk.Position);
            Assert.AreEqual(0, chunk.Timeout);
            Assert.AreEqual(0, chunk.FailoverCount);
            Assert.IsNull(chunk.Data);
        }
    }
}
