using PolyType.Abstractions;
using PolyType.Utilities;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PolyType.Examples.JsonSerializer.Converters;

internal sealed class DelayedJsonConverterFactory : IDelayedValueFactory
{
    public DelayedValue Create<T>(ITypeShape<T> _) => new DelayedValue<JsonConverter<T>>(self => new DelayedJsonConverter<T>(self));

    private sealed class DelayedJsonConverter<T>(DelayedValue<JsonConverter<T>> self) : JsonConverter<T>
    {
        public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            self.Result.Read(ref reader, typeToConvert, options);

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
            self.Result.Write(writer, value, options);

        public override T ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            self.Result.ReadAsPropertyName(ref reader, typeToConvert, options);

        public override void WriteAsPropertyName(Utf8JsonWriter writer, [DisallowNull] T value, JsonSerializerOptions options) =>
            self.Result.WriteAsPropertyName(writer, value, options);
    }
}