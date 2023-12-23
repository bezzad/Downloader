using System.IO;

namespace Downloader.Sample;

public class DownloadItem
{
    public string _folderPath;

    public string FolderPath { get => _folderPath ?? Path.GetDirectoryName(FileName); set => _folderPath = value; }
    public string FileName { get; set; }
    public string Url { get; set; }
    public bool ValidateData { get; set; }
}