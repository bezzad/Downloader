using System;

namespace Downloader.Test
{
    /// <summary>
    /// Class with helper methods to create random data
    /// </summary>
    public class DummyData
    {
        /// <summary>
        /// Generates random bytes
        /// </summary>
        /// <param name="length">amount of bytes</param>
        /// <returns></returns>
        public static byte[] GenerateRandomBytes(int length = 16)
        {
            if (length < 1)
                throw new ArgumentException("length has to be > 0");

            Random r = new Random();
            byte[] buf = new byte[length];
            r.NextBytes(buf);
            return buf;
        }

        /// <summary>
        /// Generates a Byte-Array with ascending values ([0,1,2,3,...,254,255,0,1,2,...])
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        public static byte[] GenerateOrderedBytes(int length = 256)
        {
            if (length < 1)
                throw new ArgumentException("length has to be > 0");


            byte[] buf = new byte[length];
            for (int i = 0; i < length; i++)
                buf[i] = (byte)(i % 256);

            return buf;
        }
    }
}
