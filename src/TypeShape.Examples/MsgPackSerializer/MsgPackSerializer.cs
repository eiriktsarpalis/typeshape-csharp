using MessagePack;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using TypeShape.Abstractions;
using TypeShape.Examples.Utilities;

namespace TypeShape.Examples.MsgPackSerializer;

/// <summary>
/// A msgpack serializer built on top of TypeShape.
/// </summary>
public static class MsgPackSerializer
{
    /// <summary>
    /// Serializes a value.
    /// </summary>
    /// <param name="writer">The writer that receives the msgpack bytes.</param>
    /// <param name="value">The value to be serialized.</param>
    /// <typeparam name="T">The type of value to be serialized.</typeparam>
    public static void Serialize<T>(IBufferWriter<byte> writer, in T? value)
        where T : IShapeable<T>
    {
        MessagePackWriter msgpackWriter = new(writer);
        SerializerCache<T>.Serializer(ref msgpackWriter, value);
        msgpackWriter.Flush();
    }

    /// <summary>
    /// Deserializes msgpack into a value of a particular type.
    /// </summary>
    /// <param name="sequence">The msgpack to deserialize.</param>
    /// <typeparam name="T">The type of value to deserialize.</typeparam>
    /// <returns>The deserialized value.</returns>
    public static T? Deserialize<T>(ReadOnlySequence<byte> sequence)
        where T : IShapeable<T>
    {
        MessagePackReader reader = new(sequence);
        return SerializerCache<T>.Deserializer(ref reader);
    }

    private delegate T? Reader<T>(ref MessagePackReader reader);

    private delegate void Writer<T>(ref MessagePackWriter writer, T value);

    private static class SerializerCache<T>
        where T : IShapeable<T>
    {
        private static Writer<T?>? serializer;

        private static Reader<T?>? deserializer;

        internal static Writer<T?> Serializer => serializer ??= SerializeVisitor.GetWriter(T.GetShape());

        internal static Reader<T?> Deserializer => deserializer ??= DeserializeVisitor.GetReader(T.GetShape());
    }

    private sealed class SerializeVisitor : TypeShapeVisitor
    {
        internal static readonly SerializeVisitor Instance = new();

        private static readonly TypeDictionary formatters = new()
        {
            { typeof(string), new Writer<string>((ref MessagePackWriter writer, string value) => writer.Write(value)) },
            { typeof(int), new Writer<int>((ref MessagePackWriter writer, int value) => writer.Write(value)) }
        };

        internal static Writer<T?> GetWriter<T>(ITypeShape<T> typeShape)
        {
            return formatters.GetOrAdd<Writer<T?>>(typeShape, Instance, box => (ref MessagePackWriter writer, T? value) => box.Result(ref writer, value));
        }

        private SerializeVisitor() { }

        public override object? VisitObject<T>(IObjectTypeShape<T> objectShape, object? state = null)
        {
            List<(ReadOnlyMemory<byte> Name, Writer<T> Writer)>? properties = new();
            foreach (IPropertyShape property in objectShape.GetProperties())
            {
                if (property.HasGetter/* && property.HasSetter*/)
                {
                    byte[] propertyNameBytes = MessagePackSerializer.Serialize(property.Name, MessagePackSerializerOptions.Standard);
                    properties.Add((propertyNameBytes, (Writer<T>)property.Accept(this, state)!));
                }
            }

            var frozenProperties = properties.ToArray();
            properties = null;

            return new Writer<T?>((ref MessagePackWriter writer, T? value) =>
            {
                if (value is null)
                {
                    writer.WriteNil();
                    return;
                }

                writer.WriteMapHeader(frozenProperties.Length);

                foreach (var property in frozenProperties)
                {
                    writer.WriteRaw(property.Name.Span);
                    property.Writer(ref writer, value);
                }
            });
        }

        public override object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> propertyShape, object? state = null)
        {
            var getter = propertyShape.GetGetter();
            var formatter = GetWriter(propertyShape.PropertyType);
            return new Writer<TDeclaringType>((ref MessagePackWriter writer, TDeclaringType container) => formatter(ref writer, getter(ref container)));
        }
    }

    private sealed class DeserializeVisitor : TypeShapeVisitor
    {
        internal static readonly DeserializeVisitor Instance = new();

        private static readonly TypeDictionary formatters = new()
        {
            { typeof(string), new Reader<string>((ref MessagePackReader reader) => reader.ReadString()) },
            { typeof(int), new Reader<int>((ref MessagePackReader reader) => reader.ReadInt32()) },
        };

        internal static Reader<T?> GetReader<T>(ITypeShape<T> typeShape)
        {
            return formatters.GetOrAdd<Reader<T?>>(typeShape, Instance, box => (ref MessagePackReader reader) => box.Result(ref reader));
        }

        private DeserializeVisitor() { }

        private delegate void PropertySetter<T>(ref MessagePackReader reader, ref T container);

        public override object? VisitObject<T>(IObjectTypeShape<T> objectShape, object? state = null)
        {
            SpanDictionary<byte, PropertySetter<T>> properties = objectShape.GetProperties()
                .Where(p => p.HasSetter)
                .ToSpanDictionary(
                    p => Encoding.UTF8.GetBytes(p.Name),
                    p => (PropertySetter<T>)p.Accept(this, state)!,
                    ByteSpanEqualityComparer.Ordinal);

            Func<T>? factory = (Func<T>?)objectShape.GetConstructor()?.Accept(this, state);
            if (factory is null)
            {
                throw new MessagePackSerializationException("No constructor.");
            }

            return new Reader<T?>((ref MessagePackReader reader) =>
            {
                if (reader.TryReadNil())
                {
                    return default;
                }

                T result = factory();
                int count = reader.ReadMapHeader();

                for (int i = 0; i < count; i++)
                {
                    if (!reader.TryReadStringSpan(out ReadOnlySpan<byte> stringKey))
                    {
                        throw new MessagePackSerializationException("do what AOT formatters do.");
                    }

                    if (properties.TryGetValue(stringKey, out PropertySetter<T>? setter))
                    {
                        setter(ref reader, ref result);
                    }
                    else
                    {
                        reader.Skip();
                    }
                }

                return result;
            });
        }

        public override object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> propertyShape, object? state = null)
        {
            var setter = propertyShape.GetSetter();

            switch (propertyShape.PropertyType.Kind)
            {
                case TypeShapeKind.Object:
                    var formatter = GetReader(propertyShape.PropertyType);
                    return new PropertySetter<TDeclaringType>((ref MessagePackReader reader, ref TDeclaringType container) => setter(ref container, formatter(ref reader)!));
                case TypeShapeKind.Enum:
                    throw new NotImplementedException();
                case TypeShapeKind.Nullable:
                    throw new NotImplementedException();
                case TypeShapeKind.Enumerable:
                    throw new NotImplementedException();
                case TypeShapeKind.Dictionary:
                    throw new NotImplementedException();
                default:
                    throw new NotSupportedException();
            }
        }

        public override object? VisitConstructor<TDeclaringType, TArgumentState>(IConstructorShape<TDeclaringType, TArgumentState> constructorShape, object? state = null)
        {
            return constructorShape.GetDefaultConstructor();
        }
    }
}
