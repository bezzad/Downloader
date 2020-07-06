using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Net.Http;

namespace Downloader.Test
{
    [TestClass]
    public class ObjectHelperTest
    {
        [TestMethod]
        public void HasSourceTest()
        {
            try
            {
                ThrowException();
            }
            catch (Exception exp)
            {
                Assert.IsTrue(exp.HasSource(GetType().Namespace));
                Assert.IsFalse(exp.HasSource("System.Net.Sockets"));
                Assert.IsFalse(exp.HasSource("System.Net.Security"));
            }
        }

        private void ThrowException()
        {
            throw new Exception("Top level exception", new IOException("Mid level exception", new HttpRequestException("End level exception")));
        }
    }
}
