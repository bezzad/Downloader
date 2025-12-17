using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace Downloader.Sample;

[ExcludeFromCodeCoverage]
public class DownloadItem
{
    private string _folderPath;

    public string FolderPath { get => _folderPath ?? Path.GetDirectoryName(FileName); set => _folderPath = value; }
    public string FileName { get; set; }
    public string Url { get; set; }
    public string[] Urls { get; set; }
    public bool ValidateData { get; set; }
}