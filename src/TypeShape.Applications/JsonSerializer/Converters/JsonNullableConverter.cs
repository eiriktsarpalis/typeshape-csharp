using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TypeShape.Applications.JsonSerializer.Converters;

internal sealed class JsonNullableConverter<T> : JsonConverter<T?>
    where T : struct
{
    public JsonConverter<T>? ElementConverter { get; set; }

    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        Debug.Assert(ElementConverter != null);

        if (reader.TokenType is JsonTokenType.Null)
            return null;

        return ElementConverter.Read(ref reader, typeToConvert, options);
    }

    public override void Write(Utf8JsonWriter writer, T? value, JsonSerializerOptions options)
    {
        Debug.Assert(ElementConverter != null);

        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        ElementConverter.Write(writer, value.Value, options);
    }
}
