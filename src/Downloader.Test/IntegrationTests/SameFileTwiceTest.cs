namespace Downloader.Test.IntegrationTests;

/// <summary>
/// H3 of the 7.0.1 review: two downloads to the same file at the same time (a user adds the same link
/// twice) must not share one temp file. Before the fix both said Completed and no file was left.
/// </summary>
public class SameFileTwiceTest(ITestOutputHelper output) : BaseTestClass(output)
{
    private const int Size = 128 * 1024;

    private sealed class Run
    {
        public DownloadService Service;
        public int Events;
        public AsyncCompletedEventArgs Args;
        public Task Task;
    }

    private static Run Start(FileExistPolicy policy, byte fill, string file)
    {
        Run run = new() {
            Service = new DownloadService(new DownloadConfiguration {
                MaximumBytesPerSecond = 64 * 1024,
                FileExistPolicy = policy
            })
        };
        run.Service.DownloadFileCompleted += (_, e) => {
            Interlocked.Increment(ref run.Events);
            run.Args = e;
        };
        run.Task = run.Service.DownloadFileTaskAsync(DummyFileHelper.GetFileWithNameUrl($"{fill}.bin", Size, fill), file);
        return run;
    }

    private static async Task<(Run A, Run B)> DownloadTwice(FileExistPolicy policy, string file)
    {
        Run a = Start(policy, 1, file);
        await Task.Delay(500); // A is writing its temp file now
        Run b = Start(policy, 2, file);
        Task both = Task.WhenAll(a.Task, b.Task);
        Assert.True(await Task.WhenAny(both, Task.Delay(30_000)) == both, "the downloads did not end");
        return (a, b);
    }

    private static void AssertFile(string path, byte fill)
    {
        byte[] bytes = File.ReadAllBytes(path);
        Assert.Equal(Size, bytes.Length);
        Assert.All(bytes, b => Assert.Equal(fill, b));
    }

    [Fact(Timeout = 60_000)]
    public async Task RenamePolicyGivesTheSecondDownloadAnotherName()
    {
        // arrange
        string folder = Directory.CreateTempSubdirectory().FullName;
        string file = Path.Combine(folder, "file.bin");

        try
        {
            // act
            (Run a, Run b) = await DownloadTwice(FileExistPolicy.Rename, file);

            // assert
            Assert.Equal(DownloadStatus.Completed, a.Service.Status);
            Assert.Equal(DownloadStatus.Completed, b.Service.Status);
            Assert.Equal(1, a.Events);
            Assert.Equal(1, b.Events);
            Assert.NotEqual(a.Service.Package.FileName, b.Service.Package.FileName);
            AssertFile(a.Service.Package.FileName, 1);
            AssertFile(b.Service.Package.FileName, 2);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact(Timeout = 60_000)]
    public async Task RenamePolicyWorksWhenBothStartAtTheSameMoment()
    {
        // Both downloads look for a free name at the same moment, before either one has claimed it.
        // The name check and the claim must be one step, or both pick file.bin and one fails.
        for (int round = 0; round < 5; round++)
        {
            // arrange
            string folder = Directory.CreateTempSubdirectory().FullName;
            string file = Path.Combine(folder, "file.bin");

            try
            {
                // act
                Run a = Start(FileExistPolicy.Rename, 1, file);
                Run b = Start(FileExistPolicy.Rename, 2, file);
                await Task.WhenAll(a.Task, b.Task);

                // assert
                Assert.Equal(DownloadStatus.Completed, a.Service.Status);
                Assert.Equal(DownloadStatus.Completed, b.Service.Status);
                Assert.NotEqual(a.Service.Package.FileName, b.Service.Package.FileName);
                AssertFile(a.Service.Package.FileName, 1);
                AssertFile(b.Service.Package.FileName, 2);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }
    }

    [Fact(Timeout = 60_000)]
    public async Task SecondDownloadToTheSameTempFileFailsAndKeepsTheFirst()
    {
        // arrange: the default policy (Delete)
        string folder = Directory.CreateTempSubdirectory().FullName;
        string file = Path.Combine(folder, "file.bin");

        try
        {
            // act
            (Run a, Run b) = await DownloadTwice(FileExistPolicy.Delete, file);

            // assert
            Assert.Equal(DownloadStatus.Completed, a.Service.Status);
            Assert.Equal(1, a.Events);
            Assert.Equal(DownloadStatus.Failed, b.Service.Status);
            Assert.Equal(1, b.Events);
            Assert.IsAssignableFrom<IOException>(b.Args.Error);
            AssertFile(file, 1);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    /// <summary>
    /// An app restarts a download from the completed event of the old one (retry, next mirror). The
    /// temp file must be free by then, or the new download fails with "another download is writing".
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task NewDownloadFromTheCompletedEventCanTakeTheSameFile()
    {
        // arrange
        string folder = Directory.CreateTempSubdirectory().FullName;
        string file = Path.Combine(folder, "file.bin");
        DownloadService second = new(new DownloadConfiguration());
        DownloadService first = new(new DownloadConfiguration { MaximumBytesPerSecond = 64 * 1024 });
        first.DownloadFileCompleted += (_, _) =>
            second.DownloadFileTaskAsync(DummyFileHelper.GetFileWithNameUrl("2.bin", Size, 2), file).Wait();

        try
        {
            // act: stop the first one; its completed event starts the second one on the same file
            Task run = first.DownloadFileTaskAsync(DummyFileHelper.GetFileWithNameUrl("1.bin", Size, 1), file);
            await Task.Delay(500);
            first.CancelAsync();
            await run;

            // assert
            Assert.Equal(DownloadStatus.Stopped, first.Status);
            Assert.Equal(DownloadStatus.Completed, second.Status);
            AssertFile(file, 2);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    /// <summary>
    /// A user clicks Stop and then Start at once: the app does not wait for the old download to end.
    /// The new download must wait a moment for the temp file instead of failing.
    /// </summary>
    [Fact(Timeout = 120_000)]
    public async Task StopAndStartAgainAtOnceWorks()
    {
        for (int round = 0; round < 10; round++)
        {
            // arrange
            string folder = Directory.CreateTempSubdirectory().FullName;
            string file = Path.Combine(folder, "file.bin");

            try
            {
                // act
                Run a = Start(FileExistPolicy.Delete, 1, file);
                await Task.Delay(300);
                a.Service.CancelAsync(); // not awaited, like an app that only marks the row Stopped
                DownloadService b = new(new DownloadConfiguration());
                await b.DownloadFileTaskAsync(DummyFileHelper.GetFileWithNameUrl("2.bin", Size, 2), file);
                await a.Task;

                // assert
                Assert.Equal(DownloadStatus.Completed, b.Status);
                AssertFile(file, 2);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }
    }
}
