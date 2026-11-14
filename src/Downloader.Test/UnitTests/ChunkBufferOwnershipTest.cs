using System.Buffers;

namespace Downloader.Test.UnitTests;

/// <summary>
/// A rented read buffer must go back to the pool exactly once. When the storage refused a block, the
/// buffer went back twice (by the packet and by the read loop). Then two later reads got the same
/// array, and one download wrote the bytes of another one into its file.
/// </summary>
public class ChunkBufferOwnershipTest(ITestOutputHelper output) : BaseTestClass(output)
{
    [Fact]
    public async Task RefusedBlockReturnsTheBufferOnlyOnce()
    {
        // arrange: an unusual buffer size, so no other code uses this pool bucket
        const int bufferSize = 3000;
        DownloadConfiguration config = new() { BufferBlockSize = bufferSize };
        ConcurrentStream storage = new();
        await storage.DisposeAsync(); // the storage refuses every block now
        ChunkDownloader chunkDownloader = new(new Chunk(0, 9999) { Timeout = 1000 }, config, storage, new SocketClient(config));
        using MemoryStream source = new(new byte[10000]);

        // act
        await Assert.ThrowsAnyAsync<Exception>(() =>
            chunkDownloader.ReadStream(source, new PauseTokenSource().Token, CancellationToken.None));
        byte[][] rented = Enumerable.Range(0, 4).Select(_ => ArrayPool<byte>.Shared.Rent(bufferSize)).ToArray();

        // assert: four rents give four different arrays
        try
        {
            Assert.Equal(rented.Length, rented.Distinct(ReferenceEqualityComparer.Instance).Count());
        }
        finally
        {
            foreach (byte[] array in rented.Distinct(ReferenceEqualityComparer.Instance).Cast<byte[]>())
                ArrayPool<byte>.Shared.Return(array);
        }
    }
}
