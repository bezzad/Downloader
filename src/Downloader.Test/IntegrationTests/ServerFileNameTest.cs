using Downloader.Extensions;

namespace Downloader.Test.IntegrationTests;

/// <summary>
/// A file name from the server must never write outside the folder the caller chose.
/// </summary>
public class ServerFileNameTest(ITestOutputHelper output) : BaseTestClass(output)
{
    [Theory(Timeout = 60_000)]
    [InlineData("attachment; filename=\"../evil.txt\"", "evil.txt")]
    [InlineData("attachment; filename=\"../../evil.txt\"", "evil.txt")]
    [InlineData("attachment; filename=\"..\\\\..\\\\evil.txt\"", "evil.txt")]
    [InlineData("attachment; filename=\"/etc/evil\"", "evil")]
    [InlineData("attachment; filename=\"C:\\\\evil.txt\"", "evil.txt")]
    [InlineData("attachment; FileName=\"a.zip\"", "a.zip")]
    [InlineData("attachment; filename*=UTF-8''%E2%82%AC.txt", "€.txt")]
    [InlineData("attachment; filename=\"old.txt\"; filename*=UTF-8''new.txt", "new.txt")]
    [InlineData("attachment; filename=\"a:b?.txt\"", "a_b_.txt")]
    public async Task ServerFileNameStaysInsideTheFolder(string disposition, string expectedName)
    {
        // arrange
        string root = Path.Combine(Path.GetTempPath(), "downloader-name-" + Guid.NewGuid().ToString("N"));
        string folder = Path.Combine(root, "a", "b");
        Directory.CreateDirectory(folder);
        string url = DummyFileHelper.GetFileWithRawContentDispositionUrl(disposition, DummyFileHelper.FileSize1Kb);
        await using DownloadService service = new(new DownloadConfiguration());

        try
        {
            // act
            await service.DownloadFileTaskAsync(url, new DirectoryInfo(folder));

            // assert
            Assert.Equal(DownloadStatus.Completed, service.Status);
            string expectedFile = Path.Combine(folder, expectedName);
            Assert.Equal(expectedFile, service.Package.FileName);
            Assert.True(File.Exists(expectedFile));
            Assert.Single(Directory.GetFiles(root, "*", SearchOption.AllDirectories));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Theory(Timeout = 60_000)]
    [InlineData("attachment; filename=\"..\"")]
    [InlineData("attachment; filename=\".\"")]
    [InlineData("attachment; filename=\"\"")]
    public async Task UselessServerNameFallsBackToTheUrlName(string disposition)
    {
        // arrange
        string url = DummyFileHelper.GetFileWithRawContentDispositionUrl(disposition, DummyFileHelper.FileSize1Kb);
        using SocketClient client = new(new DownloadConfiguration());

        // act
        string name = await client.SetRequestFileNameAsync(new Request(url), CancellationToken.None);

        // assert
        Assert.Equal("disposition", name);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("..", null)]
    [InlineData(".", null)]
    [InlineData("a/..", null)]
    [InlineData("file.txt", "file.txt")]
    [InlineData("../../file.txt", "file.txt")]
    [InlineData("..\\..\\file.txt", "file.txt")]
    [InlineData("/etc/passwd", "passwd")]
    [InlineData("C:\\Windows\\x.exe", "x.exe")]
    [InlineData("a<b>c|d\"e*f.txt", "a_b_c_d_e_f.txt")]
    [InlineData("name\t.txt", "name_.txt")]
    [InlineData("name.txt. . ", "name.txt")]
    [InlineData("  name.txt", "name.txt")]
    public void GetSafeFileNameTest(string name, string expected)
    {
        Assert.Equal(expected, FileHelper.GetSafeFileName(name));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("attachment", null)]
    [InlineData("attachment; filename=", null)]
    [InlineData("attachment; filename=a.zip", "a.zip")]
    [InlineData("attachment; FILENAME=\"a.zip\"", "a.zip")]
    [InlineData("attachment; filename*=UTF-8''%E2%82%AC%20rates.txt", "€ rates.txt")]
    [InlineData("attachment; filename*=utf-8'en'a.txt", "a.txt")]
    [InlineData("attachment; filename*=UTF-8''; filename=\"b.txt\"", "b.txt")]
    public void GetFileNameFromDispositionTest(string disposition, string expected)
    {
        Assert.Equal(expected, SocketClient.GetFileNameFromDisposition(disposition));
    }
}
