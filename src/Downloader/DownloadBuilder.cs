using System;
using System.IO;

namespace Downloader;

public class DownloadBuilder
{
    private string _url;
    private string _directoryPath;
    private string _filename;
    private DownloadConfiguration _downloadConfiguration;

    public static DownloadBuilder New()
    {
        return new DownloadBuilder();
    }

    public DownloadBuilder WithUrl(string url)
    {
        this._url = url;
        return this;
    }

    public DownloadBuilder WithUrl(Uri url)
    {
        return WithUrl(url.AbsoluteUri);
    }

    public DownloadBuilder WithFileLocation(string fullPath)
    {
        fullPath = Path.GetFullPath(fullPath);
        _filename = Path.GetFileName(fullPath);
        _directoryPath = Path.GetDirectoryName(fullPath);
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
        this._directoryPath = directoryPath;
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
        this._filename = name;
        return this;
    }

    public DownloadBuilder WithConfiguration(DownloadConfiguration configuration)
    {
        _downloadConfiguration = configuration;
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
        if (string.IsNullOrWhiteSpace(_url))
        {
            throw new ArgumentNullException($"{nameof(_url)} has not been declared.");
        }

        return new Download(_url, _directoryPath, _filename, _downloadConfiguration);
    }

    public IDownload Build(DownloadPackage package)
    {
        return new Download(package, _url, _downloadConfiguration);
    }

    public IDownload Build(DownloadPackage package, DownloadConfiguration downloadConfiguration)
    {
        return new Download(package, _url, downloadConfiguration);
    }
}
