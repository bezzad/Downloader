using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace Downloader.Test.UnitTests
{
    public class StorageTestOnMemoryStream : StorageTest
    {
        private ConcurrentStream _storage;
        protected override ConcurrentStream Storage => _storage ??= new ConcurrentStream();

        [TestMethod]
        public void TestInitialSizeOnMemoryStream()
        {
            // assert
            Assert.IsInstanceOfType(Storage.OpenRead(), typeof(MemoryStream));
        }
    }
}
