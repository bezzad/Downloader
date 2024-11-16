namespace Downloader.Test.UnitTests;

public class DownloadPackageTestOnMemory(ITestOutputHelper output) : DownloadPackageTest(output)
{
    public override async Task InitializeAsync()
    {
        Package = new DownloadPackage {
            Urls = [
                DummyFileHelper.GetFileWithNameUrl(DummyFileHelper.SampleFile16KbName, DummyFileHelper.FileSize16Kb)
            ],
            TotalFileSize = DummyFileHelper.FileSize16Kb
        };

        await base.InitializeAsync();
    }
}