using Downloader.DummyHttpServer;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Downloader.Test.UnitTests;
public class DownloadBuilderTest
{
    // arrange
    private string url;
    private string filename;
    private string folder;
    private string path;

    public DownloadBuilderTest()
    {
        // arrange
        url = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb);
        filename = "test.txt";
        folder = Path.GetTempPath().TrimEnd('\\', '/');
        path = Path.Combine(folder, filename);
    }

    [Fact]
    public void TestCorrect()
    {
        // act
        IDownload download = DownloadBuilder.New()
            .WithUrl(url)
            .WithFileLocation(path)
            .Configure(config => {
                config.ParallelDownload = true;
            })
            .Build();

        // assert
        Assert.Equal(folder, download.Folder);
        Assert.Equal(filename, download.Filename);
    }

    [Fact]
    public void TestSetFolderAndName()
    {
        // act
        IDownload download = DownloadBuilder.New()
            .WithUrl(url)
            .WithDirectory(folder)
            .WithFileName(filename)
            .Build();

        // assert
        Assert.Equal(folder, download.Folder);
        Assert.Equal(filename, download.Filename);
    }

    [Fact]
    public void TestSetFolder()
    {
        // arrange
        var dir = Path.GetTempPath();

        // act
        IDownload download = DownloadBuilder.New()
            .WithUrl(url)
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
            .WithUrl(url)
            .WithFileLocation(path)
            .WithFileName(filename)
            .Build();

        // assert
        Assert.Equal(folder, download.Folder);
        Assert.Equal(filename, download.Filename);
    }

    [Fact]
    public void TestUrlless()
    {
        // act
        Action act = () => DownloadBuilder.New().WithFileLocation(path).Build();

        // assert
        Assert.ThrowsAny<ArgumentNullException>(act);
    }

    [Fact]
    public void TestPathless()
    {
        // act
        IDownload result = DownloadBuilder.New()
            .WithUrl(url)
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
            .WithUrl(url)
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
        DownloadPackage package = new DownloadPackage() {
            Urls = new[] { url },
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
        var pauseCount = 0;
        var downloader = DownloadBuilder.New()
            .WithUrl(url)
            .WithFileLocation(path)
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
        Assert.True(File.Exists(path));

        // clean up
        File.Delete(path);
    }

    [Fact]
    public async Task TestOverwriteFileWithDownloadSameLocation()
    {
        // arrange
        var content = "THIS IS TEST CONTENT WHICH MUST BE OVERWRITE WITH THE DOWNLOADER";
        var downloader = DownloadBuilder.New()
                        .WithUrl(url)
                        .WithFileLocation(path)
                        .Build();

        // act
        await File.WriteAllTextAsync(path, content); // create file
        await downloader.StartAsync(); // overwrite file
        var file = await File.ReadAllTextAsync(path);

        // assert
        Assert.True(downloader.Package?.IsSaveComplete);
        Assert.True(File.Exists(path));
        Assert.False(file.StartsWith(content));
        Assert.Equal(DummyFileHelper.FileSize16Kb, Encoding.ASCII.GetByteCount(file));

        // clean up
        File.Delete(path);
    }

    [Fact]
    public async Task TestOverwriteFileWithDownloadSameFileName()
    {
        // arrange
        var content = "THIS IS TEST CONTENT WHICH MUST BE OVERWRITE WITH THE DOWNLOADER";
        var downloader = DownloadBuilder.New()
                        .WithUrl(url)
                        .WithDirectory(folder)
                        .WithFileName(filename)
                        .Build();

        // act
        await File.WriteAllTextAsync(path, content); // create file
        await downloader.StartAsync(); // overwrite file
        var file = await File.ReadAllTextAsync(path);

        // assert
        Assert.True(downloader.Package?.IsSaveComplete);
        Assert.True(File.Exists(path));
        Assert.False(file.StartsWith(content));
        Assert.Equal(DummyFileHelper.FileSize16Kb, Encoding.ASCII.GetByteCount(file));

        // clean up
        File.Delete(path);
    }
}
