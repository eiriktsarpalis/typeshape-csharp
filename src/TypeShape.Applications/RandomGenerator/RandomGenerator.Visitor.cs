using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using System.Xml.Linq;

namespace TypeShape.Applications.RandomGenerator;

public partial class RandomGenerator
{
    private delegate void RandomPropertySetter<T>(ref T value, Random random, int size);

    private sealed class Visitor : ITypeShapeVisitor
    {
        private Dictionary<Type, object> _visited = new(CreateDefaultGenerators());

        public object? VisitType<T>(ITypeShape<T> type, object? state)
        {
            if (_visited.TryGetValue(typeof(T), out object? result))
            {
                Debug.Assert(result is RandomGenerator<T> or DelayedResultValueHolder<T>);
                return result is DelayedResultValueHolder<T> delayedHolder
                    ? delayedHolder.Result
                    : result;
            }
            else
            {
                _visited.Add(typeof(T), new DelayedResultValueHolder<T>());
            }

            switch (type.Kind)
            {
                case TypeKind.Enum:
                    return type.GetEnumShape().Accept(this, null);
                case TypeKind.Nullable:
                    return type.GetNullableShape().Accept(this, null);
                case var k when ((k & TypeKind.Dictionary) != 0):
                    return type.GetDictionaryShape().Accept(this, null);
                case TypeKind.Enumerable:
                    return type.GetEnumerableShape().Accept(this, null);
            }

            RandomPropertySetter<T>[] propertySetters = type.GetProperties(nonPublic: false, includeFields: true)
                .Where(prop => prop.HasSetter)
                .Select(prop => (RandomPropertySetter<T>)prop.Accept(this, null)!)
                .ToArray();

            return type.GetConstructors(nonPublic: false)
                .OrderByDescending(ctor => ctor.ParameterCount)
                .Select(ctor => ctor.Accept(this, propertySetters))
                .First();
        }

        public object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> property, object? state)
        {
            Setter<TDeclaringType, TPropertyType> setter = property.GetSetter();
            RandomGenerator<TPropertyType> propertyGenerator = (RandomGenerator<TPropertyType>)property.PropertyType.Accept(this, null)!;
            return new RandomPropertySetter<TDeclaringType>((ref TDeclaringType obj, Random random, int size) => setter(ref obj, propertyGenerator(random, size)));
        }

        public object? VisitConstructor<TDeclaringType, TArgumentState>(IConstructorShape<TDeclaringType, TArgumentState> constructor, object? state)
        {
            if (state is null)
            {
                Debug.Assert(constructor.ParameterCount is 0 or 1);
                if (constructor.ParameterCount == 0)
                {
                    return constructor.GetDefaultConstructor();
                }
                else
                {
                    Debug.Assert(typeof(TArgumentState) == constructor.GetParameters().First().ParameterType.Type);
                    return constructor.GetParameterizedConstructor();
                }
            }

            var propertySetters = (RandomPropertySetter<TDeclaringType>[])state;

            if (constructor.ParameterCount == 0)
            {
                Func<TDeclaringType> func = constructor.GetDefaultConstructor();
                return CacheResult((Random random, int size) =>
                {
                    if (size == 0) 
                        return default!;

                    TDeclaringType obj = func();

                    int propertySize = GetChildSize(size, propertySetters.Length);
                    foreach (var propertySetter in propertySetters)
                        propertySetter(ref obj, random, propertySize);

                    return obj;
                });
            }
            else
            {
                Func<TArgumentState> argumentStateCtor = constructor.GetArgumentStateConstructor();
                Func<TArgumentState, TDeclaringType> ctor = constructor.GetParameterizedConstructor();
                RandomPropertySetter<TArgumentState>[] parameterSetters = constructor.GetParameters()
                    .Select(param => (RandomPropertySetter<TArgumentState>)param.Accept(this, null)!)
                    .ToArray();

                int totalChildren = parameterSetters.Length + propertySetters.Length;

                return CacheResult((Random random, int size) =>
                {
                    if (size == 0)
                        return default!;

                    int propertySize = GetChildSize(size, totalChildren);
                    TArgumentState argState = argumentStateCtor();

                    foreach (var parameterSetter in parameterSetters)
                        parameterSetter(ref argState, random, propertySize);

                    TDeclaringType obj = ctor(argState);

                    foreach (var propertySetter in propertySetters)
                        propertySetter(ref obj, random, propertySize);

                    return obj;
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

            if (GetDefaultConstructor() is { } defaultCtor)
            {
                var ctorFunc = (Func<TEnumerable>)defaultCtor.Accept(this, null)!;
                Setter<TEnumerable, TElement> addElementFunc = enumerableShape.GetAddElement();
                return CacheResult((Random random, int size) =>
                {
                    if (size == 0)
                        return default!;

                    TEnumerable obj = ctorFunc();
                    int length = random.Next(0, size);
                    int elementSize = GetChildSize(size, length);

                    for (int i = 0; i < length; i++)
                        addElementFunc(ref obj, elementGenerator(random, elementSize));

                    return obj;
                });
            }

            if (GetEnumerableConstructor() is { } enumerableCtor)
            {
                var ctorFunc = (Func<IEnumerable<TElement>, TEnumerable>)enumerableCtor.Accept(this, null)!;
                return CacheResult((Random random, int size) =>
                {
                    if (size == 0)
                        return default!;

                    List<TElement> buffer = new(size);
                    int length = random.Next(0, size);
                    int elementSize = GetChildSize(size, length);

                    for (int i = 0; i < length; i++)
                        buffer.Add(elementGenerator(random, elementSize));

                    return ctorFunc(buffer);
                });
            }

            throw new NotSupportedException($"Type '{typeof(TEnumerable)}' does not support random generation.");

            IConstructorShape? GetDefaultConstructor()
            {
                if (enumerableShape.IsMutable)
                {
                    return enumerableShape.Type.GetConstructors(nonPublic: false)
                        .FirstOrDefault(ctor => ctor.ParameterCount == 0);
                }

                return null;
            }

            IConstructorShape? GetEnumerableConstructor()
            {
                return enumerableShape.Type.GetConstructors(nonPublic: false)
                    .Where(ctor => ctor.ParameterCount == 1)
                    .FirstOrDefault(ctor => ctor.GetParameters().First().ParameterType.Type == typeof(IEnumerable<TElement>));
            }
        }

        public object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryShape<TDictionary, TKey, TValue> dictionaryShape, object? state)
            where TKey : notnull
        {
            var keyGenerator = (RandomGenerator<TKey>)dictionaryShape.KeyType.Accept(this, null)!;
            var valueGenerator = (RandomGenerator<TValue>)dictionaryShape.ValueType.Accept(this, null)!;

            if (GetDefaultConstructor() is { } defaultCtor)
            {
                var defaultCtorFunc = (Func<TDictionary>)defaultCtor.Accept(this, null)!;
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
            }

            if (GetEnumerableConstructor() is { } enumerableCtor)
            {
                var enumerableCtorFunc = (Func<IEnumerable<KeyValuePair<TKey, TValue>>, TDictionary>)enumerableCtor.Accept(this, null)!;
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
            }

            throw new NotSupportedException($"Type '{typeof(TDictionary)}' does not support random generation.");

            IConstructorShape? GetDefaultConstructor()
            {
                if (dictionaryShape.IsMutable)
                {
                    return dictionaryShape.Type.GetConstructors(nonPublic: false)
                        .FirstOrDefault(ctor => ctor.ParameterCount == 0);
                }

                return null;
            }

            IConstructorShape? GetEnumerableConstructor()
            {
                return dictionaryShape.Type.GetConstructors(nonPublic: false)
                    .Where(ctor => ctor.ParameterCount == 1)
                    .FirstOrDefault(ctor => ctor.GetParameters().First().ParameterType.Type == typeof(IEnumerable<KeyValuePair<TKey, TValue>>));
            }
        }

        private RandomGenerator<T> CacheResult<T>(RandomGenerator<T> generator)
        {
            var holder = (DelayedResultValueHolder<T>)_visited[typeof(T)];
            holder.Result = generator;
            return generator;
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

            yield return Create((random, size) => (Half)((random.NextDouble() - 0.5) * size));
            yield return Create((random, size) => (float)((random.NextDouble() - 0.5) * size));
            yield return Create((random, size) => (random.NextDouble() - 0.5) * size);
            yield return Create((random, size) => (decimal)((random.NextDouble() - 0.5) * size));

            yield return Create((random, _) => new TimeSpan(NextLong(random)));
            yield return Create((random, _) => new DateTime(NextLong(random, DateTime.MinValue.Ticks, DateTime.MaxValue.Ticks)));
            yield return Create((random, _) => new TimeOnly(NextLong(random, 0, TimeOnly.MaxValue.Ticks)));
            yield return Create((random, _) => DateOnly.FromDateTime(new(NextLong(random, DateTime.MinValue.Ticks, DateTime.MaxValue.Ticks))));
            yield return Create((random, _) => new DateTimeOffset(NextLong(random, DateTime.MinValue.Ticks, DateTime.MaxValue.Ticks), TimeSpan.FromHours(random.Next(-14, 14))));
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
                => new(typeof(T), randomGenerator);
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

        // Delayed delegate initializer for handling recursive types
        private sealed class DelayedResultValueHolder<T>
        {
            private RandomGenerator<T>? _result;

            public RandomGenerator<T> Result
            {
                get => _result is { } result ? result : (r, i) => _result!(r, i);
                set
                {
                    Debug.Assert(_result is null && value is not null);
                    _result = value;
                }
            }
        }
    }
}
