using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using System.Buffers;
using System.Collections.Frozen;
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
        SerializerCache<T, T>.Serializer(ref msgpackWriter, value);
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
        return SerializerCache<T, T>.Deserializer(ref reader);
    }

    /// <summary>
    /// Serializes a value.
    /// </summary>
    /// <param name="writer">The writer that receives the msgpack bytes.</param>
    /// <param name="value">The value to be serialized.</param>
    /// <param name="options">The options to use when serializing.</param>
    /// <typeparam name="T">The type of value to be serialized.</typeparam>
    public static void Serialize<T>(IBufferWriter<byte> writer, in T? value, MessagePackSerializerOptions options)
        where T : IShapeable<T>
    {
        MessagePackWriter msgpackWriter = new(writer);
        SerializerCache<T, T>.Formatter.Serialize(ref msgpackWriter, value, options);
        msgpackWriter.Flush();
    }

    /// <summary>
    /// Deserializes msgpack into a value of a particular type.
    /// </summary>
    /// <param name="sequence">The msgpack to deserialize.</param>
    /// <param name="options">The options to use when deserializing.</param>
    /// <typeparam name="T">The type of value to deserialize.</typeparam>
    /// <returns>The deserialized value.</returns>
    public static T? Deserialize<T>(ReadOnlySequence<byte> sequence, MessagePackSerializerOptions options)
        where T : IShapeable<T>
    {
        MessagePackReader reader = new(sequence);
        return SerializerCache<T, T>.Formatter.Deserialize(ref reader, options);
    }

    /// <summary>
    /// Serializes a value.
    /// </summary>
    /// <param name="writer">The writer that receives the msgpack bytes.</param>
    /// <param name="value">The value to be serialized.</param>
    /// <param name="options">The options to use when serializing.</param>
    /// <typeparam name="T">The type of value to be serialized.</typeparam>
    /// <typeparam name="TProvider">The type shape provider that enables serialization.</typeparam>
    public static void Serialize<T, TProvider>(IBufferWriter<byte> writer, in T? value, MessagePackSerializerOptions options)
        where TProvider : IShapeable<T>
    {
        MessagePackWriter msgpackWriter = new(writer);
        SerializerCache<T, TProvider>.Formatter.Serialize(ref msgpackWriter, value, options);
        msgpackWriter.Flush();
    }

    /// <summary>
    /// Deserializes msgpack into a value of a particular type.
    /// </summary>
    /// <param name="sequence">The msgpack to deserialize.</param>
    /// <param name="options">The options to use when deserializing.</param>
    /// <typeparam name="T">The type of value to deserialize.</typeparam>
    /// <typeparam name="TProvider">The type shape provider that enables serialization.</typeparam>
    /// <returns>The deserialized value.</returns>
    public static T? Deserialize<T, TProvider>(ReadOnlySequence<byte> sequence, MessagePackSerializerOptions options)
        where TProvider : IShapeable<T>
    {
        MessagePackReader reader = new(sequence);
        return SerializerCache<T, TProvider>.Formatter.Deserialize(ref reader, options);
    }

    private delegate T? Reader<T>(ref MessagePackReader reader);

    private delegate void Writer<T>(ref MessagePackWriter writer, T value);

    private static class SerializerCache<T, TProvider>
        where TProvider : IShapeable<T>
    {
        private static Writer<T?>? serializer;

        private static Reader<T?>? deserializer;

        private static IMessagePackFormatter<T?>? formatter;

        internal static Writer<T?> Serializer => serializer ??= (new SerializeVisitor()).GetWriter(TProvider.GetShape());

        internal static Reader<T?> Deserializer => deserializer ??= (new DeserializeVisitor()).GetReader(TProvider.GetShape());

        internal static IMessagePackFormatter<T?> Formatter => formatter ??= (new FormatterVisitor()).GetFormatter(TProvider.GetShape());
    }

    private sealed class SerializeVisitor : TypeShapeVisitor
    {
        private readonly TypeDictionary formatters = new()
        {
            { typeof(string), new Writer<string>((ref MessagePackWriter writer, string value) => writer.Write(value)) },
            { typeof(int), new Writer<int>((ref MessagePackWriter writer, int value) => writer.Write(value)) }
        };

        internal Writer<T?> GetWriter<T>(ITypeShape<T> typeShape)
        {
            return this.formatters.GetOrAdd<Writer<T?>>(typeShape, this, box => (ref MessagePackWriter writer, T? value) => box.Result(ref writer, value));
        }

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
        private readonly TypeDictionary formatters = new()
        {
            { typeof(string), new Reader<string>((ref MessagePackReader reader) => reader.ReadString()) },
            { typeof(int), new Reader<int>((ref MessagePackReader reader) => reader.ReadInt32()) },
        };

        internal Reader<T?> GetReader<T>(ITypeShape<T> typeShape)
        {
            return formatters.GetOrAdd<Reader<T?>>(typeShape, this, box => (ref MessagePackReader reader) => box.Result(ref reader));
        }

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
                    ReadOnlySpan<byte> stringKey = MessagePack.Internal.CodeGenHelpers.ReadStringSpan(ref reader);
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

    private sealed class FormatterVisitor : TypeShapeVisitor
    {
        private static readonly FrozenDictionary<Type, object> PrimitiveFormatters = new Dictionary<Type, object>()
        {
            { typeof(byte[]), new PrimitiveFormatter<byte[]>((ref MessagePackReader reader) => reader.ReadBytes()?.ToArray(), (ref MessagePackWriter writer, byte[]? value) => writer.Write(value)) },
            { typeof(DateTime), new PrimitiveFormatter<DateTime>((ref MessagePackReader reader) => reader.ReadDateTime(), (ref MessagePackWriter writer, DateTime value) => writer.Write(value)) },
            { typeof(string), new PrimitiveFormatter<string>((ref MessagePackReader reader) => reader.ReadString(), (ref MessagePackWriter writer, string? value) => writer.Write(value)) },
            { typeof(bool), new PrimitiveFormatter<bool>((ref MessagePackReader reader) => reader.ReadBoolean(), (ref MessagePackWriter writer, bool value) => writer.Write(value)) },
            { typeof(char), new PrimitiveFormatter<char>((ref MessagePackReader reader) => reader.ReadChar(), (ref MessagePackWriter writer, char value) => writer.Write(value)) },
            { typeof(sbyte), new PrimitiveFormatter<sbyte>((ref MessagePackReader reader) => reader.ReadSByte(), (ref MessagePackWriter writer, sbyte value) => writer.Write(value)) },
            { typeof(short), new PrimitiveFormatter<short>((ref MessagePackReader reader) => reader.ReadInt16(), (ref MessagePackWriter writer, short value) => writer.Write(value)) },
            { typeof(int), new PrimitiveFormatter<int>((ref MessagePackReader reader) => reader.ReadInt32(), (ref MessagePackWriter writer, int value) => writer.Write(value)) },
            { typeof(long), new PrimitiveFormatter<long>((ref MessagePackReader reader) => reader.ReadInt64(), (ref MessagePackWriter writer, long value) => writer.Write(value)) },
            { typeof(byte), new PrimitiveFormatter<byte>((ref MessagePackReader reader) => reader.ReadByte(), (ref MessagePackWriter writer, byte value) => writer.Write(value)) },
            { typeof(ushort), new PrimitiveFormatter<ushort>((ref MessagePackReader reader) => reader.ReadUInt16(), (ref MessagePackWriter writer, ushort value) => writer.Write(value)) },
            { typeof(uint), new PrimitiveFormatter<uint>((ref MessagePackReader reader) => reader.ReadUInt32(), (ref MessagePackWriter writer, uint value) => writer.Write(value)) },
            { typeof(ulong), new PrimitiveFormatter<ulong>((ref MessagePackReader reader) => reader.ReadUInt64(), (ref MessagePackWriter writer, ulong value) => writer.Write(value)) },
            { typeof(float), new PrimitiveFormatter<float>((ref MessagePackReader reader) => reader.ReadSingle(), (ref MessagePackWriter writer, float value) => writer.Write(value)) },
            { typeof(double), new PrimitiveFormatter<double>((ref MessagePackReader reader) => reader.ReadDouble(), (ref MessagePackWriter writer, double value) => writer.Write(value)) },
        }.ToFrozenDictionary();
        private readonly TypeDictionary formatters = new();

        internal IMessagePackFormatter<T?> GetFormatter<T>(ITypeShape<T> typeShape)
        {
            if (PrimitiveFormatters.TryGetValue(typeShape.Type, out object? formatter))
            {
                return (IMessagePackFormatter<T?>)formatter;
            }

            return formatters.GetOrAdd<IMessagePackFormatter<T?>>(typeShape, this, box => new DelayedFormatter<T?>(box));
        }

        public override object? VisitObject<T>(IObjectTypeShape<T> objectShape, object? state = null)
        {
            List<(byte[] RawPropertyNameString, byte[] PropertyNameUtf8, SerializeProperty<T> Serialize, DeserializeProperty<T> Deserialize)>? properties = new();
            foreach (IPropertyShape property in objectShape.GetProperties())
            {
                if (property.HasGetter)
                {
                    byte[] rawPropertyNameString = MessagePackSerializer.Serialize(property.Name, MessagePackSerializerOptions.Standard);
                    var (s, d) = ((SerializeProperty<T>, DeserializeProperty<T>))property.Accept(this)!;
                    properties.Add((rawPropertyNameString, Encoding.UTF8.GetBytes(property.Name), s, d));
                }
            }

            SpanDictionary<byte, DeserializeProperty<T>> propertyReaders = properties
                .ToSpanDictionary(
                    p => p.PropertyNameUtf8,
                    p => p.Deserialize,
                    ByteSpanEqualityComparer.Ordinal);

            Func<T>? constructor = (Func<T>?)objectShape.GetConstructor()?.Accept(this);
            return new SmartFormatter<T>(propertyReaders, constructor, properties);
        }

        public override object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> propertyShape, object? state = null)
        {
            var formatter = GetFormatter(propertyShape.PropertyType);

            Getter<TDeclaringType, TPropertyType> getter = propertyShape.GetGetter();
            SerializeProperty<TDeclaringType> serialize = (ref TDeclaringType container, ref MessagePackWriter writer, MessagePackSerializerOptions options) =>
            {
                formatter.Serialize(ref writer, getter(ref container), options);
            };

            Setter<TDeclaringType, TPropertyType> setter = propertyShape.GetSetter();
            DeserializeProperty<TDeclaringType> deserialize = (ref TDeclaringType container, ref MessagePackReader reader, MessagePackSerializerOptions options) =>
            {
                setter(ref container, formatter.Deserialize(ref reader, options)!);
            };

            return (serialize, deserialize);
        }

        public override object? VisitConstructor<TDeclaringType, TArgumentState>(IConstructorShape<TDeclaringType, TArgumentState> constructorShape, object? state = null)
        {
            return constructorShape.GetDefaultConstructor();
        }

        public override object? VisitEnum<TEnum, TUnderlying>(IEnumTypeShape<TEnum, TUnderlying> enumShape, object? state = null)
        {
            return new EnumAsStringFormatter<TEnum>();
        }
    }

    private delegate T? FormatDeserialize<T>(ref MessagePackReader reader);
    private delegate void FormatSerialize<T>(ref MessagePackWriter writer, T? value);

    private delegate void SerializeProperty<TDeclaringType>(ref TDeclaringType container, ref MessagePackWriter writer, MessagePackSerializerOptions options);
    private delegate void DeserializeProperty<TDeclaringType>(ref TDeclaringType container, ref MessagePackReader reader, MessagePackSerializerOptions options);


    private sealed class PrimitiveFormatter<T>(FormatDeserialize<T> deserialize, FormatSerialize<T> serialize) : IMessagePackFormatter<T?>
    {
        internal FormatDeserialize<T> DeserializeFunc => deserialize;
        internal FormatSerialize<T> SerializeFunc => serialize;

        public T? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options) => deserialize(ref reader);
        public void Serialize(ref MessagePackWriter writer, T? value, MessagePackSerializerOptions options) => serialize(ref writer, value);
    }

    private sealed class SmartFormatter<T>(SpanDictionary<byte, DeserializeProperty<T>> propertyReaders, Func<T>? constructor, List<(byte[] RawPropertyNameString, byte[] PropertyNameUtf8, SerializeProperty<T> Serialize, DeserializeProperty<T> Deserialize)> properties) : IMessagePackFormatter<T?>
    {
        internal SpanDictionary<byte, DeserializeProperty<T>> PropertyReaders => propertyReaders;

        internal Func<T>? Constructor => constructor;

        internal List<(byte[] RawPropertyNameString, byte[] PropertyNameUtf8, SerializeProperty<T> Serialize, DeserializeProperty<T> Deserialize)> Properties => properties;

        public T? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return default;
            }

            if (constructor is null)
            {
                throw new MessagePackSerializationException($"No constructor for {typeof(T).FullName}");
            }

            int count = reader.ReadMapHeader();
            T result = constructor();

            for (int i = 0; i < count; i++)
            {
                ReadOnlySpan<byte> propertyName = MessagePack.Internal.CodeGenHelpers.ReadStringSpan(ref reader);
                if (propertyReaders.TryGetValue(propertyName, out DeserializeProperty<T>? propertyReader))
                {
                    propertyReader(ref result, ref reader, options);
                }
                else
                {
                    reader.Skip();
                }
            }

            return result;
        }

        public void Serialize(ref MessagePackWriter writer, T? value, MessagePackSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteMapHeader(properties.Count);
            foreach (var property in properties)
            {
                writer.WriteRaw(property.RawPropertyNameString);
                property.Serialize(ref value, ref writer, options);
            }
        }
    }

    private sealed class EnumAsStringFormatter<T> : IMessagePackFormatter<T>
        where T : struct, Enum
    {
        public T Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options) => Enum.Parse<T>(reader.ReadString()!);
        public void Serialize(ref MessagePackWriter writer, T value, MessagePackSerializerOptions options) => writer.Write(value.ToString());
    }

    private sealed class DelayedFormatter<T>(ResultBox<IMessagePackFormatter<T>> self) : IMessagePackFormatter<T>
    {
        public T Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options) => self.Result.Deserialize(ref reader, options);
        public void Serialize(ref MessagePackWriter writer, T value, MessagePackSerializerOptions options) => self.Result.Serialize(ref writer, value, options);
    }
}
