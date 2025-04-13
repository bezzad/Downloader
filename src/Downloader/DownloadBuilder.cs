using System;
using System.IO;

namespace Downloader;

/// <summary>
/// A builder class for configuring and creating download instances.
/// </summary>
public class DownloadBuilder
{
    private string _url;
    private string _directoryPath;
    private string _filename;
    private DownloadConfiguration _downloadConfiguration;

    /// <summary>
    /// Creates a new instance of the <see cref="DownloadBuilder"/> class.
    /// </summary>
    /// <returns>A new instance of the <see cref="DownloadBuilder"/> class.</returns>
    public static DownloadBuilder New()
    {
        return new DownloadBuilder();
    }

    /// <summary>
    /// Sets the URL for the download.
    /// </summary>
    /// <param name="url">The URL to download from.</param>
    /// <returns>The current <see cref="DownloadBuilder"/> instance.</returns>
    public DownloadBuilder WithUrl(string url)
    {
        this._url = url;
        return this;
    }

    /// <summary>
    /// Sets the URL for the download using a <see cref="Uri"/> object.
    /// </summary>
    /// <param name="url">The URL to download from.</param>
    /// <returns>The current <see cref="DownloadBuilder"/> instance.</returns>
    public DownloadBuilder WithUrl(Uri url)
    {
        return WithUrl(url.AbsoluteUri);
    }

    /// <summary>
    /// Sets the file location for the download.
    /// </summary>
    /// <param name="fullPath">The full path where the file will be saved.</param>
    /// <returns>The current <see cref="DownloadBuilder"/> instance.</returns>
    public DownloadBuilder WithFileLocation(string fullPath)
    {
        fullPath = Path.GetFullPath(fullPath);
        _filename = Path.GetFileName(fullPath);
        _directoryPath = Path.GetDirectoryName(fullPath);
        return this;
    }

    /// <summary>
    /// Sets the file location for the download using a <see cref="Uri"/> object.
    /// </summary>
    /// <param name="uri">The URI representing the file location.</param>
    /// <returns>The current <see cref="DownloadBuilder"/> instance.</returns>
    public DownloadBuilder WithFileLocation(Uri uri)
    {
        return WithFileLocation(uri.LocalPath);
    }

    /// <summary>
    /// Sets the file location for the download using a <see cref="FileInfo"/> object.
    /// </summary>
    /// <param name="fileInfo">The file information representing the file location.</param>
    /// <returns>The current <see cref="DownloadBuilder"/> instance.</returns>
    public DownloadBuilder WithFileLocation(FileInfo fileInfo)
    {
        return WithFileLocation(fileInfo.FullName);
    }

    /// <summary>
    /// Sets the directory path for the download.
    /// </summary>
    /// <param name="directoryPath">The directory path where the file will be saved.</param>
    /// <returns>The current <see cref="DownloadBuilder"/> instance.</returns>
    public DownloadBuilder WithDirectory(string directoryPath)
    {
        this._directoryPath = directoryPath;
        return this;
    }

    /// <summary>
    /// Sets the directory path for the download using a <see cref="Uri"/> object.
    /// </summary>
    /// <param name="folderUri">The URI representing the directory path.</param>
    /// <returns>The current <see cref="DownloadBuilder"/> instance.</returns>
    public DownloadBuilder WithFolder(Uri folderUri)
    {
        return WithDirectory(folderUri.LocalPath);
    }

    /// <summary>
    /// Sets the directory path for the download using a <see cref="DirectoryInfo"/> object.
    /// </summary>
    /// <param name="folder">The directory information representing the directory path.</param>
    /// <returns>The current <see cref="DownloadBuilder"/> instance.</returns>
    public DownloadBuilder WithFolder(DirectoryInfo folder)
    {
        return WithDirectory(folder.FullName);
    }

    /// <summary>
    /// Sets the file name for the download.
    /// </summary>
    /// <param name="name">The name of the file to be saved.</param>
    /// <returns>The current <see cref="DownloadBuilder"/> instance.</returns>
    public DownloadBuilder WithFileName(string name)
    {
        this._filename = name;
        return this;
    }

    /// <summary>
    /// Sets the configuration for the download.
    /// </summary>
    /// <param name="configuration">The download configuration.</param>
    /// <returns>The current <see cref="DownloadBuilder"/> instance.</returns>
    public DownloadBuilder WithConfiguration(DownloadConfiguration configuration)
    {
        _downloadConfiguration = configuration;
        return this;
    }

    /// <summary>
    /// Configures the download with the specified configuration action.
    /// </summary>
    /// <param name="configure">The action to configure the download.</param>
    /// <returns>The current <see cref="DownloadBuilder"/> instance.</returns>
    public DownloadBuilder Configure(Action<DownloadConfiguration> configure)
    {
        DownloadConfiguration configuration = new();
        configure(configuration);
        return WithConfiguration(configuration);
    }

    /// <summary>
    /// Builds and returns a new <see cref="IDownload"/> instance with the configured settings.
    /// </summary>
    /// <returns>A new <see cref="IDownload"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the URL has not been declared.</exception>
    public IDownload Build()
    {
        if (string.IsNullOrWhiteSpace(_url))
        {
            throw new ArgumentNullException($"{nameof(_url)} has not been declared.");
        }

        return new Download(_url, _directoryPath, _filename, _downloadConfiguration);
    }

    /// <summary>
    /// Builds and returns a new <see cref="IDownload"/> instance with the specified package.
    /// </summary>
    /// <param name="package">The download package.</param>
    /// <returns>A new <see cref="IDownload"/> instance.</returns>
    public IDownload Build(DownloadPackage package)
    {
        return new Download(package, _url, _downloadConfiguration);
    }

    /// <summary>
    /// Builds and returns a new <see cref="IDownload"/> instance with the specified package and configuration.
    /// </summary>
    /// <param name="package">The download package.</param>
    /// <param name="downloadConfiguration">The download configuration.</param>
    /// <returns>A new <see cref="IDownload"/> instance.</returns>
    public IDownload Build(DownloadPackage package, DownloadConfiguration downloadConfiguration)
    {
        return new Download(package, _url, downloadConfiguration);
    }
}