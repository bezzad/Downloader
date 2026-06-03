using Downloader.Exceptions;

namespace Downloader.Test.UnitTests;

public class IncompleteDownloadExceptionTest
{
    [Fact]
    public void ParameterlessConstructorCreatesInstance()
    {
        // act
        IncompleteDownloadException exception = new();

        // assert
        Assert.IsType<IncompleteDownloadException>(exception);
        Assert.IsAssignableFrom<Exception>(exception);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void MessageConstructorSetsMessage()
    {
        // arrange
        const string message = "chunk ended before the whole length was received";

        // act
        IncompleteDownloadException exception = new(message);

        // assert
        Assert.Equal(message, exception.Message);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void MessageAndInnerConstructorSetsBoth()
    {
        // arrange
        const string message = "premature EOF";
        IOException inner = new("connection closed");

        // act
        IncompleteDownloadException exception = new(message, inner);

        // assert
        Assert.Equal(message, exception.Message);
        Assert.Same(inner, exception.InnerException);
    }
}
