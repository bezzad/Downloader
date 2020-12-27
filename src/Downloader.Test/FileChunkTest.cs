using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace Downloader.Test
{
    [TestClass]
    public class FileChunkTest
    {
        [TestMethod]
        public void ClearTest()
        {
            // arrange
            var chunk = new FileChunk(0, 1000) {
                FileName = Path.GetTempFileName(),
                Position = 100,
                Timeout = 100
            };
            chunk.CanTryAgainOnFailover();
            File.Create(chunk.FileName).Close();

            // act
            chunk.Clear();

            // assert
            Assert.AreEqual(0, chunk.Position);
            Assert.AreEqual(0, chunk.Timeout);
            Assert.AreEqual(0, chunk.FailoverCount);
            Assert.IsFalse(File.Exists(chunk.FileName));
        }
    }
}
