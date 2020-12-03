using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
        public void GetFileSizeTest()
        {
            Assert.AreEqual(DownloadTestHelper.FileSize1Kb, new Request(DownloadTestHelper.File1KbUrl).GetFileSize().Result);
            Assert.AreEqual(DownloadTestHelper.FileSize150Kb, new Request(DownloadTestHelper.File150KbUrl).GetFileSize().Result);
            Assert.AreEqual(DownloadTestHelper.FileSize1Mb, new Request(DownloadTestHelper.File1MbUrl).GetFileSize().Result);
            Assert.AreEqual(DownloadTestHelper.FileSize8Mb, new Request(DownloadTestHelper.File8MbUrl).GetFileSize().Result);
            Assert.AreEqual(DownloadTestHelper.FileSize10Mb, new Request(DownloadTestHelper.File10MbUrl).GetFileSize().Result);
            Assert.AreEqual(DownloadTestHelper.FileSize100Mb, new Request(DownloadTestHelper.File100MbUrl).GetFileSize().Result);
        }
    }
}
