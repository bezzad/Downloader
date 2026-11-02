using System;
using System.Buffers.Binary;

namespace Downloader;

/// <summary>
/// The resume state saved after the file data in a <c>.download</c> file:
/// an 8-byte length (little-endian) and then the JSON. An older, longer state can stay on disk
/// behind it; the length says where the current one ends.
/// </summary>
internal static class ResumeMetadata
{
    private const int LengthSize = sizeof(long);

    internal static byte[] Pack(byte[] json)
    {
        byte[] result = new byte[LengthSize + json.Length];
        BinaryPrimitives.WriteInt64LittleEndian(result, json.Length);
        json.CopyTo(result, LengthSize);
        return result;
    }

    /// <summary>
    /// Finds the JSON in the bytes after the file data. Files from older versions have only the JSON.
    /// </summary>
    internal static (int Offset, int Count) Unpack(ReadOnlySpan<byte> tail)
    {
        if (tail.Length > LengthSize)
        {
            long length = BinaryPrimitives.ReadInt64LittleEndian(tail);
            if (length > 0 && length <= tail.Length - LengthSize)
                return (LengthSize, (int)length);
        }

        return (0, tail.Length); // old format: the JSON fills the whole tail
    }
}
