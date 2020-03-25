using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Downloader.Test
{
    [TestClass]
    public class DownloadServiceTest : DownloadService
    {
        [TestMethod]
        public void ChunkFileTest()
        {
            Assert.AreEqual(1, ChunkFile(1000, -1).Length);
            DownloadedChunks.Clear();
            Assert.AreEqual(1, ChunkFile(1000, 0).Length);
            DownloadedChunks.Clear();
            Assert.AreEqual(1, ChunkFile(1000, 1).Length);
            DownloadedChunks.Clear();
            Assert.AreEqual(10, ChunkFile(1000, 10).Length);
            DownloadedChunks.Clear();
            Assert.AreEqual(1000, ChunkFile(1000, 1000).Length);
            DownloadedChunks.Clear();
            Assert.AreEqual(1000, ChunkFile(1000, 10000).Length);
            DownloadedChunks.Clear();
            Assert.AreEqual(1000, ChunkFile(1000, 100000).Length);
            DownloadedChunks.Clear();

            var fileSize = 1024000;
            var parts = 100;
            var chunks = ChunkFile(fileSize, parts).OrderBy(c => c.Start).ToArray();
            Assert.AreEqual(parts, chunks.Length);
            Assert.AreEqual(0, chunks[0].Start);
            Assert.AreEqual(fileSize, chunks.Last().End + 1);
            for (var i = 1; i < chunks.Length; i++)
                Assert.AreEqual(chunks[i].Start, chunks[i - 1].End + 1);
        }

        [TestMethod]
        public void CompareFileBits()
        {
            var myFile = File.ReadAllBytes(@"C:\Users\Behza\Videos\FILIM\Person of Interest\PersonOfInterest.S01E02.Downloader.mkv");
            var idmFile = File.ReadAllBytes(@"C:\Users\Behza\Videos\FILIM\Person of Interest\PersonOfInterest.S01E01.mkv");

            Assert.AreEqual(myFile.Length, idmFile.Length);
            for (var i = 0; i < myFile.Length; i++)
            {
                if(myFile[i] != idmFile[i])
                    Debugger.Break();
                
                //Assert.AreEqual(myFile[i], idmFile[i]);
            }
        }
    }
}
