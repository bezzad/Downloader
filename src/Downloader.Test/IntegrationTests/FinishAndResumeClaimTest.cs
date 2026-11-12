namespace Downloader.Test.IntegrationTests;

/// <summary>
/// Two findings of the code review of the critical and high fixes (2026-11-11).
/// </summary>
public class FinishAndResumeClaimTest(ITestOutputHelper output) : BaseTestClass(output)
{
    private const int Size = 128 * 1024;

    /// <summary>
    /// A resume from a <see cref="DownloadPackage"/> writes a temp file too, so a second download to
    /// the same file must be stopped. Before the fix the resume did not claim its temp file.
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task ResumeFromPackageKeepsItsFileFromASecondDownload()
    {
        // arrange
        string folder = Directory.CreateTempSubdirectory().FullName;
        string file = Path.Combine(folder, "file.bin");
        string url = DummyFileHelper.GetFileWithNameUrl("1.bin", Size, 1);
        DownloadConfiguration slow = new() { MaximumBytesPerSecond = 64 * 1024 };

        try
        {
            await using DownloadService first = new(slow);
            Task firstRun = first.DownloadFileTaskAsync(url, file);
            await Task.Delay(500);
            await first.CancelTaskAsync();
            await firstRun;
            DownloadPackage package = first.Package;

            // act: resume the package, and start a second download to the same file while it runs
            // The resume stands still in its first progress event until the second download has ended,
            // so the second one surely runs while the resume holds the file (no timing guess).
            await using DownloadService resume = new(slow);
            TaskCompletionSource resumeIsWriting = new(TaskCreationOptions.RunContinuationsAsynchronously);
            using ManualResetEventSlim secondEnded = new();
            resume.DownloadProgressChanged += (_, _) => {
                resumeIsWriting.TrySetResult();
                secondEnded.Wait(TimeSpan.FromSeconds(20));
            };
            Task resumeRun = resume.DownloadFileTaskAsync(package, url);
            await resumeIsWriting.Task;
            await using DownloadService second = new(new DownloadConfiguration());
            AsyncCompletedEventArgs secondEnd = null;
            second.DownloadFileCompleted += (_, e) => secondEnd = e;
            await second.DownloadFileTaskAsync(DummyFileHelper.GetFileWithNameUrl("2.bin", Size, 2), file);
            secondEnded.Set();
            await resumeRun;

            // assert
            Assert.Equal(DownloadStatus.Failed, second.Status);
            Assert.IsType<IOException>(secondEnd?.Error);
            Assert.Equal(DownloadStatus.Completed, resume.Status);
            byte[] bytes = await File.ReadAllBytesAsync(file);
            Assert.Equal(Size, bytes.Length);
            Assert.All(bytes, b => Assert.Equal(1, b));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    /// <summary>
    /// Every byte is on the disk, and only the last rename fails. The download is Failed, but with
    /// <see cref="DownloadConfiguration.ClearPackageOnCompletionWithFailure"/> the finished temp file
    /// must not be deleted: the user would lose a whole download for a name problem.
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task FailedRenameKeepsTheDownloadedFile()
    {
        // arrange
        string folder = Directory.CreateTempSubdirectory().FullName;
        string file = Path.Combine(folder, "file.bin");
        await using DownloadService service = new(new DownloadConfiguration { ClearPackageOnCompletionWithFailure = true });
        // A folder takes the final name while the download runs, so the rename at the end fails.
        service.DownloadStarted += (_, _) => Directory.CreateDirectory(file);
        int events = 0;
        AsyncCompletedEventArgs end = null;
        service.DownloadFileCompleted += (_, e) => {
            Interlocked.Increment(ref events);
            end = e;
        };

        try
        {
            // act
            await service.DownloadFileTaskAsync(DummyFileHelper.GetFileWithNameUrl("1.bin", Size, 1), file);

            // assert
            Assert.Equal(DownloadStatus.Failed, service.Status);
            Assert.Equal(1, events);
            Assert.NotNull(end.Error);
            byte[] bytes = await File.ReadAllBytesAsync(file + ".download");
            Assert.Equal(Size, bytes.Length);
            Assert.All(bytes, b => Assert.Equal(1, b));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }
}
