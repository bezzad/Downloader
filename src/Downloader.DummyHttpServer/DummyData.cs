using System;
using System.Diagnostics.CodeAnalysis;

namespace Downloader.DummyHttpServer;

/// <summary>
/// Class with helper methods to create random data
/// </summary>
[ExcludeFromCodeCoverage]
public static class DummyData
{
    private static readonly Random Rand = new(DateTime.Now.GetHashCode());

    /// <summary>
    /// Generates random bytes
    /// </summary>
    /// <param name="length">amount of bytes</param>
    public static byte[] GenerateRandomBytes(int length)
    {
        if (length < 1)
            throw new ArgumentException("length has to be > 0");

        byte[] buffer = new byte[length];
        Rand.NextBytes(buffer);
        return buffer;
    }

    /// <summary>
    /// Generates a Byte-Array with ascending values ([0,1,2,3,...,254,255,0,1,2,...])
    /// </summary>
    /// <param name="length">amount of bytes</param>
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
    /// Generates a Byte-Array with filling of special byte
    /// </summary>
    /// <param name="length">amount of bytes</param>
    public static byte[] GenerateSingleBytes(int length, byte fillByte)
    {
        if (length < 1)
            throw new ArgumentException("length has to be > 0");

        byte[] buffer = new byte[length];
        Array.Fill(buffer, fillByte);

        return buffer;
    }
}
