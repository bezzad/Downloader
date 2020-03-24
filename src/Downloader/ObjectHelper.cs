using System;

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

        public static int PairingFunction(this int x, int y)
        {
            return (int)PairingFunction((long)x, (long)y);
        }

        /// <summary>
        /// Inverting the Cantor pairing function
        /// reference: https://en.wikipedia.org/wiki/Pairing_function
        /// </summary>
        /// <param name="z">is single uniquely number which encoded of x and y natural numbers</param>
        /// <returns>Two natural numbers</returns>
        public static (long x, long y) InvertPairingFunction(this long z)
        {
            // w = x + y
            // t = (w*(w+1)/2) = (w^2 + w)/2
            // z = t + y
            // => W*2 + w - 2t = 0
            // w = (Sqrt(8t+1)-1)/2
            // t <= z < t+(w+1)   =>   t <= t+y < ((w+1)^2+(w+1))/2
            // we get that:
            // w <= (Sqrt(8z+1)-1)/2 < w+1
            // and thus:
            //          w = Floor[(Sqrt(8z+1)-1)/2]
            //          t = (w^2 + w)/2
            //          y = z - t
            //          x = w - y
            //
            var w = (int)Math.Floor((Math.Sqrt(8 * z + 1) - 1) / 2);
            var t = (w * w + w) / 2;
            var y = z - t;
            var x = w - y;
            return (x, y);
        }

        public static (int x, int y) InvertPairingFunction(this int z)
        {
            var (x, y) = InvertPairingFunction((long)z);
            return ((int)x, (int)y);
        }
    }
}
