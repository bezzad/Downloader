using System;
using System.IO;
using System.Reflection;

namespace Downloader
{
    public static class ObjectHelper
    {
        /// <summary>
        /// In mathematics, a pairing function is a process to uniquely encode two natural numbers into a single natural number.
        /// Pairing(x, y) = ((x + y) * (x + y + 1) / 2) + y
        /// reference: https://en.wikipedia.org/wiki/Pairing_function
        /// </summary>
        /// <param name="x">is an natural numbers</param>
        /// <param name="y">is an natural numbers</param>
        /// <returns>single uniquely number which encoded of x and y natural numbers</returns>
        public static long PairingFunction(this long x, long y)
        {
            return (x + y) * (x + y + 1) / 2 + y;
        }

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

        public static string GetTempFile(this string baseDirectory)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
                return Path.GetTempFileName();
            
            if (!Directory.Exists(baseDirectory))
                Directory.CreateDirectory(baseDirectory);

            var filename = Path.Combine(baseDirectory, Guid.NewGuid().ToString("N"));
            File.Create(filename).Close();

            return filename;
        }
    }
}
