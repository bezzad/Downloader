using System;
using System.IO;
using System.Reflection;

namespace Downloader
{
    public static class ObjectHelper
    {
        public static Version GetCurrentVersion => Assembly.GetExecutingAssembly()?.GetName().Version;

        public static bool HasSource(this Exception exp, string source)
        {
            var e = exp;
            while (e != null)
            {
                if (string.Equals(e.Source, source, StringComparison.OrdinalIgnoreCase))
                    return true;

                e = e.InnerException;
            }

            return false;
        }

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
    }
}
