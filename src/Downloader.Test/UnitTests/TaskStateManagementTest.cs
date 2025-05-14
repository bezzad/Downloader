namespace Downloader.Test.UnitTests;

public class TaskStateManagementTest : TaskStateManagement
{
    [Fact]
    public void Should_IsCompleted_When_SetException_Called()
    {
        // act
        SetException(new Exception());

        // assert
        Assert.True(IsCompleted);
    }

    [Fact]
    public void Should_IsFaulted_When_SetException_Called()
    {
        // act
        SetException(new Exception());

        // assert
        Assert.True(IsFaulted);
    }

    [Fact]
    public void Should_TaskStatus_Faulted_After_SetException()
    {
        // act
        SetException(new Exception());

        // assert
        Assert.Equal(TaskStatus.Faulted, Status);
    }

    [Fact]
    public void Should_Get_InnerException_When_SetException_Called()
    {
        // arrange
        Exception exp1 = new("test exception 1");

        // act
        SetException(exp1);

        // assert
        Assert.Equal(exp1, Exception.InnerException);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(10)]
    public void Should_Get_AllExceptions_When_Multiple_SetException_Called(int innerExceptionCount)
    {
        // arrange
        Exception[] expectedExceptions = new Exception[innerExceptionCount];
        
        // act
        for (int i = 0; i < innerExceptionCount; i++)
        {
            Exception exp = new("test exception " + i);
            expectedExceptions[i] = exp;
            SetException(exp);
        }

        // assert
        Assert.Equal(expectedExceptions.Length, Exception.InnerExceptions.Count);
        for (int i = 0; i < innerExceptionCount; i++)
        {
            Assert.Equal(expectedExceptions[i], Exception.InnerExceptions[i]);
        }
    }

    [Fact]
    public void Should_Get_AggregateException_When_SetException_Called()
    {
        // arrange
        DivideByZeroException exp = new("test exception");

        // act
        SetException(exp);

        // assert
        Assert.IsType<AggregateException>(Exception);
    }

    [Fact]
    public void Should_IsCompleted_When_Cancelled()
    {
        // act
        CancelState();

        // assert
        Assert.True(IsCompleted);
    }

    [Fact]
    public void Should_IsCanceled_When_Cancelled()
    {
        // act
        CancelState();

        // assert
        Assert.True(IsCanceled);
    }

    [Fact]
    public void Should_Null_Exception_When_Cancelled()
    {
        // act
        CancelState();

        // assert
        Assert.Null(Exception);
    }

    [Fact]
    public void Should_IsCompleted_When_CompleteState()
    {
        // act
        CompleteState();

        // assert
        Assert.True(IsCompleted);
    }

    [Fact]
    public void Should_Not_IsFaulted_When_CompleteState()
    {
        // act
        CompleteState();

        // assert
        Assert.False(IsFaulted);
    }

    [Fact]
    public void Should_Not_IsFaulted_When_Cancelled()
    {
        // act
        CancelState();

        // assert
        Assert.False(IsFaulted);
    }   
    
    [Fact]
    public void Should_Rnning_State_When_Started()
    {
        // act
        StartState();

        // assert
        Assert.Equal(TaskStatus.Running, Status);
    }
}
