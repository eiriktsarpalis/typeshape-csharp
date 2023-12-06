namespace TypeShape.Applications.Counter;

public static partial class Counter
{
    private sealed class Visitor : TypeShapeVisitor
    {
        private readonly TypeCache _cache = new();

        public override object? VisitType<T>(ITypeShape<T> type, object? state)
        {
            if (TryGetCachedResult<T>() is { } result)
            {
                return result;
            }

            switch (type.Kind)
            {
                case TypeKind.Nullable:
                    return type.GetNullableShape().Accept(this, null);
                case TypeKind.Dictionary:
                    return type.GetDictionaryShape().Accept(this, null);
                case TypeKind.Enumerable:
                    return type.GetEnumerableShape().Accept(this, null);
            }

            Func<T, long>[] propertyCounters = type.GetProperties(includeFields: true)
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

        public override object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> property, object? state)
        {
            Getter<TDeclaringType, TPropertyType> getter = property.GetGetter();
            var propertyTypeCounter = (Func<TPropertyType, long>)property.PropertyType.Accept(this, null)!;
            return new Func<TDeclaringType, long>(obj => propertyTypeCounter(getter(ref obj)));
        }

        public override object? VisitNullable<T>(INullableShape<T> nullableShape, object? state) where T : struct
        {
            var elementTypeCounter = (Func<T, long>)nullableShape.ElementType.Accept(this, null)!;
            return CacheResult<T?>(t => t.HasValue ? elementTypeCounter(t.Value) : 0);
        }

        public override object? VisitEnumerable<TEnumerable, TElement>(IEnumerableShape<TEnumerable, TElement> enumerableShape, object? state)
        {
            Func<TEnumerable, IEnumerable<TElement>> enumerableGetter = enumerableShape.GetGetEnumerable();
            var elementTypeCounter = (Func<TElement, long>)enumerableShape.ElementType.Accept(this, null)!;
            return CacheResult<TEnumerable>(enumerable =>
            {
                if (enumerable is null)
                    return 0;

                long count = 1;
                foreach (TElement element in enumerableGetter(enumerable))
                    count += elementTypeCounter(element);
                return count;
            });
        }

        public override object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryShape<TDictionary, TKey, TValue> dictionaryShape, object? state)
        {
            Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> dictionaryGetter = dictionaryShape.GetGetDictionary();
            var keyTypeCounter = (Func<TKey, long>)dictionaryShape.KeyType.Accept(this, null)!;
            var valueTypeCounter = (Func<TValue, long>)dictionaryShape.ValueType.Accept(this, null)!;
            return CacheResult<TDictionary>(dict =>
            {
                if (dict is null)
                    return 0;

                long count = 1;
                foreach (KeyValuePair<TKey, TValue> kvp in dictionaryGetter(dict))
                {
                    count += keyTypeCounter(kvp.Key);
                    count += valueTypeCounter(kvp.Value);
                }

                return count;
            });
        }

        private Func<T, long>? TryGetCachedResult<T>()
            => _cache.GetOrAddDelayedValue<Func<T, long>>(static holder => (t => holder.Value!(t)));

        private Func<T?, long> CacheResult<T>(Func<T?, long> counter)
        {
            _cache.Add(counter);
            return counter;
        }
    }
}
