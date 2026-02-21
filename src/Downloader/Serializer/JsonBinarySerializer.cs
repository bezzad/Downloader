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

    public T Deserialize<T>(byte[] bytes)
    {
        if (bytes == null)
            throw new ArgumentNullException(nameof(bytes));

        if (bytes.Length == 0)
            return default;

        string json = Encoding.UTF8.GetString(bytes);
        if (typeof(T) == typeof(string) && !json.StartsWith("\""))
        {
            // ignore deserialize unsupported strings
            return (T)(object)json;
        }

        return JsonSerializer.Deserialize<T>(json, _jsonOptions);
    }
}