using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Mqtt.Shared
{
    public static class Extensions
    {
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.General)
        {
            Converters = { new JsonStringEnumConverter() },
        };

        public static async Task<string> ToJsonStringAsync(this object obj)
        {
            if (obj == null) return await Task.FromResult(string.Empty);

            using (MemoryStream stream = new())
            {
                await JsonSerializer.SerializeAsync(stream, obj, obj.GetType(), _jsonOptions);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
        public static async Task<T> DeserializeAsync<T>(this string payload) where T : class, new()
        {
            if (string.IsNullOrEmpty(payload)) return await Task.FromResult(new T());

            using (MemoryStream stream = new(Encoding.UTF8.GetBytes(payload)))
            {
                var result = await JsonSerializer.DeserializeAsync<T>(stream, _jsonOptions);
                return result ?? new T();
            }
        }
        public static async Task<byte[]> ToJsonByteArrayAsync(this object obj)
        {
            if (obj == null) return await Task.FromResult(new byte[] { });

            using (MemoryStream stream = new())
            {
                await JsonSerializer.SerializeAsync(stream, obj, obj.GetType(), _jsonOptions);
                return stream.ToArray();
            }
        }
        public static async Task<T> DeserializeAsync<T>(this byte[] payload) where T : class, new()
        {
            if (payload == null) return await Task.FromResult(new T());

            using (MemoryStream stream = new(payload))
            {
                var result = await JsonSerializer.DeserializeAsync<T>(stream, _jsonOptions);
                return result ?? new T();
            }
        }

    }
}
