using System;

namespace Downloader.DummyHttpServer
{
    /// <summary>
    /// Class with helper methods to create random data
    /// </summary>
    public static class DummyData
    {
        /// <summary>
        /// Generates random bytes
        /// </summary>
        /// <param name="length">amount of bytes</param>
        public static byte[] GenerateRandomBytes(int length)
        {
            if (length < 1)
                throw new ArgumentException("length has to be > 0");

            Random rand = new Random();
            byte[] buffer = new byte[length];
            rand.NextBytes(buffer);
            return buffer;
        }

        /// <summary>
        /// Generates a Byte-Array with ascending values ([0,1,2,3,...,254,255,0,1,2,...])
        /// </summary>
        /// <param name="length"></param>
        public static byte[] GenerateOrderedBytes(int length)
        {
            if (length < 1)
                throw new ArgumentException("length has to be > 0");

            byte[] buffer = new byte[length];
            for (int i = 0; i < length; i++)
            {
                buffer[i] = (byte)(i % 256);
            }

            return buffer;
        }


        /// <summary>
        /// Fill an array elements by a value
        /// </summary>
        /// <typeparam name="T">value type</typeparam>
        /// <param name="array">an array to filling</param>
        /// <param name="value">a value to set array</param>
        public static void Fill<T>(this T[] array, T value)
        {
            for (int i = 0; i < array.Length; i++)
                array[i] = value;
        }
    }
}
