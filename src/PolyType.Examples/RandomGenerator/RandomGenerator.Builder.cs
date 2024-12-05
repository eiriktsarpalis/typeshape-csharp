using PolyType.Abstractions;
using PolyType.Examples.Utilities;
using PolyType.Utilities;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Numerics;
using System.Text;

namespace PolyType.Examples.RandomGenerator;

public partial class RandomGenerator
{
    private delegate void RandomPropertySetter<T>(ref T value, Random random, int size);

    private sealed class Builder(TypeGenerationContext generationContext) : ITypeShapeVisitor, ITypeShapeFunc
    {
        private static readonly Dictionary<Type, (object Generator, RandomGenerator<object?> BoxingGenerator)> s_defaultGenerators = CreateDefaultGenerators().ToDictionary();

        public RandomGenerator<T> GetOrAddGenerator<T>(ITypeShape<T> type) => (RandomGenerator<T>)generationContext.GetOrAdd(type)!;

        object? ITypeShapeFunc.Invoke<T>(ITypeShape<T> typeShape, object? _)
        {
            if (s_defaultGenerators.TryGetValue(typeShape.Type, out var entry))
            {
                return (RandomGenerator<T>)entry.Generator;
            }

            return typeShape.Accept(this);
        }

        public object? VisitObject<T>(IObjectTypeShape<T> type, object? state)
        {
            if (typeof(T) == typeof(object))
            {
                return CreateObjectGenerator();
            }
            
            IConstructorShape? constructor = type.GetConstructor();
            return constructor is null
                ? throw new NotSupportedException($"Type '{typeof(T)}' does not support random generation.")
                : constructor.Accept(this);
        }

        public object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> property, object? state)
        {
            Setter<TDeclaringType, TPropertyType> setter = property.GetSetter();
            RandomGenerator<TPropertyType> propertyGenerator = GetOrAddGenerator(property.PropertyType);
            return new RandomPropertySetter<TDeclaringType>((ref TDeclaringType obj, Random random, int size) => setter(ref obj, propertyGenerator(random, size)));
        }

        public object? VisitConstructor<TDeclaringType, TArgumentState>(IConstructorShape<TDeclaringType, TArgumentState> constructor, object? state)
        {
            if (constructor.ParameterCount == 0)
            {
                Func<TDeclaringType> defaultCtor = constructor.GetDefaultConstructor();
                RandomPropertySetter<TDeclaringType>[] propertySetters = constructor.DeclaringType.GetProperties()
                    .Where(prop => prop.HasSetter)
                    .Select(prop => (RandomPropertySetter<TDeclaringType>)prop.Accept(this)!)
                    .ToArray();

                return new RandomGenerator<TDeclaringType>((Random random, int size) =>
                {
                    if (size == 0)
                    {
                        return default!;
                    }

                    TDeclaringType obj = defaultCtor();
                    int propertySize = GetChildSize(size, propertySetters.Length);

                    foreach (var propertySetter in propertySetters)
                    {
                        propertySetter(ref obj, random, propertySize);
                    }

                    return obj;
                });
            }
            else
            {
                Func<TArgumentState> argumentStateCtor = constructor.GetArgumentStateConstructor();
                Constructor<TArgumentState, TDeclaringType> ctor = constructor.GetParameterizedConstructor();
                RandomPropertySetter<TArgumentState>[] parameterSetters = constructor.GetParameters()
                    .Select(param => (RandomPropertySetter<TArgumentState>)param.Accept(this)!)
                    .ToArray();

                return new RandomGenerator<TDeclaringType>((Random random, int size) =>
                {
                    if (size == 0)
                    {
                        return default!;
                    }

                    TArgumentState argState = argumentStateCtor();
                    int propertySize = GetChildSize(size, parameterSetters.Length);

                    foreach (var parameterSetter in parameterSetters)
                    {
                        parameterSetter(ref argState, random, propertySize);
                    }

                    return ctor(ref argState);
                });
            }
        }

        public object? VisitConstructorParameter<TArgumentState, TParameter>(IConstructorParameterShape<TArgumentState, TParameter> parameter, object? state)
        {
            Setter<TArgumentState, TParameter> setter = parameter.GetSetter();
            RandomGenerator<TParameter> parameterGenerator = GetOrAddGenerator(parameter.ParameterType);
            return new RandomPropertySetter<TArgumentState>((ref TArgumentState obj, Random random, int size) => setter(ref obj, parameterGenerator(random, size)));
        }

        public object? VisitEnum<TEnum, TUnderlying>(IEnumTypeShape<TEnum, TUnderlying> enumShape, object? state) where TEnum: struct, Enum
        {
#if NET
            TEnum[] values = Enum.GetValues<TEnum>();
#else
            TEnum[] values = (TEnum[])Enum.GetValues(typeof(TEnum));
#endif
            return new RandomGenerator<TEnum>((Random random, int _) => values[random.Next(0, values.Length)]);
        }

        public object? VisitNullable<T>(INullableTypeShape<T> nullableShape, object? state) where T : struct
        {
            RandomGenerator<T> elementGenerator = GetOrAddGenerator(nullableShape.ElementType);
            return new RandomGenerator<T?>((Random random, int size) => NextBoolean(random) ? null : elementGenerator(random, size - 1));
        }

        public object? VisitEnumerable<TEnumerable, TElement>(IEnumerableTypeShape<TEnumerable, TElement> enumerableShape, object? state)
        {
            RandomGenerator<TElement> elementGenerator = GetOrAddGenerator(enumerableShape.ElementType);

            if (typeof(TEnumerable).IsArray)
            {
                if (typeof(TEnumerable) != typeof(TElement[]))
                {
                    throw new NotImplementedException("Multi-dimensional array support.");
                }

                return new RandomGenerator<TElement[]>((Random random, int size) =>
                {
                    if (size == 0)
                    {
                        return default!;
                    }

                    int length = random.Next(0, size);
                    var array = new TElement[length];
                    int elementSize = GetChildSize(size, length);

                    for (int i = 0; i < length; i++)
                    {
                        array[i] = elementGenerator(random, elementSize);
                    }

                    return array;
                });
            }

            switch (enumerableShape.ConstructionStrategy)
            {
                case CollectionConstructionStrategy.Mutable:
                    Func<TEnumerable> defaultCtor = enumerableShape.GetDefaultConstructor();
                    Setter<TEnumerable, TElement> addElementFunc = enumerableShape.GetAddElement();
                    return new RandomGenerator<TEnumerable>((Random random, int size) =>
                    {
                        if (size == 0)
                        {
                            return default!;
                        }

                        TEnumerable obj = defaultCtor();
                        int length = random.Next(0, size);
                        int elementSize = GetChildSize(size, length);

                        for (int i = 0; i < length; i++)
                        {
                            addElementFunc(ref obj, elementGenerator(random, elementSize));
                        }

                        return obj;
                    });

                case CollectionConstructionStrategy.Enumerable:
                    Func<IEnumerable<TElement>, TEnumerable> enumerableCtor = enumerableShape.GetEnumerableConstructor();
                    return new RandomGenerator<TEnumerable>((Random random, int size) =>
                    {
                        if (size == 0)
                        {
                            return default!;
                        }

                        int length = random.Next(0, size);
                        using var buffer = new PooledList<TElement>(length);
                        int elementSize = GetChildSize(size, length);

                        for (int i = 0; i < length; i++)
                        {
                            buffer.Add(elementGenerator(random, elementSize));
                        }

                        return enumerableCtor(buffer.ExchangeToArraySegment());
                    });

                case CollectionConstructionStrategy.Span:
                    SpanConstructor<TElement, TEnumerable> spanCtor = enumerableShape.GetSpanConstructor();
                    return new RandomGenerator<TEnumerable>((Random random, int size) =>
                    {
                        if (size == 0)
                        {
                            return default!;
                        }

                        int length = random.Next(0, size);
                        using var buffer = new PooledList<TElement>(length);
                        int elementSize = GetChildSize(size, length);

                        for (int i = 0; i < length; i++)
                        {
                            buffer.Add(elementGenerator(random, elementSize));
                        }

                        return spanCtor(buffer.AsSpan());
                    });

                default:
                    throw new NotSupportedException($"Type '{typeof(TEnumerable)}' does not support random generation.");
            }
        }

        public object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryTypeShape<TDictionary, TKey, TValue> dictionaryShape, object? state) where TKey : notnull
        {
            RandomGenerator<TKey> keyGenerator = GetOrAddGenerator(dictionaryShape.KeyType);
            RandomGenerator<TValue> valueGenerator = GetOrAddGenerator(dictionaryShape.ValueType);

            switch (dictionaryShape.ConstructionStrategy)
            {
                case CollectionConstructionStrategy.Mutable:
                    Func<TDictionary> defaultCtorFunc = dictionaryShape.GetDefaultConstructor();
                    Setter<TDictionary, KeyValuePair<TKey, TValue>> addKeyValuePairFunc = dictionaryShape.GetAddKeyValuePair();
                    return new RandomGenerator<TDictionary>((Random random, int size) =>
                    {
                        if (size == 0)
                        {
                            return default!;
                        }

                        TDictionary obj = defaultCtorFunc();
                        int count = random.Next(0, size);
                        int entrySize = GetChildSize(size, count);

                        for (int i = 0; i < count; i++)
                        {
                            addKeyValuePairFunc(ref obj,
                                new(keyGenerator(random, entrySize),
                                    valueGenerator(random, entrySize)));
                        }

                        return obj;
                    });

                case CollectionConstructionStrategy.Enumerable:
                    Func<IEnumerable<KeyValuePair<TKey, TValue>>, TDictionary> enumerableCtorFunc = dictionaryShape.GetEnumerableConstructor();
                    return new RandomGenerator<TDictionary>((Random random, int size) =>
                    {
                        if (size == 0)
                        {
                            return default!;
                        }

                        Dictionary<TKey, TValue> buffer = new(size);
                        int count = random.Next(0, size);
                        int entrySize = GetChildSize(size, count);

                        for (int i = 0; i < count; i++)
                        {
                            buffer[keyGenerator(random, entrySize)] = valueGenerator(random, entrySize);
                        }

                        return enumerableCtorFunc(buffer);
                    });

                case CollectionConstructionStrategy.Span:
                    SpanConstructor<KeyValuePair<TKey, TValue>, TDictionary> spanCtorFunc = dictionaryShape.GetSpanConstructor();
                    return new RandomGenerator<TDictionary>((Random random, int size) =>
                    {
                        if (size == 0)
                        {
                            return default!;
                        }

                        using var buffer = new PooledList<KeyValuePair<TKey, TValue>>(size);
                        int entrySize = GetChildSize(size, size);

                        for (int i = 0; i < size; i++)
                        {
                            buffer.Add(new(keyGenerator(random, entrySize), valueGenerator(random, entrySize)));
                        }

                        return spanCtorFunc(buffer.AsSpan());
                    });

                default:
                    throw new NotSupportedException($"Type '{typeof(TDictionary)}' does not support random generation.");
            }
        }

        private static RandomGenerator<object?> CreateObjectGenerator()
        {
            RandomGenerator<object?>[] defaultGenerators = s_defaultGenerators.Select(kv => kv.Value.BoxingGenerator).ToArray();
            return new RandomGenerator<object?>((Random random, int size) =>
            {
                int index = random.Next(minValue: -1, defaultGenerators.Length);
                return index == -1 ? null : defaultGenerators[index](random, size);
            });
        }

        private static IEnumerable<KeyValuePair<Type, (object Generator, RandomGenerator<object?> BoxingGenerator)>> CreateDefaultGenerators()
        {
            yield return Create((random, _) => NextBoolean(random));

            yield return Create((random, _) => (byte)random.Next(0, byte.MaxValue));
            yield return Create((random, _) => (ushort)random.Next(0, ushort.MaxValue));
            yield return Create((random, _) => (char)random.Next(0, char.MaxValue));
            yield return Create((random, _) => (uint)random.Next());
            yield return Create((random, _) => NextULong(random));

            yield return Create((random, _) => (sbyte)random.Next(sbyte.MinValue, sbyte.MaxValue));
            yield return Create((random, _) => (short)random.Next(short.MinValue, short.MaxValue));
            yield return Create((random, _) => random.Next());
            yield return Create((random, _) => NextLong(random));

            yield return Create((random, _) => new BigInteger(NextLong(random)));
            yield return Create((random, size) => (float)((random.NextDouble() - 0.5) * size));
            yield return Create((random, size) => (random.NextDouble() - 0.5) * size);
            yield return Create((random, size) => (decimal)((random.NextDouble() - 0.5) * size));

            yield return Create((random, _) => new TimeSpan(NextLong(random)));
            yield return Create((random, _) => new DateTime(NextLong(random, DateTime.MinValue.Ticks, DateTime.MaxValue.Ticks)));
#if NET
            yield return Create((random, size) => (Half)((random.NextDouble() - 0.5) * size));
            yield return Create((random, _) => new UInt128(NextULong(random), NextULong(random)));
            yield return Create((random, _) => new Int128(NextULong(random), NextULong(random)));
            yield return Create((random, _) => new TimeOnly(NextLong(random, 0, TimeOnly.MaxValue.Ticks)));
            yield return Create((random, _) => DateOnly.FromDateTime(new(NextLong(random, DateTime.MinValue.Ticks, DateTime.MaxValue.Ticks))));
            yield return Create((random, _) => new Rune((char)random.Next(0, char.MaxValue)));
#endif
            yield return Create((random, _) =>
            {
                const long MaxOffsetTicks = 14 * TimeSpan.TicksPerHour;
                long dateTicks = NextLong(random, DateTime.MinValue.Ticks + MaxOffsetTicks, DateTime.MaxValue.Ticks - MaxOffsetTicks);
                long offsetTicks = NextLong(random, -MaxOffsetTicks, MaxOffsetTicks);
                return new DateTimeOffset(dateTicks, new TimeSpan(offsetTicks));
            });
            yield return Create((random, size) => new Uri($"https://github.com/{WebUtility.UrlEncode(NextString(random, size))}"));
            yield return Create((random, _) => new Version(random.Next(), random.Next(), random.Next(), random.Next()));
            yield return Create((random, _) =>
            {
#if NET
                Span<byte> buffer = stackalloc byte[16];
#else
                byte[] buffer = new byte[16];
#endif
                random.NextBytes(buffer);
                return new Guid(buffer);
            });

            yield return Create((random, size) =>
            {
                byte[] bytes = new byte[random.Next(0, Math.Max(7, size))];
                random.NextBytes(bytes);
                return bytes;
            });

            yield return Create(NextString);

            yield return Create<object>((random, size) =>
            {
                return random.Next(0, 5) switch
                {
                    0 => NextBoolean(random),
                    1 => random.Next(-size, size),
                    2 => (random.NextDouble() - 0.5) * size,
                    3 => NextString(random, size),
                    _ => new TimeSpan(NextLong(random)),
                };
            });

            static KeyValuePair<Type, (object Generator, RandomGenerator<object?> BoxingGenerator)> Create<T>(RandomGenerator<T> randomGenerator)
                => new(typeof(T), (randomGenerator, (r,i) => randomGenerator(r,i)));
        }

        private static long NextLong(Random random)
        {
#if NET
            Span<byte> bytes = stackalloc byte[8];
#else
            byte[] bytes = new byte[8];
#endif
            random.NextBytes(bytes);
            return BinaryPrimitives.ReadInt64LittleEndian(bytes);
        }

        private static ulong NextULong(Random random)
        {
#if NET
            Span<byte> bytes = stackalloc byte[8];
#else
            byte[] bytes = new byte[8];
#endif
            random.NextBytes(bytes);
            return BinaryPrimitives.ReadUInt64LittleEndian(bytes);
        }

        private static long NextLong(Random random, long min, long max) =>
#if NET
            Math.Clamp(NextLong(random), min, max);
#else
            Math.Min(Math.Max(NextLong(random), min), max);
#endif
        private static bool NextBoolean(Random random) => random.Next(0, 2) != 0;
        private static string NextString(Random random, int size)
        {
            int length = random.Next(0, Math.Max(7, size));
#if NET
            return string.Create(length, random, Populate);
#else
            char[] buffer = new char[length];
            Populate(buffer, random);
            return new string(buffer);
#endif
            static void Populate(Span<char> chars, Random random)
            {
                const string CharPool = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
                for (int i = 0; i < chars.Length; i++)
                {
                    chars[i] = CharPool[random.Next(0, CharPool.Length)];
                }
            }
        }

        private static int GetChildSize(int parentSize, int totalChildren)
        {
            Debug.Assert(parentSize > 0 && totalChildren >= 0);
            return totalChildren switch
            {
                0 => 0,
                1 => parentSize - 1,
                _ => (int)Math.Round(parentSize / (double)totalChildren),
            };
        }
    }

    private sealed class DelayedRandomGeneratorFactory : IDelayedValueFactory
    {
        public DelayedValue Create<T>(ITypeShape<T> typeShape) =>
            new DelayedValue<RandomGenerator<T>>(self => (r, t) => self.Result(r, t));
    }
}
