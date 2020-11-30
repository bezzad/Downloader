using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace Downloader.Test
{
    [TestClass]
    public class ObjectHelperTest
    {
        [TestMethod]
        public void GetTempFileTest()
        {
            string nullVal = null;
            var baseUrl = "C:\\temp";
            var tempFile = baseUrl.GetTempFile();
            Assert.IsTrue(tempFile.StartsWith(baseUrl));
            Assert.AreNotEqual(baseUrl.GetTempFile(), baseUrl.GetTempFile());
            Assert.AreNotEqual(nullVal.GetTempFile(), nullVal.GetTempFile());
            Assert.IsTrue(File.Exists(baseUrl.GetTempFile()));
            Assert.IsTrue("".GetTempFile().StartsWith(Path.GetTempPath()));
            Assert.IsTrue("     ".GetTempFile().StartsWith(Path.GetTempPath()));
            Assert.IsTrue(nullVal.GetTempFile().StartsWith(Path.GetTempPath()));

            Directory.Delete(baseUrl, true);
        }

        [TestMethod]
        public void GetFileNameTest()
        {
            Assert.AreEqual(ObjectHelper.GetFileNameFromUrl(""), "");                                                    // ""
            Assert.AreEqual(ObjectHelper.GetFileNameFromUrl("test"), "test");                                            // "test"
            Assert.AreEqual(ObjectHelper.GetFileNameFromUrl("test.xml"), "test.xml");                                    // "test.xml"
            Assert.AreEqual(ObjectHelper.GetFileNameFromUrl("/test.xml"), "test.xml");                                   // "test.xml"
            Assert.AreEqual(ObjectHelper.GetFileNameFromUrl("/test.xml?q=1"), "test.xml");                               // "test.xml"
            Assert.AreEqual(ObjectHelper.GetFileNameFromUrl("/test.xml?q=1&x=3"), "test.xml");                           // "test.xml"
            Assert.AreEqual(ObjectHelper.GetFileNameFromUrl("test.xml?q=1&x=3"), "test.xml");                            // "test.xml"
            Assert.AreEqual(ObjectHelper.GetFileNameFromUrl("http://www.a.com/test.xml?q=1&x=3"), "test.xml");           // "test.xml"
            Assert.AreEqual(ObjectHelper.GetFileNameFromUrl("http://www.a.com/test.xml?q=1&x=3#aidjsf"), "test.xml");    // "test.xml"
            Assert.AreEqual(ObjectHelper.GetFileNameFromUrl("http://www.a.com/a/b/c/d"), "d");                           // "d"
            Assert.AreEqual(ObjectHelper.GetFileNameFromUrl("http://www.a.com/a/b/c/d/e/"), "");                         // ""
        }
    }
}
