namespace Downloader.Test.IntegrationTests;

/// <summary>
/// End-to-end paths of <see cref="DownloadService"/> and <see cref="DownloadBuilder"/> that the
/// other integration tests do not reach.
/// </summary>
public class DownloadPathsIntegrationTest(ITestOutputHelper output) : BaseTestClass(output)
{
    private static DownloadConfiguration NewConfig(bool autoResume) => new() {
        ChunkCount = 4,
        ParallelDownload = true,
        MinimumSizeOfChunking = 0,
        EnableAutoResumeDownload = autoResume,
        FileExistPolicy = FileExistPolicy.Delete
    };

    private static string NewFolder()
    {
        string folder = Path.Combine(Path.GetTempPath(), "downloader-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    [Fact(Timeout = 60_000)]
    public async Task BuilderWithFolderOnlyUsesTheServerFileName()
    {
        // arrange
        string folder = NewFolder();
        string url = DummyFileHelper.GetFileWithNameUrl(DummyFileHelper.SampleFile16KbName, DummyFileHelper.FileSize16Kb);
        IDownload download = DownloadBuilder.New().WithUrl(url).WithDirectory(folder).Build();

        try
        {
            // act
            Stream result = await download.StartAsync();

            // assert
            string file = Path.Combine(folder, DummyFileHelper.SampleFile16KbName);
            Assert.Same(Stream.Null, result);
            Assert.Equal(DownloadStatus.Completed, download.Status);
            byte[] bytes = await File.ReadAllBytesAsync(file);
            Assert.Equal(DummyFileHelper.File16Kb, bytes);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact(Timeout = 60_000)]
    public async Task BuilderWithPackageAndUrlDownloadsFromTheGivenUrl()
    {
        // arrange
        string url = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb);
        DownloadPackage package = new() { Urls = ["http://localhost:1/not-used"] };
        IDownload download = DownloadBuilder.New().WithUrl(url).Build(package);

        // act
        await using Stream result = await download.StartAsync();

        // assert
        Assert.Equal(DownloadStatus.Completed, download.Status);
        Assert.True(DummyFileHelper.File16Kb.AreEqual(result));
    }

    [Fact(Timeout = 60_000)]
    public async Task OldDownloadingFileIsDeletedWhenAutoResumeIsOff()
    {
        // arrange
        string folder = NewFolder();
        string file = Path.Combine(folder, "file.bin");
        await File.WriteAllBytesAsync(file + ".download", new byte[100]);
        string url = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb);
        await using DownloadService service = new(NewConfig(autoResume: false));

        try
        {
            // act
            await service.DownloadFileTaskAsync(url, file);

            // assert
            Assert.Equal(DownloadStatus.Completed, service.Status);
            Assert.False(File.Exists(file + ".download"));
            byte[] bytes = await File.ReadAllBytesAsync(file);
            Assert.Equal(DummyFileHelper.File16Kb, bytes);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact(Timeout = 60_000)]
    public async Task OldDownloadingFileIsNotResumedWhenSizeIsUnknown()
    {
        // arrange
        string folder = NewFolder();
        string file = Path.Combine(folder, "file.bin");
        await File.WriteAllBytesAsync(file + ".download", new byte[100]);
        string url = DummyFileHelper.GetFileWithoutHeaderUrl("file.bin", DummyFileHelper.FileSize16Kb);
        await using DownloadService service = new(NewConfig(autoResume: true));

        try
        {
            // act
            await service.DownloadFileTaskAsync(url, file);

            // assert
            Assert.Equal(DownloadStatus.Completed, service.Status);
            Assert.Equal(DummyFileHelper.FileSize16Kb, new FileInfo(file).Length);
            byte[] bytes = await File.ReadAllBytesAsync(file);
            Assert.Equal(DummyFileHelper.File16Kb, bytes);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact(Timeout = 60_000)]
    public async Task RangeDownloadFailsWhenServerHasNoRangeSupport()
    {
        // arrange
        DownloadConfiguration config = NewConfig(autoResume: false);
        config.RangeDownload = true;
        config.RangeLow = 0;
        config.RangeHigh = 100;
        string url = DummyFileHelper.GetFileWithNoAcceptRangeUrl("file.bin", DummyFileHelper.FileSize16Kb);
        await using DownloadService service = new(config);
        AsyncCompletedEventArgs completed = null;
        service.DownloadFileCompleted += (_, e) => completed = e;

        // act
        await service.DownloadFileTaskAsync(url);

        // assert
        Assert.Equal(DownloadStatus.Failed, service.Status);
        Assert.IsType<NotSupportedException>(completed?.Error);
    }
}
