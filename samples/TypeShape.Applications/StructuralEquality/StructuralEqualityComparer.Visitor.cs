using System.Collections;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace TypeShape.Applications.StructuralEquality;

public static partial class StructuralEqualityComparer
{
    private sealed class Visitor : TypeShapeVisitor
    {
        private readonly TypeCache _cache = new();
        private readonly Dictionary<Type, IEqualityComparer> _polymorphicComparers = new();

        public override object? VisitType<T>(ITypeShape<T> type, object? state)
        {
            if (TryGetCachedResult<T>() is { } comparer)
            {
                return comparer;
            }

            if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>() ||
                (typeof(IEquatable<T>).IsAssignableFrom(typeof(T)) && !IsImmutableArray(typeof(T))) ||
                typeof(T) == typeof(string))
            {
                return CacheResult(EqualityComparer<T>.Default);
            }

            if (typeof(T) == typeof(object))
            {
                var result = (EqualityComparer<T>)(object)new PolymorphicObjectEqualityComparer(this, type.Provider);
                return CacheResult(result);
            }

            switch (type.Kind)
            {
                case TypeKind.Nullable:
                    return type.GetNullableShape().Accept(this, state);

                case var kind when (kind.HasFlag(TypeKind.Dictionary)):
                    return type.GetDictionaryShape().Accept(this, state);

                case TypeKind.Enumerable:
                    return type.GetEnumerableShape().Accept(this, state);
            }

            return CacheResult(new ObjectEqualityComparer<T>()
            {
                PropertyComparers = type.GetProperties(nonPublic: false, includeFields: true)
                    .Where(prop => prop.HasGetter)
                    .Select(prop => (EqualityComparer<T>)prop.Accept(this, null)!)
                    .ToArray(),
            });
        }

        public override object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> property, object? state)
        {
            return new PropertyEqualityComparer<TDeclaringType, TPropertyType>
            {
                Getter = property.GetGetter(),
                PropertyTypeEqualityComparer = (EqualityComparer<TPropertyType>)property.PropertyType.Accept(this, null)!,
            };
        }

        public override object? VisitNullable<T>(INullableShape<T> nullableShape, object? state)
        {
            return CacheResult(new NullableEqualityComparer<T>
            {
                ElementComparer = (EqualityComparer<T>)nullableShape.ElementType.Accept(this, state)!
            });
        }

        public override object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryShape<TDictionary, TKey, TValue> dictionaryShape, object? state)
        {
            var keyComparer = (EqualityComparer<TKey>)dictionaryShape.KeyType.Accept(this, state)!;
            var valueComparer = (EqualityComparer<TValue>)dictionaryShape.ValueType.Accept(this, state)!;
            if (typeof(TDictionary).IsAssignableTo(typeof(Dictionary<TKey, TValue>)))
            {
                return CacheResult(new DictionaryOfKVEqualityComparer<TKey, TValue>
                {
                    KeyComparer = keyComparer,
                    ValueComparer = valueComparer,
                });
            }
            else
            {
                return CacheResult(new DictionaryEqualityComparer<TDictionary, TKey, TValue>
                {
                    KeyComparer = keyComparer,
                    ValueComparer = valueComparer,
                    GetDictionary = dictionaryShape.GetGetDictionary(),
                });
            }
        }

        public override object? VisitEnumerable<TEnumerable, TElement>(IEnumerableShape<TEnumerable, TElement> enumerableShape, object? state)
        {
            return CacheResult(new EnumerableEqualityComparer<TEnumerable, TElement>
            {
                ElementComparer = (EqualityComparer<TElement>)enumerableShape.ElementType.Accept(this, state)!,
                GetEnumerable = enumerableShape.GetGetEnumerable()
            });
        }

        public IEqualityComparer GetPolymorphicEqualityComparer(Type runtimeType, ITypeShapeProvider provider)
        {
            if (_polymorphicComparers.TryGetValue(runtimeType, out IEqualityComparer? comparer))
                return comparer;

            return provider.GetShape(runtimeType) is { } shape
                ? (IEqualityComparer)shape.Accept(this, null)!
                : throw new NotSupportedException(runtimeType.GetType().ToString());
        }

        private static bool IsImmutableArray(Type type) 
            => type.IsGenericType && type.IsValueType && type.GetGenericTypeDefinition() == typeof(ImmutableArray<>);

        private EqualityComparer<T>? TryGetCachedResult<T>()
        {
            return _cache.GetOrAddDelayedValue<EqualityComparer<T>>(static holder => new DelayedEqualityComparer<T>(holder));
        }

        private EqualityComparer<T> CacheResult<T>(EqualityComparer<T> comparer)
        {
            _cache.Add(comparer);
            _polymorphicComparers.Add(typeof(T), comparer);
            return comparer;
        }
    }
}
