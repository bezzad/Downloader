using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text;

namespace Downloader.Test
{
    [TestClass]
    public class RequestTest
    {
        [TestMethod]
        public void GetFileNameTest()
        {
            Assert.AreEqual(new Request("").GetFileName(), "");                                                    // ""
            Assert.AreEqual(new Request("test").GetFileName(), "test");                                            // "test"
            Assert.AreEqual(new Request("test.xml").GetFileName(), "test.xml");                                    // "test.xml"
            Assert.AreEqual(new Request("/test.xml").GetFileName(), "test.xml");                                   // "test.xml"
            Assert.AreEqual(new Request("/test.xml?q=1").GetFileName(), "test.xml");                               // "test.xml"
            Assert.AreEqual(new Request("/test.xml?q=1&x=3").GetFileName(), "test.xml");                           // "test.xml"
            Assert.AreEqual(new Request("test.xml?q=1&x=3").GetFileName(), "test.xml");                            // "test.xml"
            Assert.AreEqual(new Request("http://www.a.com/test.xml?q=1&x=3").GetFileName(), "test.xml");           // "test.xml"
            Assert.AreEqual(new Request("http://www.a.com/test.xml?q=1&x=3#aidjsf").GetFileName(), "test.xml");    // "test.xml"
            Assert.AreEqual(new Request("http://www.a.com/a/b/c/d").GetFileName(), "d");                           // "d"
            Assert.AreEqual(new Request("http://www.a.com/a/b/c/d/e/").GetFileName(), "");                         // ""
        }

        [TestMethod]
        public void GetUrlDispositionFilenameAsyncTest()
        {
            Assert.IsNull(new Request("").GetUrlDispositionFilenameAsync().Result);
            Assert.IsNull(new Request("test").GetUrlDispositionFilenameAsync().Result);
            Assert.IsNull(new Request("test.xml").GetUrlDispositionFilenameAsync().Result);
            Assert.IsNull(new Request("/test.xml").GetUrlDispositionFilenameAsync().Result);
            Assert.IsNull(new Request("/test.xml?q=1").GetUrlDispositionFilenameAsync().Result);
            Assert.IsNull(new Request("/test.xml?q=1&x=3").GetUrlDispositionFilenameAsync().Result);
            Assert.IsNull(new Request("test.xml?q=1&x=3").GetUrlDispositionFilenameAsync().Result);
            Assert.IsNull(new Request("http://www.a.com/test.xml?q=1&x=3").GetUrlDispositionFilenameAsync().Result);
            Assert.IsNull(new Request("http://www.a.com/test.xml?q=1&x=3#aidjsf").GetUrlDispositionFilenameAsync().Result);
            Assert.IsNull(new Request("http://www.a.com/a/b/c/d").GetUrlDispositionFilenameAsync().Result);
            Assert.IsNull(new Request("http://www.a.com/a/b/c/d/e/").GetUrlDispositionFilenameAsync().Result);
            Assert.IsNull(new Request("https://raw.githubusercontent.com/bezzad/Downloader/master/src/Downloader.Test/Assets/excel_sample.xls?test=1").GetUrlDispositionFilenameAsync().Result);
            // Below links has been expired
            // Assert.AreEqual("41d67991_Opra-Winfrey.mp3", new Request("https://download.taaghche.com/download/UL9Jas0Au65mnsmzkuC9SvXk6ukFY1mb").GetUrlDispositionFilenameAsync().Result);
            // Assert.AreEqual("1477431_00 - Radio Goushe - Hich Doosyi Be Joz Koohestan.mp3", new Request("https://download.taaghche.com/download/Cu3nIRTOsJa6WGFYgv2RB3bTKtCiriUA").GetUrlDispositionFilenameAsync().Result);
            // Assert.AreEqual("1537456_00 - Radiogoushe - Hipi.mp3", new Request("https://download.taaghche.com/download/r1VNHbqx5tfejOcFP0ASCQd2QucByws3").GetUrlDispositionFilenameAsync().Result);
            // Assert.AreEqual("8281905_11122.mp3", new Request("https://download.taaghche.com/download/FFAwlS3E0aNzkzTFiErsnbvLXm4wOvsF").GetUrlDispositionFilenameAsync().Result);
        }

        [TestMethod]
        public void GetFileSizeTest()
        {
            Assert.AreEqual(DownloadTestHelper.FileSize1Kb, new Request(DownloadTestHelper.File1KbUrl).GetFileSize().Result);
            Assert.AreEqual(DownloadTestHelper.FileSize150Kb, new Request(DownloadTestHelper.File150KbUrl).GetFileSize().Result);
            Assert.AreEqual(DownloadTestHelper.FileSize1Mb, new Request(DownloadTestHelper.File1MbUrl).GetFileSize().Result);
            Assert.AreEqual(DownloadTestHelper.FileSize8Mb, new Request(DownloadTestHelper.File8MbUrl).GetFileSize().Result);
            Assert.AreEqual(DownloadTestHelper.FileSize10Mb, new Request(DownloadTestHelper.File10MbUrl).GetFileSize().Result);
            Assert.AreEqual(DownloadTestHelper.FileSize100Mb, new Request(DownloadTestHelper.File100MbUrl).GetFileSize().Result);
        }

        [TestMethod]
        public void ToUnicodeTest()
        {
            var requestInstance = new Request("");
            var encodingLatin1 = Encoding.GetEncoding("iso-8859-1");
            Assert.AreEqual("test1", requestInstance.ToUnicode(encodingLatin1.GetString(Encoding.UTF8.GetBytes("test1"))));
            Assert.AreEqual("متن تستی.ext", requestInstance.ToUnicode(encodingLatin1.GetString(Encoding.UTF8.GetBytes("متن تستی.ext"))));
            Assert.AreEqual("متن تستی1230456789.ext", requestInstance.ToUnicode(encodingLatin1.GetString(Encoding.UTF8.GetBytes("متن تستی1230456789.ext"))));
        }
    }
}
