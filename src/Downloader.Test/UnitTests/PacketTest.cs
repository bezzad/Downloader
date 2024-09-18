using Downloader.DummyHttpServer;
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
}
