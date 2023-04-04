using System.Collections;
using System.Runtime.CompilerServices;

namespace TypeShape.Applications.StructuralEquality;

public static partial class StructuralEqualityComparer
{
    private sealed class Visitor : TypeShapeVisitor
    {
        private readonly Dictionary<Type, IEqualityComparer> _visited = new();

        public override object? VisitType<T>(ITypeShape<T> type, object? state)
        {
            if (_visited.TryGetValue(type.Type, out IEqualityComparer? result))
            {
                return result;
            }

            if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>() ||
                typeof(IEquatable<T>).IsAssignableFrom(typeof(T)) ||
                typeof(T) == typeof(string))
            {
                return EqualityComparer<T>.Default;
            }

            if (typeof(T) == typeof(object))
            {
                var objectCmp = new PolymorphicObjectEqualityComparer(this, type.Provider);
                _visited.Add(typeof(T), objectCmp);
                return objectCmp;
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

            var objectComparer = new ObjectEqualityComparer<T>();
            _visited.Add(typeof(T), objectComparer);

            objectComparer.PropertyComparers = type.GetProperties(nonPublic: false, includeFields: true)
                .Where(prop => prop.HasGetter)
                .Select(prop => (IEqualityComparer<T>)prop.Accept(this, null)!)
                .ToArray();

            return objectComparer;
        }

        public override object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> property, object? state)
        {
            return new PropertyEqualityComparer<TDeclaringType, TPropertyType>
            {
                Getter = property.GetGetter(),
                PropertyTypeEqualityComparer = (IEqualityComparer<TPropertyType>)property.PropertyType.Accept(this, null)!,
            };
        }

        public override object? VisitNullable<T>(INullableShape<T> nullableShape, object? state)
        {
            var comparer = new NullableEqualityComparer<T>();
            _visited.Add(typeof(T?), comparer);
            comparer.ElementComparer = (IEqualityComparer<T>)nullableShape.ElementType.Accept(this, state)!;
            return comparer;
        }

        public override object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryShape<TDictionary, TKey, TValue> dictionaryShape, object? state)
        {
            if (typeof(TDictionary).IsAssignableTo(typeof(Dictionary<TKey, TValue>)))
            {
                var comparer = new DictionaryOfKVEqualityComparer<TKey, TValue>();
                _visited.Add(typeof(TDictionary), comparer);
                comparer.GetDictionary = (Func<Dictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>>)(object)dictionaryShape.GetGetDictionary();
                comparer.KeyComparer = (IEqualityComparer<TKey>)dictionaryShape.KeyType.Accept(this, state)!;
                comparer.ValueComparer = (IEqualityComparer<TValue>)dictionaryShape.ValueType.Accept(this, state)!;
                return comparer;
            }
            else
            {
                var comparer = new DictionaryEqualityComparer<TDictionary, TKey, TValue>();
                comparer.GetDictionary = dictionaryShape.GetGetDictionary();
                comparer.KeyComparer = (IEqualityComparer<TKey>)dictionaryShape.KeyType.Accept(this, state)!;
                comparer.ValueComparer = (IEqualityComparer<TValue>)dictionaryShape.ValueType.Accept(this, state)!;
                return comparer;
            }
        }

        public override object? VisitEnumerable<TEnumerable, TElement>(IEnumerableShape<TEnumerable, TElement> enumerableShape, object? state)
        {
            var comparer = new EnumerableEqualityComparer<TEnumerable, TElement>();
            _visited.Add(typeof(TEnumerable), comparer);
            comparer.ElementComparer = (IEqualityComparer<TElement>)enumerableShape.ElementType.Accept(this, state)!;
            comparer.GetEnumerable = enumerableShape.GetGetEnumerable();
            return comparer;
        }

        public IEqualityComparer GetPolymorphicEqualityComparer(Type runtimeType, ITypeShapeProvider provider)
        {
            if (_visited.TryGetValue(runtimeType, out IEqualityComparer? comparer))
                return comparer;

            return provider.GetShape(runtimeType) is ITypeShape shape
                ? (IEqualityComparer)shape.Accept(this, null)!
                : throw new NotSupportedException(runtimeType.GetType().ToString());
        }
    }
}
