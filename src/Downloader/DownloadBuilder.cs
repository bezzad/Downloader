using System;
using System.IO;

namespace Downloader
{
    public class DownloadBuilder
    {
        public static DownloadBuilder New()
        {
            DownloadBuilder builder = new();
            return builder;
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

        private string url;
        private string directoryPath;
        private string name;
        private DownloadConfiguration downloadConfiguration;

        private DownloadBuilder() { }

        public DownloadBuilder WithUrl(string url)
        {
            this.url = url;
            return this;
        }

        public DownloadBuilder WithUrl(Uri url)
        {
            return WithUrl(url.AbsoluteUri);
        }

        public DownloadBuilder WithFileLocation(string fullPath)
        {
            fullPath = Path.GetFullPath(fullPath);
            name = Path.GetFileName(fullPath);
            directoryPath = Path.GetDirectoryName(fullPath);
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

        public DownloadBuilder WithFolder(string directoryPath)
        {
            this.directoryPath = directoryPath;
            return this;
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
            this.name = name;
            return this;
        }

        public DownloadBuilder WithConfiguration(DownloadConfiguration configuration)
        {
            downloadConfiguration = configuration;
            return this;
        }

        public DownloadBuilder Configure(Action<DownloadConfiguration> configure)
        {
            DownloadConfiguration configuration = new();
            configure(configuration);
            return WithConfiguration(configuration);
        }

        public IDownload Build()
        {
            if (url is null)
            {
                throw new ArgumentNullException($"{nameof(url)} has not been declared.");
            }

            if (directoryPath is null)
            {
                throw new ArgumentNullException($"{nameof(directoryPath)} has not been declared.");
            }

            return new Download(url, Path.Combine(directoryPath, name), downloadConfiguration);
        }
    }
}
