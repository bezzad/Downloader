using System;

namespace Downloader.Test
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
        public static byte[] GenerateRandomBytes(int length = 16)
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
        public static byte[] GenerateOrderedBytes(int length = 256)
        {
            if (length < 1)
                throw new ArgumentException("length has to be > 0");

            byte[] buffer = new byte[length];
            for (int i = 0; i < length; i++)
                buffer[i] = (byte)(i % 256);

            return buffer;
        }
    }
}
