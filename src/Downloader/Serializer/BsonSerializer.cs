using System.Text;
using System.Text.Json;

namespace Downloader.Serializer;

public class BsonSerializer : IBinarySerializer
{
    private readonly JsonSerializerOptions _bsonOptions = new() { WriteIndented = false };
    
    public byte[] Serialize<T>(T value)
    {
        if (value == null)
            return null;

        string json = JsonSerializer.Serialize(value, _bsonOptions);
        return Encoding.UTF8.GetBytes(json);
    }

    public T Deserialize<T>(byte[] bytes)
    {
        if (bytes?.Length > 0)
        {
            string json = Encoding.UTF8.GetString(bytes);
            
            if (typeof(T) == typeof(string) && !json.StartsWith("\""))
            {
                // ignore deserialize unsupported strings
                return (T)(object)json;
            }
            
            return JsonSerializer.Deserialize<T>(json, _bsonOptions);
        }

        return default;
    }
}