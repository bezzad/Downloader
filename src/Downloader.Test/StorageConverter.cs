using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Downloader.Test
{
    public class StorageConverter : JsonConverter<IStorage>
    {
        public override void WriteJson(JsonWriter writer, IStorage value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override IStorage ReadJson(JsonReader reader, Type objectType, IStorage existingValue, bool hasExistingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            var obj = JObject.Load(reader); // Throws an exception if the current token is not an object.
            if (obj.ContainsKey(nameof(FileStorage.FileName)))
            {
                var filename = obj[nameof(FileStorage.FileName)]?.Value<string>();
                return new FileStorage(filename);
            }
            else
            {
                return new MemoryStorage();
            }
        }
    }
}
