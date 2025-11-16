using System.IO;

namespace Downloader.Exceptions
{
    public class FileExistException(string filePath): IOException
    {
        public string Name { get; } = filePath;
    }
}