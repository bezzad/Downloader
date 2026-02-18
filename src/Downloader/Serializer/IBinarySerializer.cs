namespace Downloader.Serializer;

public interface IBinarySerializer
{
    /// <summary>
    /// Serialize the specified value.
    /// </summary>
    /// <returns>The serialized bytes</returns>
    /// <param name="value">Value to serialize</param>
    /// <typeparam name="T">The parameter type</typeparam>
    byte[] Serialize<T>(T value);

    /// <summary>
    /// Deserialize the specified bytes.
    /// </summary>
    /// <returns>The deserialized object</returns>
    /// <param name="bytes">The serialized bytes</param>
    /// <typeparam name="T">The return type</typeparam>
    T Deserialize<T>(byte[] bytes);
}
