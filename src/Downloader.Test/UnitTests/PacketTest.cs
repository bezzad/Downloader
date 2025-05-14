namespace Downloader.Test.UnitTests;

public class PacketTest(ITestOutputHelper output) : BaseTestClass(output)
{
    [Fact]
    public void CreatePacketTest()
    {
        // arrange
        byte[] bytes = DummyData.GenerateOrderedBytes(1024);
        long pos = 1234;
        int len = 512;

        // act
        Packet packet = new(pos, bytes, len);

        // assert
        Assert.Equal(len, packet.Length);
        Assert.Equal(len, packet.Data.Length);
        Assert.Equal(pos, packet.Position);
        Assert.Equal(pos + len, packet.EndOffset);
        Assert.True(packet.Data.Span.SequenceEqual(bytes.Take(len).ToArray()));
    }
}
