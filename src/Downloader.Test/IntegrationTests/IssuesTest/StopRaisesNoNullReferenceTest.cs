using System.Runtime.ExceptionServices;

namespace Downloader.Test.IntegrationTests.IssuesTest;

/// <summary>
/// A stop must never leave a <see cref="NullReferenceException"/> behind.
/// <para>
/// Both dispatch loops start EVERY chunk task eagerly (<c>GetChunksTasks(...).ToList()</c>) and then
/// await them one at a time, so a cancellation abandons every chunk the loop had not reached yet.
/// Those abandoned chunks are still inside their read loops and still raise progress events, while
/// <see cref="DownloadService.StartDownload"/> has already moved on to the terminal state — which
/// closes the package storage and sets <see cref="DownloadPackage.Storage"/> to <c>null</c>.
/// </para>
/// <para>
/// Every progress event runs <c>AbstractDownloadService.UpdatePackage</c>, which wrote the
/// auto-resume metadata through <c>Package.Storage.Write(...)</c> with no guard: a progress event
/// that lands after the terminal state threw "Object reference not set to an instance of an
/// object." out of the chunk that raised it. This is the NRE a consumer sees on a download that
/// was stopped and then retried.
/// </para>
/// </summary>
[Collection("Sequential")]
public class StopRaisesNoNullReferenceTest(ITestOutputHelper output) : BaseTestClass(output)
{
    [Fact(Timeout = 180_000)]
    public async Task AProgressEventLandingAfterTheStopMustNotThrowNullReference()
    {
        const int size = 64 * 1024;
        string folder = Path.Combine(Path.GetTempPath(), "nre-stop-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        List<string> nres = [];
        Lock gate = new();
        EventHandler<FirstChanceExceptionEventArgs> firstChance = (_, e) => {
            if (e.Exception is NullReferenceException)
                lock (gate)
                    nres.Add(new StackTrace(true).ToString());
        };

        DownloadConfiguration config = new() {
            ChunkCount = 8,
            ParallelDownload = false, // serial dispatch: the loop abandons every chunk it has not awaited
            ParallelCount = 8,
            MinimumSizeOfChunking = 512,
            BufferBlockSize = 1024,
            MaximumBytesPerSecond = 32 * 1024, // keep the transfer alive while the stop lands
            EnableAutoResumeDownload = true    // the once-per-second metadata write is what dereferences Storage
        };

        using DownloadService service = new(config);
        TaskCompletionSource terminal = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource holding = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int held = 0;

        service.DownloadFileCompleted += (_, _) => terminal.TrySetResult();
        service.ChunkDownloadProgressChanged += (_, e) => {
            // Hold the LAST chunk (the one the serial loop abandons when the stop arrives) inside its
            // progress event — the point the engine has already left the chunk's read loop and is
            // about to write the auto-resume metadata. The stop then runs the whole terminal path,
            // which closes the storage and nulls it, before this event is allowed to continue.
            if (e.ProgressId != "7" || Interlocked.Exchange(ref held, 1) == 1)
                return;

            holding.TrySetResult();
            terminal.Task.Wait(TimeSpan.FromSeconds(30));
            Thread.Sleep(1100); // reopen the once-per-second package-update gate
        };

        try
        {
            AppDomain.CurrentDomain.FirstChanceException += firstChance;

            Task download = service.DownloadFileTaskAsync(
                DummyFileHelper.GetFileWithNameUrl("stopped.bin", size), new DirectoryInfo(folder));

            // Stop only once a chunk is actually parked inside a progress event.
            await Task.WhenAny(holding.Task, Task.Delay(TimeSpan.FromSeconds(30)));
            service.CancelAsync();

            try { await download; }
            catch { /* a stop may surface as an exception; the event is what matters here */ }

            await Task.WhenAny(terminal.Task, Task.Delay(TimeSpan.FromSeconds(30)));
            await Task.Delay(3000); // let the held chunk finish its progress event
        }
        finally
        {
            AppDomain.CurrentDomain.FirstChanceException -= firstChance;
            try { Directory.Delete(folder, true); }
            catch { /* best effort */ }
        }

        // Guard against a vacuous pass: the test only means something if a chunk really was held
        // across the terminal state.
        Assert.True(Volatile.Read(ref held) == 1,
            "no chunk progress event landed after the stop — the race this test covers never happened");
        Assert.True(terminal.Task.IsCompleted, "the download never reported a terminal state");

        lock (gate)
        {
            foreach (string s in nres)
                Output.WriteLine("=== NullReferenceException ===\n" + s);

            Assert.True(nres.Count == 0,
                $"a stop raised {nres.Count} NullReferenceException(s); see the stacks above");
        }
    }
}
