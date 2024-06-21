using System;
using System.Reflection;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;

namespace BTCPayServer.JsonConverters
{
    public class UriJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(Uri).GetTypeInfo().IsAssignableFrom(objectType.GetTypeInfo());
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            try
            {
                return reader.TokenType == JsonToken.Null ? null :
                       Uri.TryCreate((string)reader.Value, UriKind.Absolute, out var result) ? result :
                       throw new JsonObjectException("Invalid Currency value", reader);
            }
            catch (InvalidCastException)
            {
                throw new JsonObjectException("Invalid Currency value", reader);
            }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value != null)
                writer.WriteValue(((Uri)value).AbsoluteUri);
        }
    }
}
