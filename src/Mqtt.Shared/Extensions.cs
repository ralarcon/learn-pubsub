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
        public static byte[] ToUtf8Bytes(this object obj)
        {
            if (obj == null) return new byte[] { };
            return JsonSerializer.SerializeToUtf8Bytes(obj, obj.GetType());
        }


        public static T Deserialize<T>(this byte[] payload) where T : class, new()
        {
            if (payload == null) return new T();

            var result = JsonSerializer.Deserialize<T>(utf8Json: payload)!;
            return result ?? new T();
        }

        public static byte[] ToItemBytes(this Item item)
        {
            if (item == null) return new byte[] { };

            using (MemoryStream stream = new())
            {
                return JsonSerializer.SerializeToUtf8Bytes(item, ItemJsonContext.Default.Item);
            }
        }

        public static Item DeserializeItem(this byte[] payload)
        {
            if (payload == null) return new Item();

            using (MemoryStream stream = new())
            {
                return JsonSerializer.Deserialize(utf8Json: payload, jsonTypeInfo: ItemJsonContext.Default.Item)!;
            }
        }

        public static string ToItemString(this Item item)
        {
            if (item == null) return string.Empty;

            using (MemoryStream stream = new())
            {
                return Encoding.UTF8.GetString(JsonSerializer.SerializeToUtf8Bytes(item, ItemJsonContext.Default.Item));
            }
        }

        public static Item DeserilizeItem(this string payload)
        {
            if (payload == null) return new Item();

            using (MemoryStream stream = new())
            {
                var payloadBytes = Encoding.UTF8.GetBytes(payload);
                return JsonSerializer.Deserialize(utf8Json: payloadBytes, jsonTypeInfo: ItemJsonContext.Default.Item)!;
            }
        }






        public static byte[] ToItemPositionBytes(this ItemPosition item)
        {
            if (item == null) return new byte[] { };

            using (MemoryStream stream = new())
            {
                return JsonSerializer.SerializeToUtf8Bytes(item, ItemPositionJsonContext.Default.ItemPosition);
            }
        }

        public static ItemPosition DeserilizeItemPosition(this byte[] payload)
        {
            if (payload == null) return new ItemPosition();

            using (MemoryStream stream = new())
            {
                return JsonSerializer.Deserialize(utf8Json: payload, jsonTypeInfo: ItemPositionJsonContext.Default.ItemPosition)!;
            }
        }

        public static string ToItemPositionString(this ItemPosition item)
        {
            if (item == null) return string.Empty;

            using (MemoryStream stream = new())
            {
                return Encoding.UTF8.GetString(JsonSerializer.SerializeToUtf8Bytes(item, ItemPositionJsonContext.Default.ItemPosition));
            }
        }

        public static ItemPosition DeserilizeItemPosition(this string payload)
        {
            if (payload == null) return new ItemPosition();

            using (MemoryStream stream = new())
            {
                var payloadBytes = Encoding.UTF8.GetBytes(payload);
                return JsonSerializer.Deserialize(utf8Json: payloadBytes, jsonTypeInfo: ItemPositionJsonContext.Default.ItemPosition)!;
            }
        }
    }
}
