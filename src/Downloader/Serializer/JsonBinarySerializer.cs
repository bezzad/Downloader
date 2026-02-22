using System;
using System.Text;
using System.Text.Json;

namespace Downloader.Serializer;

public class JsonBinarySerializer : IBinarySerializer
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = false };

    public byte[] Serialize<T>(T value)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        string json = JsonSerializer.Serialize(value, _jsonOptions);
        return Encoding.UTF8.GetBytes(json);
    }

    public T Deserialize<T>(byte[] bytes, int offset = 0, int count = -1)
    {
        if (bytes == null)
            throw new ArgumentNullException(nameof(bytes));

        if (bytes.Length == 0)
            return default;

        if (count == -1)
            count = bytes.Length - offset;

        string json = Encoding.UTF8.GetString(bytes, offset, count);
        if (typeof(T) == typeof(string) && !json.StartsWith("\""))
        {
            // ignore deserialize unsupported strings
            return (T)(object)json;
        }

        return JsonSerializer.Deserialize<T>(json, _jsonOptions);
    }
}