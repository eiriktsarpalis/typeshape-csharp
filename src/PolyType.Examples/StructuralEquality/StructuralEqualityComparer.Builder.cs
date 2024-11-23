using PolyType.Abstractions;
using PolyType.Examples.StructuralEquality.Comparers;
using PolyType.Utilities;
using System.Runtime.CompilerServices;

namespace PolyType.Examples.StructuralEquality;

public static partial class StructuralEqualityComparer
{
    private sealed class Builder(TypeGenerationContext generationContext) : TypeShapeVisitor, ITypeShapeFunc
    {
        public IEqualityComparer<T> GetOrAddEqualityComparer<T>(ITypeShape<T> shape) =>
            (IEqualityComparer<T>)generationContext.GetOrAdd(shape, this)!;

        object? ITypeShapeFunc.Invoke<T>(ITypeShape<T> typeShape, object? state) => typeShape.Accept(this);

        public override object? VisitObject<T>(IObjectTypeShape<T> type, object? state)
        {
            if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>() ||
                (typeof(IEquatable<T>).IsAssignableFrom(typeof(T)) && !type.IsRecordType))
            {
                // Use default comparison for types that don't contain references
                // and types implementing IEquatable<T> except for records.
                return EqualityComparer<T>.Default;
            }
            
            if (typeof(T) == typeof(object))
            {
                return new PolymorphicObjectEqualityComparer(generationContext.ParentCache!);
            }

            return new ObjectEqualityComparer<T>
            {
                PropertyComparers = type.GetProperties()
                    .Where(prop => prop.HasGetter)
                    .Select(prop => (EqualityComparer<T>)prop.Accept(this)!)
                    .ToArray(),
            };
        }

        public override object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> property, object? state)
        {
            return new PropertyEqualityComparer<TDeclaringType, TPropertyType>
            {
                Getter = property.GetGetter(),
                PropertyTypeEqualityComparer = GetOrAddEqualityComparer(property.PropertyType),
            };
        }

        public override object? VisitEnum<TEnum, TUnderlying>(IEnumTypeShape<TEnum, TUnderlying> enumShape, object? state)
        {
            return EqualityComparer<TEnum>.Default;
        }

        public override object? VisitNullable<T>(INullableTypeShape<T> nullableShape, object? state)
        {
            return new NullableEqualityComparer<T>
            {
                ElementComparer = GetOrAddEqualityComparer(nullableShape.ElementType),
            };
        }

        public override object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryTypeShape<TDictionary, TKey, TValue> dictionaryShape, object? state)
        {
            IEqualityComparer<TKey> keyComparer = GetOrAddEqualityComparer(dictionaryShape.KeyType);
            IEqualityComparer<TValue> valueComparer = GetOrAddEqualityComparer(dictionaryShape.ValueType);
            if (typeof(TDictionary).IsAssignableTo(typeof(Dictionary<TKey, TValue>)))
            {
                return new DictionaryOfKVEqualityComparer<TKey, TValue>
                {
                    KeyComparer = keyComparer,
                    ValueComparer = valueComparer,
                };
            }
            else
            {
                return new DictionaryEqualityComparer<TDictionary, TKey, TValue>
                {
                    KeyComparer = keyComparer,
                    ValueComparer = valueComparer,
                    GetDictionary = dictionaryShape.GetGetDictionary(),
                };
            }
        }

        public override object? VisitEnumerable<TEnumerable, TElement>(IEnumerableTypeShape<TEnumerable, TElement> enumerableShape, object? state)
        {
            return new EnumerableEqualityComparer<TEnumerable, TElement>
            {
                ElementComparer = GetOrAddEqualityComparer(enumerableShape.ElementType),
                GetEnumerable = enumerableShape.GetGetEnumerable()
            };
        }
    }
}
