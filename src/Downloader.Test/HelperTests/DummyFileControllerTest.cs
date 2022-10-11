using Downloader.DummyHttpServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Net;

namespace Downloader.Test.HelperTests
{
    [TestClass]
    public class DummyFileControllerTest
    {
        private string contentType = "application/octet-stream";
        private WebHeaderCollection headers;

        [TestMethod]
        public void GetFileTest()
        {
            // arrange
            int size = 1024;
            byte[] bytes = new byte[size];
            string url = DummyFileHelper.GetFileUrl(size);
            var dummyData = DummyData.GenerateOrderedBytes(size);

            // act
            ReadAndGetHeaders(url, bytes);

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
            ReadAndGetHeaders(url, bytes);

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
            ReadAndGetHeaders(url, bytes);

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
            ReadAndGetHeaders(url, bytes);

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
            ReadAndGetHeaders(url, bytes, justFirst512Bytes: true);

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
            ReadAndGetHeaders(url, bytes, justFirst512Bytes: true);

            // assert
            Assert.IsTrue(dummyData.SequenceEqual(bytes));
            Assert.AreEqual(size.ToString(), headers["Content-Length"]);
            Assert.AreEqual(contentType, headers["Content-Type"]);
            Assert.IsNull(headers["Accept-Ranges"]);
        }

        [TestMethod]
        public void GetFileWithNameOnRedirectTest()
        {
            // arrange
            int size = 2048;
            byte[] bytes = new byte[size];
            string filename = "testfilename.dat";
            string url = DummyFileHelper.GetFileWithNameOnRedirectUrl(filename, size);
            var dummyData = DummyData.GenerateOrderedBytes(size);

            // act
            ReadAndGetHeaders(url, bytes);

            // assert
            Assert.IsTrue(dummyData.SequenceEqual(bytes));
            Assert.AreEqual(size.ToString(), headers["Content-Length"]);
            Assert.AreEqual(contentType, headers["Content-Type"]);
            Assert.AreNotEqual(url, headers[nameof(WebResponse.ResponseUri)]);
        }

        [TestMethod]
        public void GetFileWithFailureAfterOffsetTest()
        {
            // arrange
            int size = 10240;
            int failureOffset = size / 2;
            byte[] bytes = new byte[size];
            string url = DummyFileHelper.GetFileWithFailureAfterOffset(size, failureOffset);

            // act
            void getHeaders() => ReadAndGetHeaders(url, bytes, false);

            // assert
            Assert.ThrowsException<IOException>(getHeaders);
            Assert.AreEqual(size.ToString(), headers["Content-Length"]);
            Assert.AreEqual(contentType, headers["Content-Type"]);
            Assert.AreEqual(0, bytes[size - 1]);
        }

        private void ReadAndGetHeaders(string url, byte[] bytes, bool justFirst512Bytes = false)
        {
            HttpWebRequest request = WebRequest.CreateHttp(url);
            if (justFirst512Bytes)
                request.AddRange(0, 511);
            using HttpWebResponse downloadResponse = request.GetResponse() as HttpWebResponse;
            var respStream = downloadResponse.GetResponseStream();
            
            // keep response headers
            downloadResponse.Headers.Add(nameof(WebResponse.ResponseUri), downloadResponse.ResponseUri.ToString());
            headers = downloadResponse.Headers;

            // read stream data
            var readCount = 1;
            var offset = 0;
            while (readCount > 0)
            {
                var count = bytes.Length - offset;
                if (count <= 0)
                    break;

                readCount = respStream.Read(bytes, offset, count);
                offset += readCount;
            }            
        }
    }
}
