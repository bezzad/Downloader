using System.IO;

namespace Downloader.Exceptions
{
    public class FileExistException(string filePath): IOException($"The file already exists: {filePath}")
    {
        public string Name { get; } = filePath;
    }
}