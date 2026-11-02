namespace Downloader.Test.UnitTests;

public class ConcurrentPacketBufferTest(ITestOutputHelper output) : BaseTestClass(output)
{
    private static Packet NewPacket(long position, int length) => new(position, new byte[length], length, false);

    [Fact]
    public void AddedItemsCanBeCountedAndListed()
    {
        // arrange
        using var buffer = new ConcurrentPacketBuffer<Packet>();
        var first = NewPacket(0, 2);
        var second = NewPacket(2, 3);

        // act
        buffer.Add(first);
        buffer.Add(second);

        // assert
        Assert.Equal(2, buffer.Count);
        Assert.False(buffer.IsEmpty);
        Assert.Equal([first, second], buffer.ToArray());
        Assert.Equal([first, second], buffer.ToList());
        Assert.Equal(2, ((System.Collections.IEnumerable)buffer).Cast<Packet>().Count());
    }

    [Fact]
    public void ZeroOrNegativeBufferSizeMeansNoLimit()
    {
        // arrange
        using var buffer = new ConcurrentPacketBuffer<Packet>(10);

        // act
        buffer.BufferSize = 0;
        long zero = buffer.BufferSize;
        buffer.BufferSize = -5;

        // assert
        Assert.Equal(long.MaxValue, zero);
        Assert.Equal(long.MaxValue, buffer.BufferSize);
    }

    [Fact]
    public async Task AddingStopsWhenTheLimitIsPassedAndResumesWhenEmpty()
    {
        // arrange
        using var buffer = new ConcurrentPacketBuffer<Packet>(10, LogFactory.CreateLogger<ConcurrentPacketBufferTest>());
        int taken = 0;
        Task Take(Packet packet, CancellationToken token)
        {
            taken++;
            return Task.CompletedTask;
        }

        // act
        buffer.Add(NewPacket(0, 6));
        bool afterFirst = buffer.IsAddingCompleted;
        buffer.Add(NewPacket(6, 6)); // 12 bytes > 10 bytes limit
        bool afterSecond = buffer.IsAddingCompleted;
        Task<bool> blockedAdd = buffer.TryAddAsync(NewPacket(12, 6));
        await Task.Delay(50);
        bool addWasBlocked = !blockedAdd.IsCompleted;
        while (!buffer.IsEmpty || !blockedAdd.IsCompleted)
        {
            await buffer.WaitTryTakeAsync(CancellationToken.None, Take);
        }
        bool added = await blockedAdd;

        // assert
        Assert.False(afterFirst);
        Assert.True(afterSecond);
        Assert.True(addWasBlocked);
        Assert.True(added);
        Assert.Equal(3, taken);
        Assert.False(buffer.IsAddingCompleted);
    }

    [Fact]
    public async Task TryAddAfterDisposeReturnsFalse()
    {
        // arrange
        var buffer = new ConcurrentPacketBuffer<Packet>();
        buffer.Add(NewPacket(0, 1)); // drained and disposed by Dispose

        // act
        buffer.Dispose();
        buffer.Dispose(); // second call does nothing
        bool added = await buffer.TryAddAsync(NewPacket(1, 1));

        // assert: the refused packet is not left in the queue
        Assert.False(added);
        Assert.True(buffer.IsEmpty);
    }

    [Fact]
    public async Task ForceFlushReleaseUnblocksWaitToComplete()
    {
        // arrange
        using var buffer = new ConcurrentPacketBuffer<Packet>();
        buffer.Add(NewPacket(0, 1)); // flush now waits for the queue to be empty
        Task wait = buffer.WaitToComplete();
        await Task.Delay(50);
        bool wasBlocked = !wait.IsCompleted;

        // act
        buffer.ForceFlushRelease();
        await wait.WaitAsync(TimeSpan.FromSeconds(5));

        // assert
        Assert.True(wasBlocked);
        Assert.True(wait.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task CanceledTakeStillResumesAdding()
    {
        // arrange
        using var buffer = new ConcurrentPacketBuffer<Packet>(1);
        buffer.Add(NewPacket(0, 1));
        buffer.Add(NewPacket(1, 1)); // passes the limit, adding is stopped
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            buffer.WaitTryTakeAsync(cts.Token, (_, _) => Task.CompletedTask));

        // assert
        Assert.False(buffer.IsAddingCompleted);
        Assert.Equal(2, buffer.Count);
    }
}
