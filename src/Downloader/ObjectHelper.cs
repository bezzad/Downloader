using System;
using System.Reflection;

namespace Downloader
{
    internal static class ObjectHelper
    {
        /// <summary>
        /// In mathematics, a pairing function is a process to uniquely encode two natural numbers into a single natural number.
        /// Pairing(x, y) = ((x + y) * (x + y + 1) / 2) + y
        /// reference: https://en.wikipedia.org/wiki/Pairing_function
        /// </summary>
        /// <param name="x">is an natural numbers</param>
        /// <param name="y">is an natural numbers</param>
        /// <returns>single uniquely number which encoded of x and y natural numbers</returns>
        internal static long PairingFunction(this long x, long y)
        {
            return (x + y) * (x + y + 1) / 2 + y;
        }

        internal static Version GetCurrentVersion => Assembly.GetExecutingAssembly()?.GetName().Version;
    }
}
