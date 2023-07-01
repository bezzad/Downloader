using Downloader.DummyHttpServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace Downloader.Test.UnitTests
{
    [TestClass]
    public class PacketTest
    {
        [TestMethod]
        public void CreatePacketTest()
        {
            // arrange
            byte[] bytes = DummyData.GenerateOrderedBytes(1024);
            long pos = 1234;
            var len = 512;

            // act
            Packet packet = new Packet(pos, bytes, len);

            // assert
            Assert.AreEqual(len, packet.Length);
            Assert.AreNotEqual(len, packet.Data.Length);
            Assert.AreEqual(pos, packet.Position);
            Assert.AreEqual(pos + len, packet.EndOffset);
            Assert.IsTrue(packet.Data.SequenceEqual(bytes));
        }

        [TestMethod]
        public void MergePacketsTest()
        {
            // arrange
            var packetLength = 512;
            var startPosA = 1024;
            var startPosB = 1024;
            var dataA = DummyData.GenerateOrderedBytes(1024);
            var dataB = DummyData.GenerateSingleBytes(1024, 8);
            var packetA = new Packet(startPosA, dataA, packetLength);
            var packetB = new Packet(startPosB, dataB, packetLength);
            var concatData = dataA.Take(packetLength).Concat(dataB.Take(packetLength));

            // act
            packetA.Merge(packetB);

            // assert
            Assert.AreEqual(packetLength, packetB.Length);
            Assert.AreEqual(packetLength * 2, packetA.Length);
            Assert.IsTrue(packetA.Data.SequenceEqual(concatData));
        }
    }
}
