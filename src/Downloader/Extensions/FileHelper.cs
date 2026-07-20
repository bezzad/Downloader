using Downloader.Exceptions;
using System;
using System.IO;
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

    public static long GetAvailableFreeSpaceOnDisk(string directory)
    {
        if (string.IsNullOrEmpty(directory))
            throw new ArgumentException("Path is null or empty", nameof(directory));

        try
        {
            // Get the root of the filesystem containing the path
            string root = Path.GetPathRoot(directory);

            if (string.IsNullOrEmpty(root)) // UNC (\\server\share) paths not supported.
                return 0L;

            DriveInfo drive = new(root);

            return drive.AvailableFreeSpace; // bytes available to the current user
        }
        catch (ArgumentException)
        {
            // null or use UNC (\\server\share) paths not supported.
            return 0L;
        }
    }

    public static void ThrowIfNotEnoughSpace(long actualNeededSize, string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            return;

        long availableFreeSpace = GetAvailableFreeSpaceOnDisk(directory);
        if (availableFreeSpace > 0 && availableFreeSpace < actualNeededSize)
        {
            throw new IOException($"There is not enough space on the disk `{directory}` " +
                                  $"with {availableFreeSpace} bytes");
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