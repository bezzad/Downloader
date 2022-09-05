using Downloader.DummyHttpServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Net;

namespace Downloader.Test.HelperTests
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
            var dummyData = DummyData.GenerateOrderedBytes(size);

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
            var dummyData = DummyData.GenerateOrderedBytes(size);

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
            var dummyData = DummyData.GenerateOrderedBytes(size);

            // act
            var headers = ReadAndGetHeaders(url, bytes);

            // assert
            Assert.IsTrue(dummyData.SequenceEqual(bytes));
            Assert.AreEqual(size.ToString(), headers["Content-Length"]);
            Assert.AreEqual(contentType, headers["Content-Type"]);
            Assert.IsTrue(headers["Content-Disposition"].Contains($"filename={filename};"));
        }

        [TestMethod]
        public void GetFileWithRangeTest()
        {
            // arrange
            int size = 1024;
            byte[] bytes = new byte[size];
            string url = DummyFileHelper.GetFileUrl(size);
            var dummyData = DummyData.GenerateOrderedBytes(size);

            // act
            var headers = ReadAndGetHeaders(url, bytes, justFirst512Bytes: true);

            // assert
            Assert.IsTrue(dummyData.Take(512).SequenceEqual(bytes.Take(512)));
            Assert.AreEqual(contentType, headers["Content-Type"]);
            Assert.AreEqual("512", headers["Content-Length"]);
            Assert.AreEqual("bytes 0-511/1024", headers["Content-Range"]);
            Assert.AreEqual("bytes", headers["Accept-Ranges"]);
        }

        [TestMethod]
        public void GetFileWithNoAcceptRangeTest()
        {
            // arrange
            int size = 1024;
            byte[] bytes = new byte[size];
            string filename = "testfilename.dat";
            string url = DummyFileHelper.GetFileWithNoAcceptRangeUrl(filename, size);
            var dummyData = DummyData.GenerateOrderedBytes(size);

            // act
            var headers = ReadAndGetHeaders(url, bytes, justFirst512Bytes: true);

            // assert
            Assert.IsTrue(dummyData.SequenceEqual(bytes));
            Assert.AreEqual(size.ToString(), headers["Content-Length"]);
            Assert.AreEqual(contentType, headers["Content-Type"]);
            Assert.IsNull(headers["Accept-Ranges"]);
        }

        private WebHeaderCollection ReadAndGetHeaders(string url, byte[] bytes, bool justFirst512Bytes = false)
        {
            HttpWebRequest request = WebRequest.CreateHttp(url);
            if (justFirst512Bytes)
                request.AddRange(0, 511);
            using HttpWebResponse downloadResponse = request.GetResponse() as HttpWebResponse;
            var respStream = downloadResponse.GetResponseStream();
            respStream.Read(bytes);

            return downloadResponse.Headers;
        }
    }
}
