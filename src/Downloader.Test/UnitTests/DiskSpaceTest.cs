using Downloader.Extensions;

namespace Downloader.Test.UnitTests;

/// <summary>
/// The disk space check must measure the file system that holds the target folder, not "/".
/// Real case: in a Snap, "/" had 20,159,479,808 bytes free and /home had 145,676,783,616 bytes free;
/// a 21,718,480,960-byte download into ~/Downloads failed with "not enough space".
/// </summary>
public class DiskSpaceTest
{
    private const long SnapRootFree = 20_159_479_808;
    private const long HomeFree = 145_676_783_616;
    private const long BlobSize = 21_718_480_960;
    private const string IncidentPath = "/home/u/Downloads/ai models/qwen3.6";

    private static long FreeOf(Dictionary<string, long> mounts, string path)
    {
        return mounts[FileHelper.FindMount(path, mounts.Keys)];
    }

    [Fact]
    public void IncidentPathUsesTheHomeMount()
    {
        // arrange
        Dictionary<string, long> mounts = new() { ["/"] = SnapRootFree, ["/home"] = HomeFree };

        // act
        string mount = FileHelper.FindMount(IncidentPath, mounts.Keys);
        long free = mounts[mount];

        // assert
        Assert.Equal("/home", mount);
        Assert.Equal(HomeFree, free);
        Assert.Equal(SnapRootFree, mounts["/"]); // the old check measured only "/" (Path.GetPathRoot)
        AssertHelper.DoesNotThrow<IOException>(() =>
            FileHelper.ThrowIfNotEnoughSpace(BlobSize, IncidentPath, mount, free));
    }

    [Fact]
    public void RealShortageThrowsAClearMessage()
    {
        // arrange
        Dictionary<string, long> mounts = new() { ["/"] = SnapRootFree, ["/home"] = 1000 };
        string mount = FileHelper.FindMount(IncidentPath, mounts.Keys);

        // act
        IOException error = Assert.Throws<IOException>(() =>
            FileHelper.ThrowIfNotEnoughSpace(BlobSize, IncidentPath, mount, mounts[mount]));

        // assert
        Assert.Contains("21718480960", error.Message);
        Assert.Contains("1000 bytes free", error.Message);
        Assert.Contains("'/home'", error.Message);
        Assert.Contains(IncidentPath, error.Message);
    }

    [Fact]
    public void LongestPrefixWins()
    {
        Dictionary<string, long> mounts = new() { ["/"] = 1, ["/home"] = 2, ["/home/u/data"] = 3 };

        Assert.Equal(3, FreeOf(mounts, "/home/u/data/x"));
        Assert.Equal(3, FreeOf(mounts, "/home/u/data"));
        Assert.Equal(2, FreeOf(mounts, "/home/u/other"));
    }

    [Fact]
    public void MountMatchesWholeSegmentsOnly()
    {
        Dictionary<string, long> mounts = new() { ["/"] = 1, ["/home"] = 2 };

        Assert.Equal(1, FreeOf(mounts, "/home2/x"));
    }

    [Fact]
    public void NoMatchingMountIsUnknown()
    {
        Assert.Null(FileHelper.FindMount("/data/x", ["/home"]));
    }

    [Fact]
    public void FolderThatDoesNotExistYetUsesItsNearestExistingParent()
    {
        // arrange
        string realTemp = FileHelper.GetRealExistingPath(Path.GetFullPath(DummyFileHelper.TempDirectory));
        string missing = Path.Combine(DummyFileHelper.TempDirectory, Guid.NewGuid().ToString("N"), "a", "b");
        string root = Path.GetPathRoot(realTemp);

        // act
        string real = FileHelper.GetRealExistingPath(Path.GetFullPath(missing));

        // assert
        Assert.Equal(realTemp, real);
        Assert.Equal(realTemp, FileHelper.FindMount(real, [root, realTemp]));
    }

    [Fact]
    public void SymlinkedFolderUsesTheMountOfItsTarget()
    {
        // Creating a symbolic link on Windows needs admin rights or developer mode.
        if (OperatingSystem.IsWindows())
            return;

        // arrange
        string baseDir = Path.Combine(DummyFileHelper.TempDirectory, Guid.NewGuid().ToString("N"));
        string target = Directory.CreateDirectory(Path.Combine(baseDir, "other-mount")).FullName;
        string link = Path.Combine(baseDir, "Downloads");
        Directory.CreateSymbolicLink(link, target);
        try
        {
            string realTarget = FileHelper.GetRealExistingPath(target);
            string realBase = FileHelper.GetRealExistingPath(baseDir);
            string[] mounts = ["/", realBase, realTarget];

            // act
            string real = FileHelper.GetRealExistingPath(Path.Combine(link, "sub", "file.bin"));

            // assert
            Assert.Equal(realTarget, real);
            Assert.Equal(realTarget, FileHelper.FindMount(real, mounts));
        }
        finally
        {
            Directory.Delete(baseDir, recursive: true);
        }
    }

    [Fact]
    public void ResumeNeedsOnlyTheMissingBytes()
    {
        // arrange
        const long received = 21_000_000_000;
        const long free = 1_000_000_000;

        // act
        long neededOnResume = FileHelper.GetNeededBytes(BlobSize, received);
        long neededFresh = FileHelper.GetNeededBytes(BlobSize, 0);

        // assert
        Assert.Equal(BlobSize - received, neededOnResume);
        AssertHelper.DoesNotThrow<IOException>(() =>
            FileHelper.ThrowIfNotEnoughSpace(neededOnResume, IncidentPath, "/home", free));
        Assert.Throws<IOException>(() =>
            FileHelper.ThrowIfNotEnoughSpace(neededFresh, IncidentPath, "/home", free));
    }

    [Fact]
    public void NeededBytesIsNeverNegativeAndZeroForUnknownSize()
    {
        Assert.Equal(0, FileHelper.GetNeededBytes(100, 200));
        Assert.Equal(0, FileHelper.GetNeededBytes(0, 0));
        Assert.Equal(0, FileHelper.GetNeededBytes(-1, 0));
    }

    [Fact]
    public void UnknownSizeOrFreeSpaceIsNotChecked()
    {
        AssertHelper.DoesNotThrow<IOException>(() => FileHelper.ThrowIfNotEnoughSpace(0, IncidentPath));
        AssertHelper.DoesNotThrow<IOException>(() =>
            FileHelper.ThrowIfNotEnoughSpace(BlobSize, IncidentPath, null, 0));
    }

    [Fact]
    public void UncOrEmptyPathIsUnknown()
    {
        Assert.Equal(0, FileHelper.GetAvailableFreeSpaceOnDisk(@"\\server\share\folder\file.bin"));
        Assert.Equal(0, FileHelper.GetAvailableFreeSpaceOnDisk(""));
        AssertHelper.DoesNotThrow<IOException>(() => FileHelper.ThrowIfNotEnoughSpace(long.MaxValue, ""));
    }
}
