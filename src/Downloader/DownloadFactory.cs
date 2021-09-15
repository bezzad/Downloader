using System.Runtime.InteropServices;
using System;
using System.IO;

namespace Downloader
{
    public class DownloadBuilder
    {
        private static readonly Guid downloadFolderGuid = new("{374DE290-123F-4565-9164-39C4925E467B}");

        [DllImport("Shell32.dll")]
        private static extern int SHGetKnownFolderPath(
            [MarshalAs(UnmanagedType.LPStruct)] Guid rfid,
            uint dwFlags,
            IntPtr hToken,
            out IntPtr ppszPath);

        private static string GetDownloadFolderPath()
        {
            var result = SHGetKnownFolderPath(
                downloadFolderGuid,
                0x00004000,
                IntPtr.Zero,
                out IntPtr output);
            if (result >= 0)
            {
                var downloadPath = Marshal.PtrToStringUni(output);
                Marshal.FreeCoTaskMem(output);
                return downloadPath;
            }
            else
            {
                throw new Exception();
            }
        }

        public static DownloadBuilder New(string url)
        {
            DownloadBuilder builder = new(url);
            return builder;
        }

        public static DownloadBuilder New(Uri uri)
        {
            return New(uri.AbsoluteUri);
        }

        public static IDownload Build(DownloadPackage package)
        {
            return Build(package, new DownloadConfiguration());
        }
        public static IDownload Build(
            DownloadPackage package,
            DownloadConfiguration downloadConfiguration)
        {
            return new Download(package, downloadConfiguration);
        }


        private readonly string url;
        private string fullPath;
        private DownloadConfiguration downloadConfiguration;

        private DownloadBuilder(string url) { this.url = url; }

        public DownloadBuilder WithFileLocation(string path)
        {
            fullPath = Path.GetFullPath(path);
            return this;
        }

        public DownloadBuilder WithFileLocation(Uri uri)
        {
            return WithFileLocation(uri.LocalPath);
        }

        public DownloadBuilder WithFileLocation(FileInfo fileInfo)
        {
            return WithFileLocation(fileInfo.FullName);
        }

        public DownloadBuilder WithFolder(string folderPath)
        {
            var name = Path.GetFileName(fullPath ?? url);
            var path = Path.Combine(folderPath, name);
            return WithFileLocation(path);
        }

        public DownloadBuilder WithFolder(Uri folderUri)
        {
            return WithFolder(folderUri.LocalPath);
        }

        public DownloadBuilder WithFolder(DirectoryInfo folder)
        {
            return WithFolder(folder.FullName);
        }

        public DownloadBuilder WithFileName(string name)
        {
            string folderPath =
                fullPath is not null ?
                Path.GetDirectoryName(fullPath) :
                GetDownloadFolderPath()
                ;

            return WithFileLocation(Path.Combine(folderPath, name));
        }

        public DownloadBuilder WithConfiguration(DownloadConfiguration configuration)
        {
            downloadConfiguration = configuration;
            return this;
        }

        public DownloadBuilder Configure(Action<DownloadConfiguration> configure)
        {
            DownloadConfiguration configuration = new();
            configure(downloadConfiguration);
            return WithConfiguration(configuration);
        }

        public IDownload Build()
        {
            if (fullPath is null)
            {
                WithFolder(GetDownloadFolderPath());
            }

            if (downloadConfiguration is null)
            {
                WithConfiguration(new DownloadConfiguration());
            }

            return new Download(url, fullPath, downloadConfiguration);
        }
    }
}
