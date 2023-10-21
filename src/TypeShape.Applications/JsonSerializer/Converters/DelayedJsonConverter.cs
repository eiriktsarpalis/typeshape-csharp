using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TypeShape.Applications.JsonSerializer.Converters;

internal sealed class DelayedJsonConverter<T>(ResultHolder<JsonConverter<T>> holder) : JsonConverter<T>
{
    public JsonConverter<T> Underlying
    {
        get
        {
            Debug.Assert(holder.Value != null);
            return holder.Value;
        }
    }

    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => Underlying.Read(ref reader, typeToConvert, options);

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        => Underlying.Write(writer, value, options);

    public override T ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => Underlying.ReadAsPropertyName(ref reader, typeToConvert, options);

    public override void WriteAsPropertyName(Utf8JsonWriter writer, [DisallowNull] T value, JsonSerializerOptions options)
        => Underlying.WriteAsPropertyName(writer, value, options);
}