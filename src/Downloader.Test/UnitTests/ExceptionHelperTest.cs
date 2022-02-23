using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Net.Http;

namespace Downloader.Test.UnitTests
{
    [TestClass]
    public class ExceptionHelperTest
    {
        [TestMethod]
        public void HasSourceFromThisNamespaceTest()
        {
            // arrange
            Exception exception = ExceptionThrower.GetException();
            var exceptionSource = exception.Source;
            var currentNamespace = "Downloader.Test";

            // act
            bool hasThisNamespace = exception.HasSource(currentNamespace);

            // assert
            Assert.IsTrue(hasThisNamespace,
                $"Exception.Source: {exceptionSource}, CurrentNamespace: {currentNamespace}");
        }

        [TestMethod]
        public void HasSourceFromNonOccurrenceNamespaceTest()
        {
            // arrange
            Exception exception = ExceptionThrower.GetException();

            // act
            bool hasSocketsNamespace = exception.HasSource("System.Net.Sockets");
            bool hasSecurityNamespace = exception.HasSource("System.Net.Security");

            // assert
            Assert.IsFalse(hasSocketsNamespace);
            Assert.IsFalse(hasSecurityNamespace);
        }
    }

    public static class ExceptionThrower
    {
        public static Exception GetException()
        {
            try
            {
                ThrowException();
                return new Exception(); // This code will never run.
            }
            catch (Exception e)
            {
                return e;
            }
        }
        private static void ThrowException()
        {
            try
            {
                ThrowIoException();
            }
            catch (Exception e)
            {
                throw new Exception("High level exception", e);
            }
        }
        private static void ThrowIoException()
        {
            try
            {
                ThrowHttpRequestException();
            }
            catch (Exception e)
            {
                throw new IOException("Mid level exception", e);
            }
        }
        private static void ThrowHttpRequestException()
        {
            throw new HttpRequestException("Low level exception");
        }
    }
}
