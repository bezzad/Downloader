using Downloader.DummyHttpServer;
using System;
using System.Linq;
using Xunit;

namespace Downloader.Test.UnitTests;

public class PacketTest
{
    [Fact]
    public void CreatePacketTest()
    {
        // arrange
        byte[] bytes = DummyData.GenerateOrderedBytes(1024);
        long pos = 1234;
        var len = 512;

        // act
        Packet packet = new Packet(pos, bytes, len);

        // assert
        Assert.Equal(len, packet.Length);
        Assert.NotEqual(len, packet.Data.Length);
        Assert.Equal(pos, packet.Position);
        Assert.Equal(pos + len, packet.EndOffset);
        Assert.True(packet.Data.SequenceEqual(bytes));
    }

    [Fact]
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
        Assert.Equal(packetLength, packetB.Length);
        Assert.Equal(packetLength * 2, packetA.Length);
        Assert.True(packetA.Data.SequenceEqual(concatData));
    }
}
