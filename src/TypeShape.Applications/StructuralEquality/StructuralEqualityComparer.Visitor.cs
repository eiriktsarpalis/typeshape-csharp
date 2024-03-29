﻿using System.Collections;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using TypeShape.Applications.StructuralEquality.Comparers;

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
                (typeof(IEquatable<T>).IsAssignableFrom(typeof(T)) && !IsImmutableArrayOrMemory(typeof(T)) && !type.IsRecord) ||
                typeof(T) == typeof(string))
            {
                // Use default comparison for structs that don't contain references
                // and types implementing IEquatable<T> (except for records, immutable arrays and memory types)
                return CacheResult(EqualityComparer<T>.Default);
            }

            if (typeof(T) == typeof(object))
            {
                var result = (EqualityComparer<T>)(object)new PolymorphicObjectEqualityComparer(t => GetPolymorphicEqualityComparer(t, type.Provider));
                return CacheResult(result);
            }

            switch (type.Kind)
            {
                case TypeKind.Nullable:
                    return type.GetNullableShape().Accept(this, state);

                case TypeKind.Dictionary:
                    return type.GetDictionaryShape().Accept(this, state);

                case TypeKind.Enumerable:
                    return type.GetEnumerableShape().Accept(this, state);
                default:
                    return CacheResult(new ObjectEqualityComparer<T>()
                    {
                        PropertyComparers = type.GetProperties()
                            .Where(prop => prop.HasGetter)
                            .Select(prop => (EqualityComparer<T>)prop.Accept(this, null)!)
                            .ToArray(),
                    });
            }
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

        private static bool IsImmutableArrayOrMemory(Type type)
        {
            if (!type.IsGenericType || !type.IsValueType)
            {
                return false;
            }

            Type genericTypeDefinition = type.GetGenericTypeDefinition();
            return genericTypeDefinition == typeof(ImmutableArray<>) ||
                genericTypeDefinition == typeof(Memory<>) ||
                genericTypeDefinition == typeof(ReadOnlyMemory<>);
        }

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
