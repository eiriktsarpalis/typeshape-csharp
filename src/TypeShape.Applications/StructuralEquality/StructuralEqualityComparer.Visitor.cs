using System.Runtime.CompilerServices;

namespace TypeShape.Applications.StructuralEquality;

public static partial class StructuralEqualityComparer
{
    private class Visitor : ITypeShapeVisitor
    {
        private readonly Dictionary<Type, object> _cache = new();

        public object? VisitType<T>(IType<T> type, object? state)
        {
            if (_cache.TryGetValue(type.Type, out object? result))
            {
                return result;
            }

            if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>() ||
                typeof(IEquatable<T>).IsAssignableFrom(typeof(T)) ||
                typeof(T) == typeof(string))
            {
                return EqualityComparer<T>.Default;
            }

            switch (type.Kind)
            {
                case TypeKind.Nullable:
                    return type.GetNullableType().Accept(this, state);

                case var kind when (kind.HasFlag(TypeKind.Dictionary)):
                    return type.GetDictionaryType().Accept(this, state);

                case TypeKind.Enumerable:
                    return type.GetEnumerableType().Accept(this, state);
            }

            var objectComparer = new ObjectEqualityComparer<T>();
            _cache.Add(typeof(T), objectComparer);

            objectComparer.PropertyComparers = type.GetProperties(nonPublic: false, includeFields: true)
                .Where(prop => prop.HasGetter)
                .Select(prop => (IEqualityComparer<T>)prop.Accept(this, null)!)
                .ToArray();

            return objectComparer;
        }

        public object? VisitProperty<TDeclaringType, TPropertyType>(IProperty<TDeclaringType, TPropertyType> property, object? state)
        {
            return new PropertyEqualityComparer<TDeclaringType, TPropertyType>
            {
                Getter = property.GetGetter(),
                PropertyTypeEqualityComparer = (IEqualityComparer<TPropertyType>)property.PropertyType.Accept(this, null)!,
            };
        }

        public object? VisitNullable<T>(INullableType<T> nullableType, object? state) where T : struct
        {
            var comparer = new NullableEqualityComparer<T>();
            _cache.Add(typeof(T?), comparer);
            comparer.ElementComparer = (IEqualityComparer<T>)nullableType.ElementType.Accept(this, state)!;
            return comparer;
        }

        public object? VisitDictionaryType<TDictionary, TKey, TValue>(IDictionaryType<TDictionary, TKey, TValue> dictionaryType, object? state)
            where TKey : notnull
        {
            var comparer = new DictionaryEqualityComparer<TDictionary, TKey, TValue>();
            _cache.Add(typeof(TDictionary), comparer);
            comparer.GetEnumerable = dictionaryType.GetGetEnumerable();
            comparer.KeyComparer = (IEqualityComparer<TKey>)dictionaryType.KeyType.Accept(this, state)!;
            comparer.ValueComparer = (IEqualityComparer<TValue>)dictionaryType.ValueType.Accept(this, state)!;
            return comparer;
        }

        public object? VisitEnumerableType<TEnumerable, TElement>(IEnumerableType<TEnumerable, TElement> enumerableType, object? state)
        {
            var comparer = new EnumerableEqualityComparer<TEnumerable, TElement>();
            _cache.Add(typeof(TEnumerable), comparer);
            comparer.ElementComparer = (IEqualityComparer<TElement>)enumerableType.ElementType.Accept(this, state)!;
            comparer.GetEnumerable = enumerableType.GetGetEnumerable();
            return comparer;
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
    }
}
