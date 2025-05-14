namespace Downloader.Test.UnitTests;

public class BandwidthTest(ITestOutputHelper output) : BaseTestClass(output)
{
    [Fact]
    public void TestCalculateAverageSpeed()
    {
        // arrange
        int delayTime = 50;
        int receivedBytesPerDelay = 250;
        int testElapsedTime = 4000; // 4s
        int repeatCount = testElapsedTime / delayTime;
        Bandwidth calculator = new();
        List<double> speedHistory = new();

        // act
        for (int i = 0; i < repeatCount; i++)
        {
            Thread.Sleep(delayTime);
            calculator.CalculateSpeed(receivedBytesPerDelay);
            speedHistory.Add(calculator.Speed);
        }

        // assert
        double expectedAverageSpeed = Math.Ceiling(speedHistory.Average());
        double actualAverageSpeed = Math.Ceiling(calculator.AverageSpeed);
        int theoryAverageSpeed = 1000 / delayTime * receivedBytesPerDelay;
        Assert.True(expectedAverageSpeed < actualAverageSpeed,
            $"Actual Average Speed is: {actualAverageSpeed} , Expected Average Speed is: {expectedAverageSpeed}");
        Assert.True(actualAverageSpeed < theoryAverageSpeed,
            $"Actual Average Speed is: {actualAverageSpeed} , Theory Average Speed is: {theoryAverageSpeed}");
    }
}