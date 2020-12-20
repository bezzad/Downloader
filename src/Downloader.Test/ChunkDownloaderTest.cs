using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Downloader.Test
{
    [TestClass]
    public class ChunkDownloaderTest : ChunkDownloader
    {
        public ChunkDownloaderTest() 
            : base(new MemoryChunk(0, 10000), 1024)
        {}

        public ChunkDownloaderTest(Chunk chunk, int blockSize) 
            : base(chunk, blockSize)
        { }

        protected override Task ReadStream(Stream stream, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        private void ThrowException()
        {
            throw new Exception("Top level exception", new IOException("Mid level exception", new HttpRequestException("End level exception")));
        }

        [TestMethod]
        public void HasSourceTest()
        {
            try
            {
                ThrowException();
            }
            catch (Exception exp)
            {
                Assert.IsTrue(HasSource(exp, GetType().Namespace));
                Assert.IsFalse(HasSource(exp, "System.Net.Sockets"));
                Assert.IsFalse(HasSource(exp, "System.Net.Security"));
            }
        }
    }
}
