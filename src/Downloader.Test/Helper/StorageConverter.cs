using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace Downloader.Test.Helper
{
    public class StorageConverter : JsonConverter<IStorage>
    {
        public override void WriteJson(JsonWriter writer, IStorage value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override IStorage ReadJson(JsonReader reader, Type objectType, IStorage existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            var obj = JObject.Load(reader); // Throws an exception if the current token is not an object.
            if (obj.ContainsKey(nameof(FileStorage.FileName)))
            {
                var filename = obj[nameof(FileStorage.FileName)]?.Value<string>();
                return new FileStorage(filename);
            }

            if (obj.ContainsKey(nameof(MemoryStorage.Data)))
            {
                var data = obj[nameof(MemoryStorage.Data)]?.Value<string>();
                return new MemoryStorage() { Data = data };
            }

            return null;
        }
    }
}
