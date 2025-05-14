namespace Downloader.Test.UnitTests;

public class DownloadPackageTestOnFile(ITestOutputHelper output) : DownloadPackageTest(output)
{
    private string _path;

    public override async Task InitializeAsync()
    {
        _path = Path.GetTempFileName();

        Package = new DownloadPackage() {
            FileName = _path,
            Urls = [
                DummyFileHelper.GetFileWithNameUrl(DummyFileHelper.SampleFile16KbName, DummyFileHelper.FileSize16Kb)
            ],
            TotalFileSize = DummyFileHelper.FileSize16Kb
        };

        await base.InitializeAsync();
    }

    public override async Task DisposeAsync()
    {
        await base.DisposeAsync();
        File.Delete(_path);
    }

    [Theory]
    [InlineData(true)] // BuildStorageWithReserveSpaceTest
    [InlineData(false)] // BuildStorageTest
    public async Task BuildStorageTest(bool reserveSpace)
    {
        // arrange
        _path = Path.GetTempFileName();
        Package = new DownloadPackage() {
            FileName = _path,
            Urls = [
                DummyFileHelper.GetFileWithNameUrl(DummyFileHelper.SampleFile16KbName, DummyFileHelper.FileSize16Kb)
            ],
            TotalFileSize = DummyFileHelper.FileSize16Kb
        };

        // act
        Package.BuildStorage(reserveSpace, 1024 * 1024);
        await using Stream stream = Package.Storage.OpenRead();

        // assert
        Assert.IsType<FileStream>(stream);
        Assert.Equal(reserveSpace ? DummyFileHelper.FileSize16Kb : 0, Package.Storage.Length);
    }
}