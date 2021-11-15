using Downloader.Test.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Net;

namespace Downloader.Test
{
    [TestClass]
    public class DummyFileControllerTest
    {
        private string contentType = "application/octet-stream";

        [TestMethod]
        public void GetFileTest()
        {
            // arrange
            int size = 1024;
            byte[] bytes = new byte[size];
            string url = DummyFileHelper.GetFileUrl(size);
            var dummyData = DummyData.GenerateOrderedBytes(size);

            // act
            var headers = ReadAndGetHeaders(url, bytes);

            // assert
            Assert.IsTrue(dummyData.SequenceEqual(bytes));
            Assert.AreEqual(size.ToString(), headers["Content-Length"]);
            Assert.AreEqual(contentType, headers["Content-Type"]);
        }

        [TestMethod]
        public void GetFileWithNameTest()
        {
            // arrange
            int size = 2048;
            byte[] bytes = new byte[size];
            string filename = "testfilename.dat";
            string url = DummyFileHelper.GetFileWithNameUrl(filename, size);
            var dummyData = Helper.DummyData.GenerateOrderedBytes(size);

            // act
            var headers = ReadAndGetHeaders(url, bytes);

            // assert
            Assert.IsTrue(dummyData.SequenceEqual(bytes));
            Assert.AreEqual(size.ToString(), headers["Content-Length"]);
            Assert.AreEqual(contentType, headers["Content-Type"]);
        }

        [TestMethod]
        public void GetFileWithoutHeaderTest()
        {
            // arrange
            int size = 2048;
            byte[] bytes = new byte[size];
            string filename = "testfilename.dat";
            string url = DummyFileHelper.GetFileWithoutHeaderUrl(filename, size);
            var dummyData = Helper.DummyData.GenerateOrderedBytes(size);

            // act
            var headers = ReadAndGetHeaders(url, bytes);

            // assert
            Assert.IsTrue(dummyData.SequenceEqual(bytes));
            Assert.IsNull(headers["Content-Length"]);
            Assert.IsNull(headers["Content-Type"]);
        }

        [TestMethod]
        public void GetFileWithContentDispositionTest()
        {
            // arrange
            int size = 1024;
            byte[] bytes = new byte[size];
            string filename = "testfilename.dat";
            string url = DummyFileHelper.GetFileWithContentDispositionUrl(filename, size);
            var dummyData = Helper.DummyData.GenerateOrderedBytes(size);

            // act
            var headers = ReadAndGetHeaders(url, bytes);

            // assert
            Assert.IsTrue(dummyData.SequenceEqual(bytes));
            Assert.AreEqual(size.ToString(), headers["Content-Length"]);
            Assert.AreEqual(contentType, headers["Content-Type"]);
            Assert.IsTrue(headers["Content-Disposition"].Contains($"filename={filename};"));
        }

        private WebHeaderCollection ReadAndGetHeaders(string url, byte[] bytes)
        {
            HttpWebRequest request = WebRequest.CreateHttp(url);
            using HttpWebResponse downloadResponse = request.GetResponse() as HttpWebResponse;
            var respStream = downloadResponse.GetResponseStream();
            respStream.Read(bytes);

            return downloadResponse.Headers;
        }
    }
}
