namespace Downloader.Test.HelperTests;

public class ExceptionHelperTest
{
    [Fact]
    public void HasSourceFromThisNamespaceTest()
    {
        // arrange
        var exception = ExceptionThrower.GetException();
        var exceptionSource = exception.Source;
        var currentNamespace = "Downloader.Test";

        // act
        bool hasThisNamespace = exception.HasSource(currentNamespace);

        // assert
        Assert.True(hasThisNamespace,
            $"Exception.Source: {exceptionSource}, CurrentNamespace: {currentNamespace}");
    }

    [Fact]
    public void HasSourceFromNonOccurrenceNamespaceTest()
    {
        // arrange
        var exception = ExceptionThrower.GetException();

        // act
        bool hasSocketsNamespace = exception.HasSource("System.Net.Sockets");
        bool hasSecurityNamespace = exception.HasSource("System.Net.Security");

        // assert
        Assert.False(hasSocketsNamespace);
        Assert.False(hasSecurityNamespace);
    }

    [Fact]
    public void HasTypeOfWebExceptionTest()
    {
        // arrange
        var exception = ExceptionThrower.GetWebException();

        // act
        bool hasTypeOfWebExp = exception.HasTypeOf(typeof(WebException));

        // assert
        Assert.True(hasTypeOfWebExp);
    }

    [Fact]
    public void HasTypeOfInnerExceptionsTest()
    {
        // arrange
        var exception = ExceptionThrower.GetWebException();

        // act
        bool hasTypeOfMultipleTypes = exception.HasTypeOf(typeof(DivideByZeroException),
            typeof(ArgumentNullException), typeof(HttpRequestException));

        // assert
        Assert.True(hasTypeOfMultipleTypes);
    }

    [Fact]
    public void HasTypeOfNonOccurrenceExceptionsTest()
    {
        // arrange
        var exception = ExceptionThrower.GetWebException();

        // act
        bool hasTypeOfMultipleTypes = exception.HasTypeOf(typeof(DivideByZeroException),
            typeof(ArgumentNullException), typeof(InvalidCastException));

        // assert
        Assert.False(hasTypeOfMultipleTypes);
    }

    [Fact]
    public void IsMomentumErrorTestWhenNoWebException()
    {
        // arrange
        var exception = ExceptionThrower.GetException();

        // act
        bool isMomentumError = exception.IsMomentumError();

        // assert
        Assert.False(isMomentumError);
    }

    [Fact]
    public void IsMomentumErrorTestOnWebException()
    {
        // arrange
        var exception = ExceptionThrower.GetWebException();

        // act
        bool isMomentumError = exception.IsMomentumError();

        // assert
        Assert.True(isMomentumError);
    }
}
