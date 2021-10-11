using System.Net;
using System.Text;
using Downloader.Test.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Downloader.Test
{
    [TestClass]
    public class RequestTest
    {
        private const string EnglishText = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        private const string PersianText = "۰۱۲۳۴۵۶۷۸۹ابپتثجچحخدذرزژسشصضطظعغفقکگلمنوهیًٌٍَُِّْؤئيإأآة»«:؛كٰ‌ٔء؟";
        private static readonly Encoding Latin1Encoding = Encoding.GetEncoding("iso-8859-1");

        [TestMethod]
        public void GetFileNameWhenNoUrlTest()
        {
            // arrange
            var url = "  ";

            // act
            var actualFilename = new Request(url).GetFileName();

            // assert
            Assert.AreEqual("", actualFilename);
        }

        [TestMethod]
        public void GetFileNameWhenBadUrlTest()
        {
            // arrange
            var url = "http://www.a.com/a/b/c/d/e/";

            // act
            var actualFilename = new Request(url).GetFileName();

            // assert
            Assert.AreEqual("", actualFilename);
        }

        [TestMethod]
        public void GetFileNameWhenBadUrlWithFilenameTest()
        {
            // arrange
            var filename = "test";
            var url = "http://www.a.com/a/b/c/" + filename;

            // act
            var actualFilename = new Request(url).GetFileName();

            // assert
            Assert.AreEqual(filename, actualFilename);
        }

        [TestMethod]
        public void GetFileNameWithJustFilenameTest()
        {
            // arrange
            var filename = "test.xml";
            var url = filename;

            // act
            var actualFilename = new Request(url).GetFileName();

            // assert
            Assert.AreEqual(filename, actualFilename);
        }

        [TestMethod]
        public void GetFileNameWithJustFilenameWithoutExtensionTest()
        {
            // arrange
            var filename = "test";
            var url = filename;

            // act
            var actualFilename = new Request(url).GetFileName();

            // assert
            Assert.AreEqual(filename, actualFilename);
        }

        [TestMethod]
        public void GetFileNameWithShortUrlTest()
        {
            // arrange
            var filename = "test.xml";
            var url = "/" + filename;

            // act
            var actualFilename = new Request(url).GetFileName();

            // assert
            Assert.AreEqual(filename, actualFilename);
        }

        [TestMethod]
        public void GetFileNameWithShortUrlAndQueryParamTest()
        {
            // arrange
            var filename = "test.xml";
            var url = $"/{filename}?q=1";

            // act
            var actualFilename = new Request(url).GetFileName();

            // assert
            Assert.AreEqual(filename, actualFilename);
        }

        [TestMethod]
        public void GetFileNameWithShortUrlAndQueryParamsTest()
        {
            // arrange
            var filename = "test.xml";
            var url = $"/{filename}?q=1&x=100.0&y=testName";

            // act
            var actualFilename = new Request(url).GetFileName();

            // assert
            Assert.AreEqual(filename, actualFilename);
        }

        [TestMethod]
        public void GetFileNameWithJustFilenameAndQueryParamsTest()
        {
            // arrange
            var filename = "test.xml";
            var url = $"{filename}?q=1&x=100.0&y=testName";

            // act
            var actualFilename = new Request(url).GetFileName();

            // assert
            Assert.AreEqual(filename, actualFilename);
        }

        [TestMethod]
        public void GetFileNameWithUrlAndQueryParamsTest()
        {
            // arrange
            var filename = "test.xml";
            var url = $"http://www.a.com/{filename}?q=1&x=1&filename=test";

            // act
            var actualFilename = new Request(url).GetFileName();

            // assert
            Assert.AreEqual(filename, actualFilename);
        }

        [TestMethod]
        public void GetFileNameWithUrlAndQueryParamsAndFragmentIdentifierTest()
        {
            // arrange
            var filename = "test.xml";
            var url = $"http://www.a.com/{filename}?q=1&x=3#aidjsf";

            // act
            var actualFilename = new Request(url).GetFileName();

            // assert
            Assert.AreEqual(filename, actualFilename);
        }

        [TestMethod]
        public void GetUrlDispositionWhenNoUrlTest()
        {
            // arrange
            var url = "  ";

            // act
            var actualFilename = new Request(url).GetUrlDispositionFilenameAsync().Result;

            // assert
            Assert.IsNull(actualFilename);
        }

        [TestMethod]
        public void GetUrlDispositionWhenBadUrlTest()
        {
            // arrange
            var url = "http://www.a.com/a/b/c/d/e/";

            // act
            var actualFilename = new Request(url).GetUrlDispositionFilenameAsync().Result;

            // assert
            Assert.IsNull(actualFilename);
        }

        [TestMethod]
        public void GetUrlDispositionWhenBadUrlWithFilenameTest()
        {
            // arrange
            var filename = "test";
            var url = "http://www.a.com/a/b/c/" + filename;

            // act
            var actualFilename = new Request(url).GetUrlDispositionFilenameAsync().Result;

            // assert
            Assert.IsNull(actualFilename);
        }

        [TestMethod]
        public void GetUrlDispositionWithJustFilenameTest()
        {
            // arrange
            var filename = "test.xml";
            var url = filename;

            // act
            var actualFilename = new Request(url).GetUrlDispositionFilenameAsync().Result;

            // assert
            Assert.IsNull(actualFilename);
        }

        [TestMethod]
        public void GetUrlDispositionWithJustFilenameWithoutExtensionTest()
        {
            // arrange
            var filename = "test";
            var url = filename;

            // act
            var actualFilename = new Request(url).GetUrlDispositionFilenameAsync().Result;

            // assert
            Assert.IsNull(actualFilename);
        }

        [TestMethod]
        public void GetUrlDispositionWithShortUrlTest()
        {
            // arrange
            var filename = "test.xml";
            var url = "/" + filename;

            // act
            var actualFilename = new Request(url).GetUrlDispositionFilenameAsync().Result;

            // assert
            Assert.IsNull(actualFilename);
        }

        [TestMethod]
        public void GetUrlDispositionWithShortUrlAndQueryParamTest()
        {
            // arrange
            var filename = "test.xml";
            var url = $"/{filename}?q=1";

            // act
            var actualFilename = new Request(url).GetUrlDispositionFilenameAsync().Result;

            // assert
            Assert.IsNull(actualFilename);
        }

        [TestMethod]
        public void GetUrlDispositionWithShortUrlAndQueryParamsTest()
        {
            // arrange
            var filename = "test.xml";
            var url = $"/{filename}?q=1&x=100.0&y=testName";

            // act
            var actualFilename = new Request(url).GetUrlDispositionFilenameAsync().Result;

            // assert
            Assert.IsNull(actualFilename);
        }

        [TestMethod]
        public void GetUrlDispositionWithJustFilenameAndQueryParamsTest()
        {
            // arrange
            var filename = "test.xml";
            var url = $"{filename}?q=1&x=100.0&y=testName";

            // act
            var actualFilename = new Request(url).GetUrlDispositionFilenameAsync().Result;

            // assert
            Assert.IsNull(actualFilename);
        }

        [TestMethod]
        public void GetUrlDispositionWithUrlAndQueryParamsTest()
        {
            // arrange
            var filename = "test.xml";
            var url = $"http://www.a.com/{filename}?q=1&x=1&filename=test";

            // act
            var actualFilename = new Request(url).GetUrlDispositionFilenameAsync().Result;

            // assert
            Assert.IsNull(actualFilename);
        }

        [TestMethod]
        public void GetUrlDispositionWithUrlAndQueryParamsAndFragmentIdentifierTest()
        {
            // arrange
            var filename = "test.xml";
            var url = $"http://www.a.com/{filename}?q=1&x=3#aidjsf";

            // act
            var actualFilename = new Request(url).GetUrlDispositionFilenameAsync().Result;

            // assert
            Assert.IsNull(actualFilename);
        }

        [TestMethod]
        public void GetUrlDispositionWithLongUrlTest()
        {
            // arrange
            var filename = "excel_sample.xls";
            var url = $"https://raw.githubusercontent.com/bezzad/Downloader/master/src/Downloader.Test/Assets/{filename}?test=1";

            // act
            var actualFilename = new Request(url).GetUrlDispositionFilenameAsync().Result;

            // assert
            Assert.IsNull(actualFilename);
        }

        [TestMethod]
        public void GetFileSizeTest()
        {
            // arrange
            var url = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb);
            var expectedSize = DummyFileHelper.FileSize16Kb;

            // act
            var actualSize = new Request(url).GetFileSize().Result;

            // assert
            Assert.AreEqual(expectedSize, actualSize);
        }

        [TestMethod]
        public void ToUnicodeFromEnglishToEnglishTest()
        {
            // arrange
            byte[] inputRawBytes = Encoding.UTF8.GetBytes(EnglishText);
            string inputEncodedText = Latin1Encoding.GetString(inputRawBytes);
            Request requestInstance = new Request("");

            // act 
            string decodedEnglishText = requestInstance.ToUnicode(inputEncodedText);

            // assert
            Assert.AreEqual(EnglishText, decodedEnglishText);
        }

        [TestMethod]
        public void ToUnicodeFromPersianToPersianTest()
        {
            // arrange
            byte[] inputRawBytes = Encoding.UTF8.GetBytes(PersianText);
            string inputEncodedText = Latin1Encoding.GetString(inputRawBytes);
            Request requestInstance = new Request("");

            // act 
            string decodedEnglishText = requestInstance.ToUnicode(inputEncodedText);

            // assert
            Assert.AreEqual(PersianText, decodedEnglishText);
        }

        [TestMethod]
        public void ToUnicodeFromAllToAllWithExtensionTest()
        {
            // arrange
            string inputRawText = EnglishText + PersianText + ".ext";
            byte[] inputRawBytes = Encoding.UTF8.GetBytes(inputRawText);
            string inputEncodedText = Latin1Encoding.GetString(inputRawBytes);
            Request requestInstance = new Request("");

            // act 
            string decodedEnglishText = requestInstance.ToUnicode(inputEncodedText);

            // assert
            Assert.AreEqual(inputRawText, decodedEnglishText);
        }

        [TestMethod]
        public void GetRequestWithCredentialsTest()
        {
            // arrange
            var requestConfig = new RequestConfiguration() {
                Credentials = new NetworkCredential("username", "password")
            };
            var request = new Request("http://test.com", requestConfig);

            // act
            var httpRequest = request.GetRequest();

            // assert
            Assert.IsNotNull(httpRequest.Credentials);
        }

        [TestMethod]
        public void GetRequestWithNullCredentialsTest()
        {
            // arrange
            var requestConfig = new RequestConfiguration();
            var request = new Request("http://test.com", requestConfig);

            // act
            var httpRequest = request.GetRequest();

            // assert
            Assert.IsNull(httpRequest.Credentials);
        }
    }
}