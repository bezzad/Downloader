using System;
using System.Collections.Generic;
using System.Linq;

namespace Downloader
{
    public static class ObjectHelper
    {
        public static bool IsNumber(this object value)
        {
            return value is sbyte
                   || value is byte
                   || value is short
                   || value is ushort
                   || value is int
                   || value is uint
                   || value is long
                   || value is ulong
                   || value is float
                   || value is double
                   || value is decimal;
        }

        /// <summary>Returns a specified number of contiguous elements from the startIndex in a sequence.</summary>
        /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return elements from.</param>
        /// <param name="startIndex">The number of range starting index.</param>
        /// <param name="count">The number of elements to return.</param>
        /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
        /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains the elements that occur after the specified index in the input sequence.</returns>
        /// <exception cref="T:System.ArgumentNullException">
        /// <paramref name="source" /> is <see langword="null" />.</exception>
        public static IEnumerable<TSource> Slice<TSource>(this IEnumerable<TSource> source, int startIndex, int count)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            return source.Skip(startIndex).Take(count);
        }


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
