using Downloader.Extensions;

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
        // act
        long availableFreeSpace = FileHelper.GetAvailableFreeSpaceOnDisk(@"\\server\UNC_Server_1234584456465487981231\testFolder\test.test");

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
        // act
        void ThrowIfNotEnoughSpaceMethod() => FileHelper.ThrowIfNotEnoughSpace(1, null);

        // assert
        AssertHelper.DoesNotThrow<IOException>(ThrowIfNotEnoughSpaceMethod);
    }

    [Fact]
    public void DeleteFileRetriesUntilTransientLockIsReleasedTest()
    {
        // arrange
        // A sharing violation on delete is a Windows-only OS behavior (POSIX allows unlinking open files).
        if (!OperatingSystem.IsWindows())
            return;

        string filename = Path.Combine(DummyFileHelper.TempDirectory, Guid.NewGuid().ToString("N") + ".test");
        File.WriteAllText(filename, "content");
        FileStream lockingStream = new(filename, FileMode.Open, FileAccess.Read, FileShare.None);
        _ = Task.Run(() =>
        {
            Thread.Sleep(150); // release the lock after the first delete attempt fails
            lockingStream.Dispose();
        });

        // act
        FileHelper.DeleteFile(filename, maxAttempts: 5, initialRetryDelayMs: 100);

        // assert
        Assert.False(File.Exists(filename));
    }

    [Fact]
    public void DeleteFileDefaultBudgetSurvivesASecondsLongLockTest()
    {
        // arrange
        // A sharing violation on delete is a Windows-only OS behavior (POSIX allows unlinking open files).
        if (!OperatingSystem.IsWindows())
            return;

        // A large file under real-time antivirus scanning can stay locked for well over 100ms;
        // the default backoff (100,200,400,800,1600ms) must survive a lock held ~1.1s (issue #239).
        string filename = Path.Combine(DummyFileHelper.TempDirectory, Guid.NewGuid().ToString("N") + ".test");
        File.WriteAllText(filename, "content");
        FileStream lockingStream = new(filename, FileMode.Open, FileAccess.Read, FileShare.None);
        _ = Task.Run(() =>
        {
            Thread.Sleep(1100);
            lockingStream.Dispose();
        });

        // act
        FileHelper.DeleteFile(filename);

        // assert
        Assert.False(File.Exists(filename));
    }

    [Fact]
    public void DeleteFileThrowsWhenLockNeverReleasesTest()
    {
        // arrange
        // A sharing violation on delete is a Windows-only OS behavior (POSIX allows unlinking open files).
        if (!OperatingSystem.IsWindows())
            return;

        string filename = Path.Combine(DummyFileHelper.TempDirectory, Guid.NewGuid().ToString("N") + ".test");
        File.WriteAllText(filename, "content");
        using FileStream lockingStream = new(filename, FileMode.Open, FileAccess.Read, FileShare.None);

        // act
        void DeleteFileMethod() => FileHelper.DeleteFile(filename, maxAttempts: 2, initialRetryDelayMs: 10);

        // assert: the surfaced error must explain the external lock, with the OS error preserved inside
        IOException exception = Assert.ThrowsAny<IOException>(DeleteFileMethod);
        Assert.Contains("remained locked by another process", exception.Message);
        Assert.IsAssignableFrom<IOException>(exception.InnerException);

        File.Delete(filename);
    }
}
