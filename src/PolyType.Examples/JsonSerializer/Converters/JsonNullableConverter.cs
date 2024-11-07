using System.Text.Json;
using System.Text.Json.Serialization;

namespace PolyType.Examples.JsonSerializer.Converters;

internal sealed class JsonNullableConverter<T>(JsonConverter<T> elementConverter) : JsonConverter<T?>
    where T : struct
{
    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.Null)
        {
            return null;
        }

        return elementConverter.Read(ref reader, typeToConvert, options);
    }

    public override void Write(Utf8JsonWriter writer, T? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        elementConverter.Write(writer, value.Value, options);
    }
}
