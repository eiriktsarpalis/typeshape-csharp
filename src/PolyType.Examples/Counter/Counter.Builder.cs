using PolyType.Abstractions;
using PolyType.Utilities;

namespace PolyType.Examples.Counter;

public static partial class Counter
{
    private sealed class Builder(TypeGenerationContext generationContext) : TypeShapeVisitor, ITypeShapeFunc
    {
        public Func<T?, long> GetOrAddCounter<T>(ITypeShape<T> typeShape) => (Func<T?, long>)generationContext.GetOrAdd(typeShape)!;
        object? ITypeShapeFunc.Invoke<T>(ITypeShape<T> shape, object? _) => shape.Accept(this);

        public override object? VisitObject<T>(IObjectTypeShape<T> type, object? state)
        {
            Func<T, long>[] propertyCounters = type.GetProperties()
                .Where(prop => prop.HasGetter)
                .Select(prop => (Func<T, long>)prop.Accept(this)!)
                .ToArray();

            if (propertyCounters.Length == 0)
            {
                return new Func<T, long>(value => value is null ? 0 : 1);
            }

            return new Func<T, long>(value =>
            {
                if (value is null)
                {
                    return 0;
                }

                long count = 1;
                foreach (Func<T, long> propCounter in propertyCounters)
                {
                    count += propCounter(value);
                }

                return count;
            });
        }

        public override object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> property, object? state)
        {
            Getter<TDeclaringType, TPropertyType> getter = property.GetGetter();
            Func<TPropertyType, long> propertyTypeCounter = GetOrAddCounter(property.PropertyType);
            return new Func<TDeclaringType, long>(obj => propertyTypeCounter(getter(ref obj)));
        }

        public override object? VisitEnum<TEnum, TUnderlying>(IEnumTypeShape<TEnum, TUnderlying> enumShape, object? state) => 
            new Func<TEnum, long>(_ => 1);

        public override object? VisitNullable<T>(INullableTypeShape<T> nullableShape, object? state)
        {
            var elementTypeCounter = (Func<T, long>)nullableShape.ElementType.Accept(this)!;
            return new Func<T?, long>(t => t.HasValue ? elementTypeCounter(t.Value) : 0);
        }

        public override object? VisitEnumerable<TEnumerable, TElement>(IEnumerableTypeShape<TEnumerable, TElement> enumerableShape, object? state)
        {
            Func<TEnumerable, IEnumerable<TElement>> enumerableGetter = enumerableShape.GetGetEnumerable();
            Func<TElement, long> elementTypeCounter = GetOrAddCounter(enumerableShape.ElementType);
            return new Func<TEnumerable, long>(enumerable =>
            {
                if (enumerable is null)
                {
                    return 0;
                }

                long count = 1;
                foreach (TElement element in enumerableGetter(enumerable))
                {
                    count += elementTypeCounter(element);
                }

                return count;
            });
        }

        public override object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryTypeShape<TDictionary, TKey, TValue> dictionaryShape, object? state)
        {
            Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> dictionaryGetter = dictionaryShape.GetGetDictionary();
            Func<TKey, long> keyTypeCounter = GetOrAddCounter(dictionaryShape.KeyType);
            Func<TValue, long> valueTypeCounter = GetOrAddCounter(dictionaryShape.ValueType);
            return new Func<TDictionary, long>(dict =>
            {
                if (dict is null)
                {
                    return 0;
                }

                long count = 1;
                foreach (KeyValuePair<TKey, TValue> kvp in dictionaryGetter(dict))
                {
                    count += keyTypeCounter(kvp.Key);
                    count += valueTypeCounter(kvp.Value);
                }

                return count;
            });
        }
    }

    private sealed class DelayedCounterFactory : IDelayedValueFactory
    {
        public DelayedValue Create<T>(ITypeShape<T> typeShape) =>
            new DelayedValue<Func<T?, long>>(self => t => self.Result(t));
    }
}
