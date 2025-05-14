namespace Downloader.Test.UnitTests;

public class FileHelperTest(ITestOutputHelper output) : BaseTestClass(output)
{
    [Fact]
    public void CreateFileSpecialPathTest()
    {
        // arrange
        string baseUrl = Path.Combine(DummyFileHelper.TempDirectory, "downloader", "test");
        string filename = Path.Combine(baseUrl, Guid.NewGuid().ToString("N") + ".test");

        // act
        FileHelper.CreateFile(filename).Dispose();

        // assert
        Assert.True(File.Exists(filename));

        File.Delete(filename);
    }

    [Fact]
    public void CreateFileNoPathTest()
    {
        // arrange
        string baseUrl = "  ";
        string filename = Path.Combine(baseUrl, Guid.NewGuid().ToString("N") + DummyFileHelper.TempFilesExtension);

        // act
        Stream fileStream = FileHelper.CreateFile(filename);

        // assert
        Assert.Equal(Stream.Null, fileStream);
    }

    [Fact]
    public void GetTempFileSpecialPathTest()
    {
        // arrange
        string baseUrl = Path.Combine(DummyFileHelper.TempDirectory, "downloader", "test");

        // act
        string tempFile = FileHelper.GetTempFile(baseUrl, DummyFileHelper.TempFilesExtension);

        // assert
        Assert.StartsWith(baseUrl, tempFile);

        File.Delete(tempFile);
    }

    [Fact]
    public void GetTempFileNoPathTest()
    {
        // arrange
        string baseUrl = " ";
        string tempFolder = DummyFileHelper.TempDirectory;

        // act
        string tempFile = FileHelper.GetTempFile(baseUrl, DummyFileHelper.TempFilesExtension);

        // assert
        Assert.StartsWith(tempFolder, tempFile);

        File.Delete(tempFile);
    }

    [Fact]
    public void GetTempFileNullPathTest()
    {
        // arrange
        string tempFolder = DummyFileHelper.TempDirectory;

        // act
        string tempFile = FileHelper.GetTempFile(null, string.Empty);

        // assert
        Assert.StartsWith(tempFolder, tempFile);

        File.Delete(tempFile);
    }

    [Fact]
    public void GetTempFileSpecialPathNonDuplicationTest()
    {
        // arrange
        string baseUrl = Path.Combine(DummyFileHelper.TempDirectory, "downloader", "test");

        // act
        string tempFile1 = FileHelper.GetTempFile(baseUrl, DummyFileHelper.TempFilesExtension);
        string tempFile2 = FileHelper.GetTempFile(baseUrl, DummyFileHelper.TempFilesExtension);

        // assert
        Assert.NotEqual(tempFile1, tempFile2);

        File.Delete(tempFile1);
        File.Delete(tempFile2);
    }

    [Fact]
    public void GetTempFileNoPathNonDuplicationTest()
    {
        // arrange
        string baseUrl = "     ";

        // act
        string tempFile1 = FileHelper.GetTempFile(baseUrl, DummyFileHelper.TempFilesExtension);
        string tempFile2 = FileHelper.GetTempFile(baseUrl, DummyFileHelper.TempFilesExtension);

        // assert
        Assert.NotEqual(tempFile1, tempFile2);

        File.Delete(tempFile1);
        File.Delete(tempFile2);
    }

    [Fact]
    public void GetTempFileNullPathNonDuplicationTest()
    {
        // act
        string tempFile1 = FileHelper.GetTempFile(null, string.Empty);
        string tempFile2 = FileHelper.GetTempFile(null, string.Empty);

        // assert
        Assert.NotEqual(tempFile1, tempFile2);

        File.Delete(tempFile1);
        File.Delete(tempFile2);
    }

    [Fact]
    public void GetTempFileSpecialPathCreationTest()
    {
        // arrange
        string baseUrl = Path.Combine(DummyFileHelper.TempDirectory, "downloader", "test");

        // act
        string tempFile = FileHelper.GetTempFile(baseUrl, DummyFileHelper.TempFilesExtension);

        // assert
        Assert.True(File.Exists(tempFile));

        File.Delete(tempFile);
    }

    [Fact]
    public void GetTempFileNullPathCreationTest()
    {
        // act
        string tempFile = FileHelper.GetTempFile(null, string.Empty);

        // assert
        Assert.True(File.Exists(tempFile));

        File.Delete(tempFile);
    }

    [Fact]
    public void GetTempFileNoPathCreationTest()
    {
        // arrange
        string baseUrl = " ";

        // act
        string tempFile = FileHelper.GetTempFile(baseUrl, DummyFileHelper.TempFilesExtension);

        // assert
        Assert.True(File.Exists(tempFile));

        File.Delete(tempFile);
    }

    [Fact]
    public void GetAvailableFreeSpaceOnDiskTest()
    {
        // arrange
        string mainDriveRoot = Path.GetPathRoot(DummyFileHelper.TempDirectory);
        DriveInfo mainDrive = new(mainDriveRoot ?? string.Empty);
        long mainDriveAvailableFreeSpace = mainDrive.AvailableFreeSpace + (100*1024); // + 100MB realtime data changes

        // act
        long availableFreeSpace = FileHelper.GetAvailableFreeSpaceOnDisk(DummyFileHelper.TempDirectory);

        // assert
        Assert.True(availableFreeSpace > 0);
        Assert.True(mainDriveAvailableFreeSpace > availableFreeSpace, 
            $"main space is {mainDriveAvailableFreeSpace} >! available space {availableFreeSpace}");
    }

    [Fact]
    public void GetAvailableFreeSpaceOnDiskWhenUncPathTest()
    {
        // arrange
        string mainDriveRoot = Path.GetPathRoot("\\UNC_Server_1234584456465487981231\\testFolder\\test.test");

        // act
        long availableFreeSpace = FileHelper.GetAvailableFreeSpaceOnDisk(mainDriveRoot);

        // assert
        Assert.Equal(0, availableFreeSpace);
    }

    [Fact]
    public void ThrowIfNotEnoughSpaceTest()
    {
        // arrange
        string mainDriveRoot = Path.GetPathRoot(DummyFileHelper.TempDirectory);

        // act
        void ThrowIfNotEnoughSpaceMethod() => FileHelper.ThrowIfNotEnoughSpace(long.MaxValue, mainDriveRoot);

        // assert
        Assert.ThrowsAny<IOException>(ThrowIfNotEnoughSpaceMethod);
    }

    [Fact]
    public void ThrowIfNotEnoughSpaceWhenIsNullTest()
    {
        // act
        void ThrowIfNotEnoughSpaceMethod() => FileHelper.ThrowIfNotEnoughSpace(long.MaxValue, null);

        // assert
        AssertHelper.DoesNotThrow<IOException>(ThrowIfNotEnoughSpaceMethod);
    }

    [Fact]
    public void ThrowIfNotEnoughSpaceWhenPathIsNullTest()
    {
        // arrange
        string mainDriveRoot = Path.GetPathRoot(DummyFileHelper.TempDirectory);

        // act
        void ThrowIfNotEnoughSpaceMethod() => FileHelper.ThrowIfNotEnoughSpace(1, mainDriveRoot, null);

        // assert
        AssertHelper.DoesNotThrow<IOException>(ThrowIfNotEnoughSpaceMethod);
    }
}
