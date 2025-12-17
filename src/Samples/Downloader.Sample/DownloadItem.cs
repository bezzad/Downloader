using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace Downloader.Sample;

[ExcludeFromCodeCoverage]
public class DownloadItem
{
    public string FolderPath
    {
        get => field ?? Path.GetDirectoryName(FileName);
        set;
    }

    public string FileName { get; set; }
    public string Url { get; set; }
    public string[] Urls { get; set; }
    public bool ValidateData { get; set; }
}