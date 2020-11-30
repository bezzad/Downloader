using System;
using System.IO;

namespace Downloader
{
    public static class ObjectHelper
    {
        public static string GetTempFile(this string baseDirectory, string fileExtension = "")
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
                return Path.GetTempFileName();

            if (!Directory.Exists(baseDirectory))
                Directory.CreateDirectory(baseDirectory);

            var filename = Path.Combine(baseDirectory, Guid.NewGuid().ToString("N") + fileExtension);
            File.Create(filename).Close();

            return filename;
        }

        public static string GetFileNameFromUrl(this string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) == false)
                uri = new Uri(new Uri("http://localhost"), url);

            return Path.GetFileName(uri.LocalPath);
        }
    }
}
