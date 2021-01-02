using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Net.Http;

namespace Downloader.Test
{
    [TestClass]
    public class ExceptionHelperTest
    {
        private Exception GetException()
        {
            try
            {
                throw new Exception("High level exception",
                    new IOException("Mid level exception",
                        new HttpRequestException("Low level exception")));
            }
            catch (Exception e)
            {
                return e;
            }
        }

        [TestMethod]
        public void HasSourceFromThisNamespaceTest()
        {
            // arrange
            Exception exception = GetException();

            // act
            bool hasThisNamespace = ExceptionHelper.HasSource(exception, GetType().Namespace);

            // assert
            Assert.IsTrue(hasThisNamespace);
        }

        [TestMethod]
        public void HasSourceFromNonOccurrenceNamespaceTest()
        {
            // arrange
            Exception exception = GetException();

            // act
            bool hasSocketsNamespace = ExceptionHelper.HasSource(exception, "System.Net.Sockets");
            bool hasSecurityNamespace = ExceptionHelper.HasSource(exception, "System.Net.Security");

            // assert
            Assert.IsFalse(hasSocketsNamespace);
            Assert.IsFalse(hasSecurityNamespace);
        }
    }
}
