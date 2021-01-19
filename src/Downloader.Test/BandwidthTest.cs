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
            Bandwidth calculator = new Bandwidth();
            var speedHistory = new List<double>();

            // act
            for (int i = 0; i < 400; i++)
            {
                Thread.Sleep(10);
                calculator.CalculateSpeed(250);
                speedHistory.Add(calculator.Speed);
            }

            // assert
            var expectedAverageSpeed = Math.Ceiling(speedHistory.Average());
            var actualAverageSpeed = Math.Ceiling(calculator.AverageSpeed);
            Assert.AreEqual(expectedAverageSpeed, actualAverageSpeed,
                $"Actual Average Speed is: {actualAverageSpeed} , Expected Average Speed is: {expectedAverageSpeed}");
        }

        // public void TestCalculateSpeed()
        // {
        //     // arrange
        //     int delayTime = 10;
        //     int receivedBytesPerDelay = 250;
        //     int testElapsedTime = 4000; // 4s
        //     int repeatCount = testElapsedTime / delayTime;
        //     Bandwidth calculator = new Bandwidth();
        //
        //     // act
        //     for (int i = 0; i < repeatCount; i++)
        //     {
        //         Thread.Sleep(delayTime);
        //         calculator.CalculateSpeed(receivedBytesPerDelay);
        //     }
        //
        //     // assert
        //     var expectedAverageSpeed = Math.Ceiling(speedHistory.Average());
        //     var actualAverageSpeed = Math.Ceiling(calculator.AverageSpeed);
        //     Assert.AreEqual(expectedAverageSpeed, actualAverageSpeed,
        //         $"Actual Average Speed is: {actualAverageSpeed} , Expected Average Speed is: {expectedAverageSpeed}");
        // }
    }
}
