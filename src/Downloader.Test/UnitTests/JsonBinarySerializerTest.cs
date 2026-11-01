using Downloader.Serializer;

namespace Downloader.Test.UnitTests;

public class JsonBinarySerializerTest(ITestOutputHelper output) : BaseTestClass(output)
{
    IBinarySerializer serializer = new JsonBinarySerializer();

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    [InlineData(1)]
    [InlineData(10_000)]
    public void SerializeBinaryDataByOffset(int dataLength)
    {
        // arrange
        var data = DummyData.GenerateOrderedBytes(dataLength);
        DownloadPackage package = new() {
            FileName = "file.txt",
            IsSupportDownloadInRange = true,
            TotalFileSize = 100_000,
            Urls = ["http://example.com/file"],
            Chunks =
            [
               new Chunk() { Start = 0, End = 10_000, Position= 10 },
               new Chunk() { Start = 0, End = 10_000, Position= 101},
               new Chunk() { Start = 0, End = 10_000, Position= 101},
               new Chunk() { Start = 0, End = 10_000, Position= 101},
                new Chunk() { Start = 10_000, End = 60_000, Position=10 },
                new Chunk() { Start = 60_000, End = 80_000, Position=10 },
                new Chunk() { Start = 80_000, End = 100_000, Position=10 }
            ]
        };

        // act
        var serializedPackage = serializer.Serialize(package);
        data = data.Concat(serializedPackage).ToArray();
        var deserializedPackage = serializer.Deserialize(data, dataLength, serializedPackage.Length);

        // assert
        Assert.NotNull(deserializedPackage);
        Assert.Equal(package.TotalFileSize, deserializedPackage.TotalFileSize);
        Assert.Equal(package.Chunks?.Length, deserializedPackage.Chunks?.Length);
        for (int i = 0; i < package.Chunks.Length; i++)
        {
            Assert.Equal(package.Chunks[i].Start, deserializedPackage.Chunks[i].Start);
            Assert.Equal(package.Chunks[i].End, deserializedPackage.Chunks[i].End);
            Assert.Equal(package.Chunks[i].Position, deserializedPackage.Chunks[i].Position);
        }
    }

    [Fact]
    public void SerializeNullThrows()
    {
        Assert.Throws<ArgumentNullException>(() => serializer.Serialize(null));
    }

    [Fact]
    public void DeserializeNullThrows()
    {
        Assert.Throws<ArgumentNullException>(() => serializer.Deserialize(null));
    }

    [Fact]
    public void DeserializeEmptyReturnsNull()
    {
        Assert.Null(serializer.Deserialize([]));
    }

    [Fact]
    public void DeserializeWholeArrayByDefault()
    {
        // arrange
        PackageInfo package = new() { TotalFileSize = 10, Chunks = [new Chunk(0, 9)] };
        byte[] bytes = serializer.Serialize(package);

        // act
        PackageInfo result = serializer.Deserialize(bytes);

        // assert
        Assert.Equal(10, result.TotalFileSize);
        Assert.Equal(9, result.Chunks.Single().End);
    }
}
