namespace Downloader.Test.UnitTests;

public class ProgressOutboxTest(ITestOutputHelper output) : BaseTestClass(output)
{
    private static void WaitUntilIdle(ProgressOutbox outbox)
    {
        Assert.True(SpinWait.SpinUntil(() => outbox.IsIdle, TimeSpan.FromSeconds(10)), "The deliverer did not finish.");
    }

    [Fact]
    public void NewestTotalAlwaysArrivesAndTheTotalsAddUp()
    {
        // Many short runs: the race between the writer and the deliverer ending must never lose the last total.
        const int blocks = 64;
        for (int run = 0; run < 2000; run++)
        {
            List<(long Progressed, long Total)> events = [];
            ProgressOutbox outbox = new((progressed, total, _) => events.Add((progressed, total)), () => true);
            outbox.Start(0, liveStreaming: false, liveLimit: 0, newFile: true);

            for (int i = 0; i < blocks; i++)
                outbox.OnWritten(1, default);

            WaitUntilIdle(outbox);
            Assert.Equal(blocks, events[^1].Total);
            Assert.Equal(blocks, events.Sum(e => e.Progressed));
            for (int i = 1; i < events.Count; i++)
                Assert.Equal(events[i - 1].Total + events[i].Progressed, events[i].Total);
        }
    }

    [Fact]
    public void LiveStreamingGivesEveryBlockAndWaitsOverTheLimit()
    {
        // arrange — the handler holds the first block until the test lets it go.
        using ManualResetEventSlim release = new(false);
        List<long> totals = [];
        ProgressOutbox outbox = new((_, total, _) => {
            release.Wait();
            totals.Add(total);
        }, () => true);
        outbox.Start(0, liveStreaming: true, liveLimit: 10, newFile: true);

        // act — 20 blocks of 1 byte: more than the limit waits for the held handler.
        for (int i = 0; i < 20; i++)
            outbox.OnWritten(1, new byte[1]);
        bool pausedWhileHeld = outbox.StreamRoom.IsPaused;
        release.Set();
        WaitUntilIdle(outbox);

        // assert
        Assert.True(pausedWhileHeld);
        Assert.False(outbox.StreamRoom.IsPaused);
        Assert.Equal(Enumerable.Range(1, 20).Select(i => (long)i), totals);
    }

    [Fact]
    public void NothingIsRaisedWithoutAHandler()
    {
        int raised = 0;
        ProgressOutbox outbox = new((_, _, _) => raised++, () => false);
        outbox.Start(0, liveStreaming: false, liveLimit: 0, newFile: true);

        outbox.OnWritten(5, default);

        Assert.Equal(5, outbox.Written);
        Assert.True(outbox.IsIdle);
        Assert.Equal(0, raised);
    }
}
