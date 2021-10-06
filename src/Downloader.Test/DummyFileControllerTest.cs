using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Net.Http;

namespace Downloader.Test
{
    [TestClass]
    public class DummyFileControllerTest
    {
        int port;
        public DummyFileControllerTest()
        {
            port = 3333;
            DummyHttpServer.HttpServer.Run(port);
        }

        [TestMethod]
        public void GetBytesTest()
        {
            // arrange
            int size = 1024;
            string baseUrl = $"http://localhost:{port}/dummyfile/bytes/{size}";
            var client = new HttpClient();
            var dummyData = Helper.DummyData.GenerateOrderedBytes(size);
            var dummyDataBase64 = System.Convert.ToBase64String(dummyData);

            // act
            var task = client.GetStringAsync(baseUrl);
            task.Wait();
            var bytesBase64 = task.Result;

            // assert
            Assert.AreEqual(dummyDataBase64, bytesBase64.Trim('"'));
        }
    }
}
