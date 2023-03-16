using System.Runtime.InteropServices;

namespace TypeShape.Applications.Counter;

public static class Counter
{
    // Defines the simplest possible generic traversal application:
    // walks the object graph returning a count of the number of nodes encountered.

    public static Func<T, long> Create<T>(IType<T> shape)
    {
        var visitor = new Visitor();
        return (Func<T, long>)shape.Accept(visitor, null)!;
    }

    private sealed class Visitor : ITypeShapeVisitor
    {
        private readonly Dictionary<Type, object> _visited = new();

        public object? VisitType<T>(IType<T> type, object? state)
        {
            if (TryGetVisitedValue<T>() is { } result)
                return result;

            if (typeof(T).IsPrimitive || typeof(T) == typeof(string) || typeof(T).IsEnum || 
                typeof(T) == typeof(Guid) || typeof(T) == typeof(DateTime) || typeof(T) == typeof(TimeSpan))
            {
                return CacheResult<T>(static _ => 1);
            }

            switch (type.Kind)
            {
                case TypeKind.Nullable:
                    return type.GetNullableType().Accept(this, null);
                case var k when (k.HasFlag(TypeKind.Dictionary)):
                    return type.GetDictionaryType().Accept(this, null);
                case TypeKind.Enumerable:
                    return type.GetEnumerableType().Accept(this, null);
            }

            Func<T, long>[] propertyCounters = type.GetProperties(nonPublic: false, includeFields: true)
                .Where(prop => prop.HasGetter)
                .Select(prop => (Func<T, long>)prop.Accept(this, null)!)
                .ToArray();

            return CacheResult<T>(value =>
            {
                if (value is null)
                    return 0;

                long count = 1;
                foreach (Func<T, long> propCounter in propertyCounters)
                    count += propCounter(value);

                return count;
            });
        }

        public object? VisitProperty<TDeclaringType, TPropertyType>(IProperty<TDeclaringType, TPropertyType> property, object? state)
        {
            Getter<TDeclaringType, TPropertyType> getter = property.GetGetter();
            var propertyTypeCounter = (Func<TPropertyType, long>)property.PropertyType.Accept(this, null)!;
            return new Func<TDeclaringType, long>(obj => propertyTypeCounter(getter(ref obj)));
        }

        public object? VisitNullable<T>(INullableType<T> nullableType, object? state) where T : struct
        {
            var elementTypeCounter = (Func<T, long>)nullableType.ElementType.Accept(this, null)!;
            return CacheResult<T?>(t => t.HasValue ? elementTypeCounter(t.Value) : 0);
        }

        public object? VisitEnumerableType<TEnumerable, TElement>(IEnumerableType<TEnumerable, TElement> enumerableType, object? state)
        {
            Func<TEnumerable, IEnumerable<TElement>> enumerableGetter = enumerableType.GetGetEnumerable();
            var elementTypeCounter = (Func<TElement, long>)enumerableType.ElementType.Accept(this, null)!;
            return CacheResult<TEnumerable>(enumerable =>
            {
                long count = 1;
                foreach (TElement element in enumerableGetter(enumerable))
                    count += elementTypeCounter(element);
                return count;
            });
        }

        public object? VisitDictionaryType<TDictionary, TKey, TValue>(IDictionaryType<TDictionary, TKey, TValue> dictionaryType, object? state) where TKey : notnull
        {
            Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> dictionaryGetter = dictionaryType.GetGetDictionary();
            var keyTypeCounter = (Func<TKey, long>)dictionaryType.KeyType.Accept(this, null)!;
            var valueTypeCounter = (Func<TValue, long>)dictionaryType.ValueType.Accept(this, null)!;
            return CacheResult<TDictionary>(dict =>
            {
                long count = 1;
                foreach (KeyValuePair<TKey, TValue> kvp in dictionaryGetter(dict))
                {
                    count += keyTypeCounter(kvp.Key);
                    count += valueTypeCounter(kvp.Value);
                }

                return count;
            });
        }

        public object? VisitEnum<TEnum, TUnderlying>(IEnumType<TEnum, TUnderlying> enumType, object? state) where TEnum : struct, Enum
        {
            throw new NotImplementedException();
        }

        public object? VisitConstructor<TDeclaringType, TArgumentState>(IConstructor<TDeclaringType, TArgumentState> constructor, object? state)
        {
            throw new NotImplementedException();
        }

        public object? VisitConstructorParameter<TArgumentState, TParameter>(IConstructorParameter<TArgumentState, TParameter> parameter, object? state)
        {
            throw new NotImplementedException();
        }

        private Func<T, long> CacheResult<T>(Func<T, long> counter)
        {
            ((DelayedResultHolder<T>)_visited[typeof(T)]).Result = counter;
            return counter;
        }

        private Func<T, long>? TryGetVisitedValue<T>()
        {
            ref object? entryRef = ref CollectionsMarshal.GetValueRefOrAddDefault(_visited, typeof(T), out bool exists);
            if (exists)
            {
                return ((DelayedResultHolder<T>)entryRef!).Result;
            }
            else
            {
                entryRef = new DelayedResultHolder<T>();
                return null;
            }
        }

        private sealed class DelayedResultHolder<T>
        {
            private Func<T, long>? _result;
            public Func<T, long> Result
            {
                get => _result ??= (t => _result!(t));
                set => _result = value;
            }
        }
    }
}
