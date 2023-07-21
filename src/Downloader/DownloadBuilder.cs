using System;
using System.IO;

namespace Downloader
{
    public class DownloadBuilder
    {
        private string url;
        private string directoryPath;
        private string filename;
        private DownloadConfiguration downloadConfiguration;

        public static DownloadBuilder New()
        {
            return new DownloadBuilder();
        }

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
            filename = Path.GetFileName(fullPath);
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

        public DownloadBuilder WithDirectory(string directoryPath)
        {
            this.directoryPath = directoryPath;
            return this;
        }

        public DownloadBuilder WithFolder(Uri folderUri)
        {
            return WithDirectory(folderUri.LocalPath);
        }

        public DownloadBuilder WithFolder(DirectoryInfo folder)
        {
            return WithDirectory(folder.FullName);
        }

        public DownloadBuilder WithFileName(string name)
        {
            this.filename = name;
            return this;
        }

        public DownloadBuilder WithConfiguration(DownloadConfiguration configuration)
        {
            downloadConfiguration = configuration;
            return this;
        }

        public DownloadBuilder Configure(Action<DownloadConfiguration> configure)
        {
            var configuration = new DownloadConfiguration();
            configure(configuration);
            return WithConfiguration(configuration);
        }

        public IDownload Build()
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentNullException($"{nameof(url)} has not been declared.");
            }

            return new Download(url, directoryPath, filename, downloadConfiguration);
        }

        public IDownload Build(DownloadPackage package)
        {
            return new Download(package, url, downloadConfiguration);
        }

        public IDownload Build(DownloadPackage package, DownloadConfiguration downloadConfiguration)
        {
            return new Download(package, url, downloadConfiguration);
        }
    }
}
