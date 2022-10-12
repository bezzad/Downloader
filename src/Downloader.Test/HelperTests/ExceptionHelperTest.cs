using Downloader.Test.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Net;
using System.Net.Http;

namespace Downloader.Test.HelperTests
{
    [TestClass]
    public class ExceptionHelperTest
    {
        [TestMethod]
        public void HasSourceFromThisNamespaceTest()
        {
            // arrange
            var exception = ExceptionThrower.GetException();
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
            var exception = ExceptionThrower.GetException();

            // act
            bool hasSocketsNamespace = exception.HasSource("System.Net.Sockets");
            bool hasSecurityNamespace = exception.HasSource("System.Net.Security");

            // assert
            Assert.IsFalse(hasSocketsNamespace);
            Assert.IsFalse(hasSecurityNamespace);
        }

        [TestMethod]
        public void HasTypeOfWebExceptionTest()
        {
            // arrange
            var exception = ExceptionThrower.GetWebException();

            // act
            bool hasTypeOfWebExp = exception.HasTypeOf(typeof(WebException));

            // assert
            Assert.IsTrue(hasTypeOfWebExp);
        }

        [TestMethod]
        public void HasTypeOfInnerExceptionsTest()
        {
            // arrange
            var exception = ExceptionThrower.GetWebException();

            // act
            bool hasTypeOfMultipleTypes = exception.HasTypeOf(typeof(DivideByZeroException),
                typeof(ArgumentNullException), typeof(HttpRequestException));

            // assert
            Assert.IsTrue(hasTypeOfMultipleTypes);
        }

        [TestMethod]
        public void HasTypeOfNonOccurrenceExceptionsTest()
        {
            // arrange
            var exception = ExceptionThrower.GetWebException();

            // act
            bool hasTypeOfMultipleTypes = exception.HasTypeOf(typeof(DivideByZeroException),
                typeof(ArgumentNullException), typeof(InvalidCastException));

            // assert
            Assert.IsFalse(hasTypeOfMultipleTypes);
        }

        [TestMethod]
        public void IsMomentumErrorTestWhenNoWebException()
        {
            // arrange
            var exception = ExceptionThrower.GetException();

            // act
            bool isMomentumError = exception.IsMomentumError();

            // assert
            Assert.IsFalse(isMomentumError);
        }

        [TestMethod]
        public void IsMomentumErrorTestOnWebException()
        {
            // arrange
            var exception = ExceptionThrower.GetWebException();

            // act
            bool isMomentumError = exception.IsMomentumError();

            // assert
            Assert.IsTrue(isMomentumError);
        }
    }
}
