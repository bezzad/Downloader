using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Downloader.Test
{
    [TestClass]
    public class BandwidthTest
    {
        [TestMethod]
        public void TestCalculateAverageSpeed()
        {
            // arrange
            int delayTime = 50;
            int receivedBytesPerDelay = 250;
            int testElapsedTime = 4000; // 4s
            int repeatCount = testElapsedTime / delayTime;
            Bandwidth calculator = new Bandwidth();
            var speedHistory = new List<double>();

            // act
            for (int i = 0; i < repeatCount; i++)
            {
                Thread.Sleep(delayTime);
                calculator.CalculateSpeed(receivedBytesPerDelay);
                speedHistory.Add(calculator.Speed);
            }

            // assert
            var expectedAverageSpeed = Math.Ceiling(speedHistory.Average());
            var actualAverageSpeed = Math.Ceiling(calculator.AverageSpeed);
            var theoryAverageSpeed = 1000 / delayTime * receivedBytesPerDelay;
            Assert.IsTrue(expectedAverageSpeed < actualAverageSpeed, $"Actual Average Speed is: {actualAverageSpeed} , Expected Average Speed is: {expectedAverageSpeed}");
            Assert.IsTrue(actualAverageSpeed < theoryAverageSpeed, $"Actual Average Speed is: {actualAverageSpeed} , Theory Average Speed is: {theoryAverageSpeed}");
        }
    }
}
