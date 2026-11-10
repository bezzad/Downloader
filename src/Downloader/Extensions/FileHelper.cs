using Downloader.Exceptions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Downloader.Extensions;

internal static class FileHelper
{
    /// <summary>
    /// Deletes a file, retrying with exponential backoff on a transient sharing violation (e.g.
    /// an antivirus real-time scan of a freshly-written executable — which can take several
    /// seconds on a large file — or a handle not yet released by the OS) instead of surfacing it
    /// as a fatal download failure. Default budget is ~3.1s across 6 attempts (100ms, 200ms,
    /// 400ms, 800ms, 1600ms between tries) before giving up and letting the IOException through.
    /// (issue #239)
    /// </summary>
    public static void DeleteFile(string filename, int maxAttempts = 6, int initialRetryDelayMs = 100)
    {
        int delayMs = initialRetryDelayMs;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                File.Delete(filename);
                return;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                Thread.Sleep(delayMs);
                delayMs *= 2;
            }
            catch (IOException exp)
            {
                // The lock outlived the whole retry budget, so it is not a transient scan/handle
                // race. Surface a message that points at the external holder — the condition is
                // an OS-level file lock owned by another process, not a downloader defect.
                throw new IOException(
                    $"The file `{filename}` remained locked by another process after " +
                    $"{maxAttempts} delete attempts. This lock is held outside of the " +
                    "downloader (commonly antivirus real-time scanning, a previous unclosed " +
                    "instance, or another program using the file); close the program holding " +
                    "it or exclude the download folder from real-time scanning.", exp);
            }
        }
    }

    private static readonly char[] InvalidFileNameChars = ['<', '>', ':', '"', '|', '?', '*'];

    /// <summary>
    /// Cleans a file name that came from the server, so it can not point outside the download folder:
    /// keeps only the last path part (both '/' and '\' are separators), replaces control and invalid
    /// characters with '_', and trims dots and spaces at the end.
    /// </summary>
    /// <returns>The safe name, or null when nothing useful is left (empty, "." or "..").</returns>
    public static string GetSafeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        string lastPart = name.Replace('\\', '/').Split('/')[^1];
        StringBuilder safeName = new(lastPart.Length);
        foreach (char c in lastPart)
            safeName.Append(char.IsControl(c) || Array.IndexOf(InvalidFileNameChars, c) >= 0 ? '_' : c);

        string result = safeName.ToString().Trim().TrimEnd('.', ' ');
        return result.Length == 0 ? null : result;
    }

    public static Stream CreateFile(string filename)
    {
        string directory = Path.GetDirectoryName(filename);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return Stream.Null;
        }

        if (Directory.Exists(directory) == false)
        {
            Directory.CreateDirectory(directory);
        }

        return new FileStream(filename, FileMode.OpenOrCreate, FileAccess.Write,
            FileShare.ReadWrite | FileShare.Delete);
    }

    public static string GetTempFile(string baseDirectory, string fileExtension)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            baseDirectory = Path.GetTempPath();
        }

        string filename = Path.Combine(baseDirectory, Guid.NewGuid().ToString("N") + fileExtension);
        CreateFile(filename).Dispose();

        return filename;
    }

    /// <summary>
    /// Lists the mount points (roots) of the file systems this process can see. On Linux this reads the
    /// process's own mount table, so inside a Snap or Flatpak sandbox it lists the sandbox mounts.
    /// Tests replace it to feed an exact mount table.
    /// </summary>
    internal static Func<IEnumerable<string>> MountsProvider = GetMounts;

    /// <summary>
    /// Returns the free bytes (for the current user) of the file system mounted at the given root,
    /// or 0 when it is unknown. Tests replace it to feed exact numbers.
    /// </summary>
    internal static Func<string, long> FreeSpaceOf = GetFreeSpaceOfMount;

    /// <summary>
    /// Gets the free bytes of the file system that holds the given file or folder.
    /// </summary>
    /// <param name="path">A file or folder path. It does not have to exist yet.</param>
    /// <returns>The free bytes, or 0 when unknown (empty or UNC path, no matching mount).</returns>
    public static long GetAvailableFreeSpaceOnDisk(string path) => GetFreeSpace(path).Free;

    /// <summary>
    /// Finds the file system that holds the given path and its free bytes.
    /// The path is made absolute, cut back to the nearest folder that exists, and its symbolic links
    /// are followed; then the mount that is the longest whole-segment prefix of that real path wins.
    /// (<see cref="Path.GetPathRoot(string)"/> is always "/" on Unix, so it can not find a separate
    /// /home, a USB drive or a sandbox mount.)
    /// </summary>
    /// <returns>The mount and its free bytes, or (null, 0) when unknown.</returns>
    internal static (string Mount, long Free) GetFreeSpace(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || IsUncPath(path))
            return (null, 0L);

        try
        {
            string realPath = GetRealExistingPath(Path.GetFullPath(path));
            string mount = realPath is null ? null : FindMount(realPath, MountsProvider());
            return mount is null ? (null, 0L) : (mount, FreeSpaceOf(mount));
        }
        catch (Exception exp) when (exp is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return (null, 0L); // unknown, so the space check is skipped
        }
    }

    /// <summary>
    /// Picks the mount that holds <paramref name="realPath"/>: the longest mount root that is a
    /// prefix of the path on whole path segments, so "/home" holds "/home/u" but not "/home2".
    /// </summary>
    /// <returns>The chosen mount root, or null when none matches.</returns>
    internal static string FindMount(string realPath, IEnumerable<string> mounts)
    {
        string best = null;
        foreach (string mount in mounts)
        {
            if (string.IsNullOrEmpty(mount) || (best is not null && mount.Length <= best.Length))
                continue;

            if (IsUnder(realPath, mount))
                best = mount;
        }

        return best;
    }

    /// <summary>
    /// The bytes a download still has to write: the whole size minus what is already on disk
    /// (a resumed download), never below 0. 0 when the size is unknown.
    /// </summary>
    internal static long GetNeededBytes(long totalFileSize, long receivedBytesSize)
    {
        return totalFileSize <= 0 ? 0 : Math.Max(0, totalFileSize - receivedBytesSize);
    }

    /// <summary>
    /// Throws an <see cref="IOException"/> when the file system that holds <paramref name="fileName"/>
    /// has less free space than <paramref name="neededBytes"/>. Does nothing when the free space is unknown.
    /// </summary>
    /// <param name="neededBytes">The bytes still to write (see <see cref="GetNeededBytes"/>).</param>
    /// <param name="fileName">The target file path.</param>
    public static void ThrowIfNotEnoughSpace(long neededBytes, string fileName)
    {
        if (neededBytes <= 0 || string.IsNullOrWhiteSpace(fileName))
            return;

        (string mount, long free) = GetFreeSpace(fileName);
        ThrowIfNotEnoughSpace(neededBytes, fileName, mount, free);
    }

    /// <summary>
    /// Throws an <see cref="IOException"/> when <paramref name="free"/> is known (above 0) and smaller
    /// than <paramref name="neededBytes"/>. The message names the needed bytes, the free bytes and the mount.
    /// </summary>
    internal static void ThrowIfNotEnoughSpace(long neededBytes, string fileName, string mount, long free)
    {
        if (free > 0 && free < neededBytes)
        {
            throw new IOException($"Not enough disk space for '{fileName}': needs {neededBytes} more bytes, " +
                                  $"{free} bytes free on '{mount}'.");
        }
    }

    private static bool IsUncPath(string path)
    {
        return path.StartsWith(@"\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal);
    }

    private static bool IsSeparator(char c) => c == '/' || c == Path.DirectorySeparatorChar;

    private static bool IsUnder(string path, string mount)
    {
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!path.StartsWith(mount, comparison))
            return false;

        // Whole segments only: the mount ends with a separator ("/", "C:\"), or the path ends right
        // after the mount, or the next character of the path is a separator.
        return IsSeparator(mount[^1]) || path.Length == mount.Length || IsSeparator(path[mount.Length]);
    }

    /// <summary>
    /// Cuts the path back to the nearest folder that exists (the target folder may not exist yet),
    /// then follows every symbolic link in it, so a linked ~/Downloads maps to its real mount.
    /// </summary>
    internal static string GetRealExistingPath(string fullPath)
    {
        string existing = fullPath;
        while (existing is not null && !Directory.Exists(existing))
            existing = Path.GetDirectoryName(existing);

        if (existing is null)
            return null;

        string root = Path.GetPathRoot(existing);
        if (string.IsNullOrEmpty(root))
            return null;

        string current = root;
        string[] parts = existing[root.Length..].Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        foreach (string part in parts)
        {
            current = Path.Combine(current, part);
            DirectoryInfo dir = new(current);
            if (dir.LinkTarget is not null)
                current = dir.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? current;
        }

        return current;
    }

    private static IEnumerable<string> GetMounts()
    {
        try
        {
            return DriveInfo.GetDrives().Select(drive => drive.RootDirectory.FullName).ToList();
        }
        catch (Exception exp) when (exp is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static long GetFreeSpaceOfMount(string mount)
    {
        try
        {
            DriveInfo drive = new(mount);
            return drive.IsReady ? drive.AvailableFreeSpace : 0L; // a drive that is not ready is unknown
        }
        catch (Exception exp) when (exp is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return 0L;
        }
    }

    public static bool CheckFileExistPolicy(this DownloadPackage package, FileExistPolicy policy)
    {
        if (string.IsNullOrWhiteSpace(package.FileName))
            return false;
        
        int filenameCounter = 1;
        var filename = package.FileName;

        while (File.Exists(filename))
        {
            if (policy == FileExistPolicy.Exception)
                throw new FileExistException(filename);

            if (policy == FileExistPolicy.Delete)
                DeleteFile(filename);

            if (policy == FileExistPolicy.Rename)
            {
                var dirPath = Path.GetDirectoryName(package.FileName) ?? Path.GetPathRoot(package.FileName);
                filename = Path.Combine(dirPath!, Path.GetFileNameWithoutExtension(package.FileName) + $"({filenameCounter++})" + Path.GetExtension(package.FileName));
                continue;
            }

            if (policy == FileExistPolicy.IgnoreDownload)
                return false; // Ignore and do not download again!    
        }
        package.FileName = filename;
        return true;
    }
}