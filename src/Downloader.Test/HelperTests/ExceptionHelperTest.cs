using Downloader.Test.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace Downloader.Test.HelperTests
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
}
