using Downloader.Exceptions;
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
        private string fullPath;
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
            var name = Path.GetFileName(fullPath ?? url ?? string.Empty);
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
                string.Empty;

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
            configure(configuration);
            return WithConfiguration(configuration);
        }

        public IDownload Build()
        {
            if (url is null)
            {
                throw new DownloadFactoryException("URL has not been declared.");
            }

            if (fullPath is null)
            {
                throw new DownloadFactoryException("File path has not been declared.");
            }

            return new Download(url, fullPath, downloadConfiguration);
        }
    }
}
