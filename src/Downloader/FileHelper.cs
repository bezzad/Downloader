using System;
using System.IO;

namespace Downloader
{
    public static class FileHelper
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

            return new FileStream(filename, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        }

        public static string GetTempFile(string baseDirectory, string fileExtension = "")
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                baseDirectory = Path.GetTempPath();
            }

            string filename = Path.Combine(baseDirectory, Guid.NewGuid().ToString("N") + fileExtension);
            CreateFile(filename).Dispose();

            return filename;
        }

        public static void CheckDiskSize(string directory, long actualSize)
        {
            DriveInfo drive = new DriveInfo(Directory.GetDirectoryRoot(directory));
            if (drive.IsReady && actualSize >= drive.AvailableFreeSpace)
            {
                throw new IOException($"There is not enough space on the disk `{drive.Name}`");
            }
        }
    }
}