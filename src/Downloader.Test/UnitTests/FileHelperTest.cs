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
        // act
        long availableFreeSpace = FileHelper.GetAvailableFreeSpaceOnDisk(DummyFileHelper.TempDirectory);

        // assert
        Assert.True(availableFreeSpace > 0);
        if (!OperatingSystem.IsLinux())
            return;

        // On Linux, compare with the free space of the mount that really holds the temp folder,
        // found here on its own from /proc/self/mountinfo (not with Path.GetPathRoot, which is "/").
        string realTemp = FileHelper.GetRealExistingPath(Path.GetFullPath(DummyFileHelper.TempDirectory));
        string mount = File.ReadLines("/proc/self/mountinfo")
            .Select(line => line.Split(' ')[4].Replace("\\040", " "))
            .Where(m => m == "/" || realTemp == m || realTemp.StartsWith(m + "/", StringComparison.Ordinal))
            .MaxBy(m => m.Length);
        long expected = new DriveInfo(mount!).AvailableFreeSpace;
        const long tolerance = 100L * 1024 * 1024; // other programs write while the test runs
        Assert.InRange(availableFreeSpace, expected - tolerance, expected + tolerance);
    }

    [Fact]
    public void GetAvailableFreeSpaceOnDiskWhenPathIsEmptyTest()
    {
        Assert.Equal(0, FileHelper.GetAvailableFreeSpaceOnDisk(""));
        Assert.Equal(0, FileHelper.GetAvailableFreeSpaceOnDisk(null));
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
        // A dedicated thread, not Task.Run: on a busy CI machine a thread-pool task can start
        // after all delete attempts are used up, so the test failed by chance.
        Thread releaser = new(() =>
        {
            Thread.Sleep(150); // release the lock after the first delete attempt fails
            lockingStream.Dispose();
        });
        releaser.Start();

        // act
        FileHelper.DeleteFile(filename, maxAttempts: 5, initialRetryDelayMs: 100);
        releaser.Join();

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
        Thread releaser = new(() =>
        {
            Thread.Sleep(1100);
            lockingStream.Dispose();
        }); // a dedicated thread, so a busy thread pool can not delay the release
        releaser.Start();

        // act
        FileHelper.DeleteFile(filename);
        releaser.Join();

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
        FileStream lockingStream = new(filename, FileMode.Open, FileAccess.Read, FileShare.None);

        try
        {
            // act
            void DeleteFileMethod() => FileHelper.DeleteFile(filename, maxAttempts: 2, initialRetryDelayMs: 10);

            // assert: the surfaced error must explain the external lock, with the OS error preserved inside
            IOException exception = Assert.ThrowsAny<IOException>(DeleteFileMethod);
            Assert.Contains("remained locked by another process", exception.Message);
            Assert.IsAssignableFrom<IOException>(exception.InnerException);
        }
        finally
        {
            // release the lock BEFORE the cleanup delete — on Windows deleting while the
            // stream is still open throws the very sharing violation this test simulates
            lockingStream.Dispose();
            File.Delete(filename);
        }
    }
}
