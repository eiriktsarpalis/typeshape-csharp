using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Numerics;
using System.Text;

namespace TypeShape.Applications.RandomGenerator;

public partial class RandomGenerator
{
    private delegate void RandomPropertySetter<T>(ref T value, Random random, int size);

    private sealed class Visitor : ITypeShapeVisitor
    {
        private readonly TypeCache _cache = new(CreateDefaultGenerators());

        public object? VisitType<T>(ITypeShape<T> type, object? state)
        {
            if (TryGetCachedResult<T>() is { } result)
            {
                return result;
            }

            switch (type.Kind)
            {
                case TypeKind.Enum:
                    return type.GetEnumShape().Accept(this, null);
                case TypeKind.Nullable:
                    return type.GetNullableShape().Accept(this, null);
                case TypeKind.Dictionary:
                    return type.GetDictionaryShape().Accept(this, null);
                case TypeKind.Enumerable:
                    return type.GetEnumerableShape().Accept(this, null);
                default:
                    // Prefer the default constructor, if available.
                    IConstructorShape? constructor = type.GetConstructors(includeProperties: true, includeFields: true)
                        .MinBy(ctor => ctor.ParameterCount);

                    return constructor is null
                        ? throw new NotSupportedException($"Type '{typeof(T)}' does not support random generation.")
                        : constructor.Accept(this, null);
            }
        }

        public object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> property, object? state)
        {
            Setter<TDeclaringType, TPropertyType> setter = property.GetSetter();
            RandomGenerator<TPropertyType> propertyGenerator = (RandomGenerator<TPropertyType>)property.PropertyType.Accept(this, null)!;
            return new RandomPropertySetter<TDeclaringType>((ref TDeclaringType obj, Random random, int size) => setter(ref obj, propertyGenerator(random, size)));
        }

        public object? VisitConstructor<TDeclaringType, TArgumentState>(IConstructorShape<TDeclaringType, TArgumentState> constructor, object? state)
        {
            if (constructor.ParameterCount == 0)
            {
                Func<TDeclaringType> defaultCtor = constructor.GetDefaultConstructor();
                RandomPropertySetter<TDeclaringType>[] propertySetters = constructor.DeclaringType.GetProperties(includeFields: true)
                    .Where(prop => prop.HasSetter)
                    .Select(prop => (RandomPropertySetter<TDeclaringType>)prop.Accept(this, null)!)
                    .ToArray();

                return CacheResult((Random random, int size) =>
                {
                    if (size == 0) 
                        return default!;

                    TDeclaringType obj = defaultCtor();
                    int propertySize = GetChildSize(size, propertySetters.Length);

                    foreach (var propertySetter in propertySetters)
                        propertySetter(ref obj, random, propertySize);

                    return obj;
                });
            }
            else
            {
                Func<TArgumentState> argumentStateCtor = constructor.GetArgumentStateConstructor();
                Constructor<TArgumentState, TDeclaringType> ctor = constructor.GetParameterizedConstructor();
                RandomPropertySetter<TArgumentState>[] parameterSetters = constructor.GetParameters()
                    .Select(param => (RandomPropertySetter<TArgumentState>)param.Accept(this, null)!)
                    .ToArray();

                return CacheResult((Random random, int size) =>
                {
                    if (size == 0)
                        return default!;

                    TArgumentState argState = argumentStateCtor();
                    int propertySize = GetChildSize(size, parameterSetters.Length);

                    foreach (var parameterSetter in parameterSetters)
                        parameterSetter(ref argState, random, propertySize);

                    return ctor(ref argState);
                });
            }
        }

        public object? VisitConstructorParameter<TArgumentState, TParameter>(IConstructorParameterShape<TArgumentState, TParameter> parameter, object? state)
        {
            Setter<TArgumentState, TParameter> setter = parameter.GetSetter();
            RandomGenerator<TParameter> propertyGenerator = (RandomGenerator<TParameter>)parameter.ParameterType.Accept(this, null)!;
            return new RandomPropertySetter<TArgumentState>((ref TArgumentState obj, Random random, int size) => setter(ref obj, propertyGenerator(random, size)));
        }

        public object? VisitEnum<TEnum, TUnderlying>(IEnumShape<TEnum, TUnderlying> enumType, object? state)
            where TEnum : struct, Enum
        {
            TEnum[] values = Enum.GetValues<TEnum>();
            return CacheResult((Random random, int _) => values[random.Next(0, values.Length)]);
        }

        public object? VisitNullable<T>(INullableShape<T> nullableShape, object? state)
            where T : struct
        {
            var underlyingGenerator = (RandomGenerator<T>)nullableShape.ElementType.Accept(this, null)!;
            return CacheResult<T?>((Random random, int size) => NextBoolean(random) ? null : underlyingGenerator(random, size - 1));
        }

        public object? VisitEnumerable<TEnumerable, TElement>(IEnumerableShape<TEnumerable, TElement> enumerableShape, object? state)
        {
            var elementGenerator = (RandomGenerator<TElement>)enumerableShape.ElementType.Accept(this, null)!;

            if (typeof(TEnumerable).IsArray)
            {
                if (typeof(TEnumerable) != typeof(TElement[]))
                {
                    throw new NotImplementedException("Multi-dimensional array support.");
                }

                return CacheResult((Random random, int size) =>
                {
                    int length = random.Next(0, size);
                    var array = new TElement[length];
                    int elementSize = GetChildSize(size, length);

                    for (int i = 0; i < length; i++)
                        array[i] = elementGenerator(random, size);

                    return array;
                });
            }

            switch (enumerableShape.ConstructionStrategy)
            {
                case CollectionConstructionStrategy.Mutable:
                    Func<TEnumerable> defaultCtor = enumerableShape.GetDefaultConstructor();
                    Setter<TEnumerable, TElement> addElementFunc = enumerableShape.GetAddElement();
                    return CacheResult((Random random, int size) =>
                    {
                        if (size == 0)
                            return default!;

                        TEnumerable obj = defaultCtor();
                        int length = random.Next(0, size);
                        int elementSize = GetChildSize(size, length);

                        for (int i = 0; i < length; i++)
                            addElementFunc(ref obj, elementGenerator(random, elementSize));

                        return obj;
                    });

                case CollectionConstructionStrategy.Enumerable:
                    Func<IEnumerable<TElement>, TEnumerable> enumerableCtor = enumerableShape.GetEnumerableConstructor();
                    return CacheResult((Random random, int size) =>
                    {
                        if (size == 0)
                            return default!;

                        int length = random.Next(0, size);
                        TElement[] buffer = new TElement[length];
                        int elementSize = GetChildSize(size, length);

                        for (int i = 0; i < length; i++)
                            buffer[i] = elementGenerator(random, elementSize);

                        return enumerableCtor(buffer);
                    });

                case CollectionConstructionStrategy.Span:
                    SpanConstructor<TElement, TEnumerable> spanCtor = enumerableShape.GetSpanConstructor();
                    return CacheResult((Random random, int size) =>
                    {
                        if (size == 0)
                            return default!;

                        int length = random.Next(0, size);
                        TElement[] buffer = new TElement[length];
                        int elementSize = GetChildSize(size, length);

                        for (int i = 0; i < length; i++)
                            buffer[i] = elementGenerator(random, elementSize);

                        return spanCtor(buffer);
                    });

                default:
                    throw new NotSupportedException($"Type '{typeof(TEnumerable)}' does not support random generation.");
            }
        }

        public object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryShape<TDictionary, TKey, TValue> dictionaryShape, object? state)
            where TKey : notnull
        {
            var keyGenerator = (RandomGenerator<TKey>)dictionaryShape.KeyType.Accept(this, null)!;
            var valueGenerator = (RandomGenerator<TValue>)dictionaryShape.ValueType.Accept(this, null)!;

            switch (dictionaryShape.ConstructionStrategy)
            {
                case CollectionConstructionStrategy.Mutable:
                    Func<TDictionary> defaultCtorFunc = dictionaryShape.GetDefaultConstructor();
                    Setter<TDictionary, KeyValuePair<TKey, TValue>> addKeyValuePairFunc = dictionaryShape.GetAddKeyValuePair();
                    return CacheResult((Random random, int size) =>
                    {
                        if (size == 0)
                            return default!;

                        TDictionary obj = defaultCtorFunc();
                        int count = random.Next(0, size);
                        int entrySize = GetChildSize(size, count);

                        for (int i = 0; i < count; i++)
                            addKeyValuePairFunc(ref obj,
                                new(keyGenerator(random, entrySize),
                                    valueGenerator(random, entrySize)));

                        return obj;
                    });

                case CollectionConstructionStrategy.Enumerable:
                    Func<IEnumerable<KeyValuePair<TKey, TValue>>, TDictionary> enumerableCtorFunc = dictionaryShape.GetEnumerableConstructor();
                    return CacheResult((Random random, int size) =>
                    {
                        if (size == 0)
                            return default!;

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
                    return CacheResult((Random random, int size) =>
                    {
                        if (size == 0)
                            return default!;

                        KeyValuePair<TKey, TValue>[] buffer = new KeyValuePair<TKey, TValue>[size];
                        int entrySize = GetChildSize(size, size);

                        for (int i = 0; i < size; i++)
                        {
                            buffer[i] = new(keyGenerator(random, entrySize), valueGenerator(random, entrySize));
                        }

                        return spanCtorFunc(buffer);
                    });

                default:
                    throw new NotSupportedException($"Type '{typeof(TDictionary)}' does not support random generation.");
            }
        }

        private static IEnumerable<KeyValuePair<Type, object>> CreateDefaultGenerators()
        {
            yield return Create((random, _) => NextBoolean(random));

            yield return Create((random, _) => (byte)random.Next(0, byte.MaxValue));
            yield return Create((random, _) => (ushort)random.Next(0, ushort.MaxValue));
            yield return Create((random, _) => (char)random.Next(0, char.MaxValue));
            yield return Create((random, _) => (uint)random.Next());
            yield return Create((random, _) => (ulong)random.Next());
            yield return Create((random, _) => new UInt128(NextULong(random), NextULong(random)));

            yield return Create((random, _) => (sbyte)random.Next(sbyte.MinValue, sbyte.MaxValue));
            yield return Create((random, _) => (short)random.Next(short.MinValue, short.MaxValue));
            yield return Create((random, _) => random.Next());
            yield return Create((random, _) => NextLong(random));
            yield return Create((random, _) => new Int128(NextULong(random), NextULong(random)));
            yield return Create((random, _) => new BigInteger(NextLong(random)));

            yield return Create((random, size) => (Half)((random.NextDouble() - 0.5) * size));
            yield return Create((random, size) => (float)((random.NextDouble() - 0.5) * size));
            yield return Create((random, size) => (random.NextDouble() - 0.5) * size);
            yield return Create((random, size) => (decimal)((random.NextDouble() - 0.5) * size));

            yield return Create((random, _) => new TimeSpan(NextLong(random)));
            yield return Create((random, _) => new DateTime(NextLong(random, DateTime.MinValue.Ticks, DateTime.MaxValue.Ticks)));
            yield return Create((random, _) => new TimeOnly(NextLong(random, 0, TimeOnly.MaxValue.Ticks)));
            yield return Create((random, _) => DateOnly.FromDateTime(new(NextLong(random, DateTime.MinValue.Ticks, DateTime.MaxValue.Ticks))));
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
                Span<byte> buffer = stackalloc byte[16];
                random.NextBytes(buffer);
                return new Guid(buffer);
            });

            yield return Create((random, _) => new Rune((char)random.Next(0, char.MaxValue)));

            yield return Create((random, size) =>
            {
                byte[] bytes = new byte[random.Next(0, Math.Max(7, size))];
                random.NextBytes(bytes);
                return bytes;
            });

            yield return Create(NextString);

            // TODO implement proper polymorphism
            yield return Create<object>((random, size) =>
            {
                return (random.Next(0, 5)) switch
                {
                    0 => NextBoolean(random),
                    1 => random.Next(-size, size),
                    2 => (random.NextDouble() - 0.5) * size,
                    3 => NextString(random, size),
                    _ => new TimeSpan(NextLong(random)),
                };
            });

            static KeyValuePair<Type, object> Create<T>(RandomGenerator<T> randomGenerator)
                => new(typeof(RandomGenerator<T>), randomGenerator);
        }

        private static long NextLong(Random random)
        {
            Span<byte> bytes = stackalloc byte[8];
            random.NextBytes(bytes);
            return BinaryPrimitives.ReadInt64LittleEndian(bytes);
        }

        private static ulong NextULong(Random random)
        {
            Span<byte> bytes = stackalloc byte[8];
            random.NextBytes(bytes);
            return BinaryPrimitives.ReadUInt64LittleEndian(bytes);
        }

        private static long NextLong(Random random, long min, long max) => Math.Clamp(NextLong(random), min, max);
        private static bool NextBoolean(Random random) => random.Next(0, 2) != 0;
        private static string NextString(Random random, int size)
        {
            int length = random.Next(0, Math.Max(7, size));
            return string.Create(length, random, Populate);
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
                1 => (int)Math.Round(parentSize * 0.9),
                _ => (int)Math.Round(parentSize / (double)totalChildren),
            };
        }
        private RandomGenerator<T>? TryGetCachedResult<T>()
            => _cache.GetOrAddDelayedValue<RandomGenerator<T>>(static holder => (r,s) => holder.Value!(r,s));

        private RandomGenerator<T> CacheResult<T>(RandomGenerator<T> generator)
        {
            _cache.Add(generator);
            return generator;
        }
    }
}
