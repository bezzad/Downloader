namespace Downloader.Test.UnitTests;

public class DownloadBuilderTest : BaseTestClass
{
    // arrange
    private string _url;
    private string _filename;
    private string _folder;
    private string _path;

    public DownloadBuilderTest(ITestOutputHelper output) : base(output)
    {
        // arrange
        _url = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb);
        _filename = Path.GetRandomFileName();
        _folder = Path.GetTempPath().TrimEnd('\\', '/');
        _path = Path.Combine(_folder, _filename);
    }

    [Fact]
    public void TestCorrect()
    {
        // act
        IDownload download = DownloadBuilder.New()
            .WithUrl(_url)
            .WithFileLocation(_path)
            .Configure(config => {
                config.ParallelDownload = true;
            })
            .Build();

        // assert
        Assert.Equal(_folder, download.Folder);
        Assert.Equal(_filename, download.Filename);
    }

    [Fact]
    public void TestSetFolderAndName()
    {
        // act
        IDownload download = DownloadBuilder.New()
            .WithUrl(_url)
            .WithDirectory(_folder)
            .WithFileName(_filename)
            .Build();

        // assert
        Assert.Equal(_folder, download.Folder);
        Assert.Equal(_filename, download.Filename);
    }

    [Fact]
    public void TestSetFolder()
    {
        // arrange
        string dir = Path.GetTempPath();

        // act
        IDownload download = DownloadBuilder.New()
            .WithUrl(_url)
            .WithDirectory(dir)
            .Build();

        // assert
        Assert.Equal(dir, download.Folder);
        Assert.Null(download.Filename);
    }

    [Fact]
    public void TestSetName()
    {
        // act
        IDownload download = DownloadBuilder.New()
            .WithUrl(_url)
            .WithFileLocation(_path)
            .WithFileName(_filename)
            .Build();

        // assert
        Assert.Equal(_folder, download.Folder);
        Assert.Equal(_filename, download.Filename);
    }

    [Fact]
    public void TestUrlless()
    {
        // act
        Action act = () => DownloadBuilder.New().WithFileLocation(_path).Build();

        // assert
        Assert.ThrowsAny<ArgumentNullException>(act);
    }

    [Fact]
    public void TestPathless()
    {
        // act
        IDownload result = DownloadBuilder.New()
            .WithUrl(_url)
            .Build();

        // assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task TestPackageWhenNewUrl()
    {
        // arrange
        DownloadPackage beforePackage = null;
        IDownload download = DownloadBuilder.New()
            .WithUrl(_url)
            .Build();

        // act
        beforePackage = download.Package;
        await download.StartAsync();

        // assert
        Assert.NotNull(beforePackage);
        Assert.NotNull(download.Package);
        Assert.Equal(beforePackage, download.Package);
        Assert.True(beforePackage.IsSaveComplete);
    }

    [Fact]
    public async Task TestPackageWhenResume()
    {
        // arrange
        DownloadPackage package = new() {
            Urls = [_url],
            IsSupportDownloadInRange = true
        };
        IDownload download = DownloadBuilder.New().Build(package);
        DownloadPackage beforeStartPackage = download.Package;

        // act
        await download.StartAsync();

        // assert
        Assert.NotNull(beforeStartPackage);
        Assert.NotNull(download.Package);
        Assert.Equal(beforeStartPackage, download.Package);
        Assert.Equal(beforeStartPackage, package);
        Assert.True(package.IsSaveComplete);
    }

    [Fact]
    public async Task TestPauseAndResume()
    {
        // arrange
        int pauseCount = 0;
        IDownload downloader = DownloadBuilder.New()
            .WithUrl(_url)
            .WithFileLocation(_path)
            .Build();

        downloader.DownloadProgressChanged += (s, e) => {
            if (pauseCount < 10)
            {
                downloader.Pause();
                pauseCount++;
                downloader.Resume();
            }
        };

        // act
        await downloader.StartAsync();

        // assert
        Assert.True(downloader.Package?.IsSaveComplete);
        Assert.Equal(10, pauseCount);
        Assert.True(File.Exists(_path));

        // clean up
        File.Delete(_path);
    }

    [Fact]
    public async Task TestOverwriteFileWithDownloadSameLocation()
    {
        // arrange
        string content = "THIS IS TEST CONTENT WHICH MUST BE OVERWRITE WITH THE DOWNLOADER";
        IDownload downloader = DownloadBuilder.New()
            .WithUrl(_url)
            .WithFileLocation(_path)
            .Build();

        // act
        await File.WriteAllTextAsync(_path, content); // create file
        await downloader.StartAsync(); // overwrite file
        string file = await File.ReadAllTextAsync(_path);

        // assert
        Assert.True(downloader.Package?.IsSaveComplete);
        Assert.True(File.Exists(_path));
        Assert.False(file.StartsWith(content));
        Assert.Equal(DummyFileHelper.FileSize16Kb, Encoding.ASCII.GetByteCount(file));

        // clean up
        File.Delete(_path);
    }

    [Fact]
    public async Task TestOverwriteFileWithDownloadSameFileName()
    {
        // arrange
        string content = "THIS IS TEST CONTENT WHICH MUST BE OVERWRITE WITH THE DOWNLOADER";
        IDownload downloader = DownloadBuilder.New()
            .WithUrl(_url)
            .WithDirectory(_folder)
            .WithFileName(_filename)
            .Build();

        // act
        await File.WriteAllTextAsync(_path, content); // create file
        await downloader.StartAsync(); // overwrite file
        string file = await File.ReadAllTextAsync(_path);

        // assert
        Assert.True(downloader.Package?.IsSaveComplete);
        Assert.True(File.Exists(_path));
        Assert.False(file.StartsWith(content));
        Assert.Equal(DummyFileHelper.FileSize16Kb, Encoding.ASCII.GetByteCount(file));

        // clean up
        File.Delete(_path);
    }
}