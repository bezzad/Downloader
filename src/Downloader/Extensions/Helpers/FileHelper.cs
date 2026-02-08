using Downloader.Exceptions;
using System;
using System.IO;

namespace Downloader.Extensions.Helpers;

internal static class FileHelper
{
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
                File.Delete(filename);

            if (policy == FileExistPolicy.Rename)
            {
                var dirPath = Path.GetDirectoryName(package.FileName) ?? Path.GetPathRoot(package.FileName);
                filename = Path.Combine(dirPath!, Path.GetFileNameWithoutExtension(package.FileName) + $"({filenameCounter++})" + Path.GetExtension(package.FileName));
                continue;
            }

            if (policy == FileExistPolicy.IgnoreDownload)
                return false; // Ignore and don't download again!    
        }
        package.FileName = filename;
        return true;
    }
}